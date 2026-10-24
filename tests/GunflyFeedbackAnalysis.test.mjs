import test from "node:test";
import assert from "node:assert/strict";
import {
  summarize,
  findCleanWindows,
  summarizeFromRest,
} from "../projects/Boneworks/BoneworksPerformance/dev/Gunfly/analysis/analyze-gunfly-feedback.mjs";
const trajectory = (slope = 0) =>
  Array.from({ length: 401 }, (_, i) => ({
    elapsed_s: 10 + i / 20,
    fixed_tick: i,
    render_dt_s: 0.05,
    foot_x: 0,
    foot_y: (slope * i) / 20,
    foot_z: -i / 20,
    game_time: i / 20,
    magazine_contact: 1,
  }));
test("straight level and1.5 climb use signed forward progress and actual time", () => {
  const flat = summarize(trajectory(), 20);
  assert.equal(flat.withinHalfMeterCruisePath, true);
  assert.deepEqual(flat.terminalFootVelocityReal, [0, 0, -1]);
  const climb = summarize(trajectory(1.5), 20, 1.5);
  assert.ok(climb.routeError.max < 1e-12);
  assert.equal(climb.terminalPhysicsHz, 20);
  assert.equal(climb.terminalCleanFraction, 1);
});
test("endpoint recovery cannot hide path violation or lateral motion", () => {
  const rows = trajectory();
  rows[100].foot_y = 2;
  rows[200].foot_x = 3;
  const result = summarize(rows, 20);
  assert.equal(result.withinHalfMeterCruisePath, false);
  assert.equal(result.routeError.max, 2);
  assert.equal(result.crossTrack.max, 3);
});
test("missing foot data and non-monotonic timestamps are rejected", () => {
  const missing = trajectory();
  missing[4].foot_y = null;
  assert.throws(() => summarize(missing, 20), /Missing numeric/);
  const bad = trajectory();
  bad[4].elapsed_s = bad[3].elapsed_s;
  assert.throws(() => summarize(bad, 20), /Non-monotonic/);
});

const clean = () =>
  trajectory().map((r) => ({
    ...r,
    velocity_valid: 1,
    world_vx_m_s: 0,
    world_vy_m_s: 0,
    world_vz_m_s: -1,
    raw_hmd_y_m: 1.55,
    command_height: 1.55,
    look_angle_from_down_deg: 45,
    command_angle: 45,
    left_x: 0,
    left_y: -1,
    right_x: 0,
    right_y: -1,
    sprint_held: 1,
    native_jog: 1,
  }));
test("clean window admits dynamic height and keeps latest rather than fastest window", () => {
  const rows = clean();
  for (let i = 1; i < rows.length; i++) {
    rows[i].command_height = 1.5 + i * 0.00001;
    rows[i].raw_hmd_y_m = rows[i - 1].command_height;
  }
  const result = findCleanWindows(rows, 20);
  assert.ok(result.candidateCount > 0);
  assert.ok(result.latest.endElapsed > 29);
  assert.deepEqual(result.latest.footVelocityReal, [0, 0, -1]);
});
test("rate mismatch, pose mismatch, missing inputs and unstable motion are rejected", () => {
  for (const kind of [
    "rate",
    "pose",
    "input",
    "unstable",
    "missing",
    "missing_axis",
  ]) {
    const rows = clean();
    for (let i = 0; i < rows.length; i++) {
      if (kind === "rate") rows[i].elapsed_s = 10 + i / 19;
      if (kind === "pose") rows[i].raw_hmd_y_m = 1.7;
      if (kind === "input") rows[i].sprint_held = 0;
      if (kind === "unstable") rows[i].world_vz_m_s = -1 - i * 0.1;
      if (kind === "missing") rows[i].world_vy_m_s = null;
      if (kind === "missing_axis") rows[i].left_x = undefined;
    }
    assert.equal(findCleanWindows(rows, 20).candidateCount, 0, kind);
  }
});

test("500m clock starts at prelaunch zero and interpolates crossing, not first logged frame", () => {
  const rows = Array.from({ length: 401 }, (_, i) => ({
    elapsed_s: (i + 1) / 20,
    foot_x: 3,
    foot_y: 2,
    foot_z: 7 - (i + 1) * 2,
    game_time: 100 + (i + 1) / 20,
  }));
  const result = summarizeFromRest(rows, { x: 3, y: 2, z: 7, game_time: 100 });
  assert.equal(result.reachedTarget, true);
  assert.equal(result.realSeconds, 12.5);
  assert.equal(result.gameSeconds, 12.5);
  assert.equal(result.pathWithinHalfMeter, true);
  const between = summarizeFromRest(
    rows,
    { x: 3, y: 2, z: 7, game_time: 100 },
    0,
    501
  );
  assert.ok(Math.abs(between.realSeconds - 12.525) < 1e-10);
});
test("from-rest path retains earlier failures, missing clocks and incomplete travel", () => {
  const rows = Array.from({ length: 301 }, (_, i) => ({
    elapsed_s: (i + 1) / 20,
    foot_x: 0,
    foot_y: i === 40 ? 0.6 : 0,
    foot_z: -(i + 1) * 2,
    game_time: 10 + (i + 1) / 20,
  }));
  const origin = { x: 0, y: 0, z: 0, game_time: 10 };
  assert.equal(summarizeFromRest(rows, origin).pathWithinHalfMeter, false);
  const short = summarizeFromRest(rows.slice(0, 10), origin);
  assert.equal(short.reachedTarget, false);
  assert.equal(short.realSeconds, null);
  assert.throws(
    () => summarizeFromRest(rows, { x: 0, y: 0, z: 0 }),
    /Missing prelaunch/
  );
});

test("game velocity median uses the clean measurement interval, not the preceding stability interval", () => {
  const rows = clean();
  for (const r of rows) r.world_vy_m_s = (r.elapsed_s - 10) * 0.001;
  const latest = findCleanWindows(rows, 20).latest;
  const values = rows
    .filter(
      (r) =>
        r.elapsed_s > latest.startElapsed && r.elapsed_s <= latest.endElapsed
    )
    .map((r) => r.world_vy_m_s)
    .sort((a, b) => a - b);
  const mid = values.length >> 1,
    expected =
      values.length % 2 ? values[mid] : (values[mid - 1] + values[mid]) / 2;
  assert.equal(latest.worldVelocityGameMedian[1], expected);
  assert.ok(latest.gameMedianWindowSeconds < 1.1);
  assert.ok(latest.stabilityWindowSeconds >= 5);
});
