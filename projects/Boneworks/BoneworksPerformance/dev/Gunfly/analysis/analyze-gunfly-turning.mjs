import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import {
  readCsv,
  rotate,
  chestAnchorError,
  projectedLagSeconds,
} from "./analyze-gunfly-slot-ab.mjs";
export const median = (a) => quantile(a, 0.5);
function quantile(values, q) {
  const a = values.filter(Number.isFinite).sort((x, y) => x - y);
  if (!a.length) return null;
  const p = (a.length - 1) * q,
    i = Math.floor(p);
  return a[i] + (a[Math.min(i + 1, a.length - 1)] - a[i]) * (p - i);
}
const stats = (a) => ({
  n: a.filter(Number.isFinite).length,
  median: median(a),
  p05: quantile(a, 0.05),
  p95: quantile(a, 0.95),
  maxAbs: a.some(Number.isFinite)
    ? Math.max(...a.filter(Number.isFinite).map(Math.abs))
    : null,
});
const vec = (r, p) => ["x", "y", "z"].map((k) => r[p + "_" + k]);
const q = (r, p) => ["x", "y", "z", "w"].map((k) => r[p + "_q" + k]);
const norm = (a) => Math.hypot(...a),
  sub = (a, b) => a.map((v, i) => v - b[i]);
const inverse = (q) => [-q[0], -q[1], -q[2], q[3]];
export function multiply(a, b) {
  return [
    a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1],
    a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0],
    a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3],
    a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2],
  ];
}
export function quaternionAngle(a, b) {
  const n = norm(a) * norm(b);
  return n
    ? (2 *
        Math.acos(
          Math.min(1, Math.abs(a.reduce((s, v, i) => s + v * b[i], 0) / n))
        ) *
        180) /
        Math.PI
    : null;
}
const wrap = (a) => ((((a + 180) % 360) + 360) % 360) - 180;
export function trackedAngles(rotation) {
  const f = rotate(rotation, [0, 0, 1]);
  return {
    angle: (Math.atan2(Math.hypot(f[0], f[2]), -f[1]) * 180) / Math.PI,
    yaw: (Math.atan2(f[0], f[2]) * 180) / Math.PI,
  };
}
export function schedule(mode, t) {
  const ramp = (a, b) => Math.max(0, Math.min(1, (t - a) / (b - a))),
    first = ramp(18, 20) - ramp(22, 24),
    second = ramp(24, 26) - ramp(28, 30);
  return {
    height: mode === "pitch-height" ? 1.76 - 0.1 * (first + second) : 1.76,
    angle: mode === "pitch-height" ? 45 + 15 * first - 15 * second : 45,
    yaw: mode === "yaw-turn" ? 45 * (first - second) : 0,
  };
}
export function inferredAges(mode, t, actual) {
  const interior = [
    [18, 20],
    [22, 24],
    [24, 26],
    [28, 30],
  ].some(([start, end]) => t > start + 0.1 && t < end - 0.1);
  if (!interior) return { yawMs: null, pitchMs: null, heightMs: null };
  const a = schedule(mode, t),
    before = schedule(mode, t - 0.001),
    after = schedule(mode, t + 0.001);
  const dy = (after.yaw - before.yaw) / 0.002,
    da = (after.angle - before.angle) / 0.002,
    dh = (after.height - before.height) / 0.002;
  return {
    yawMs: Math.abs(dy) > 1 ? (wrap(a.yaw - actual.yaw) / dy) * 1000 : null,
    pitchMs:
      Math.abs(da) > 1 ? (wrap(a.angle - actual.angle) / da) * 1000 : null,
    heightMs:
      Math.abs(dh) > 0.001 ? ((a.height - actual.height) / dh) * 1000 : null,
  };
}
export function inputStepMetrics(rows, mode) {
  const selected = rows.filter((r) => r.phase === "fixed_exit");
  const values = [];
  for (let i = 1; i < selected.length; i++) {
    const a = selected[i - 1],
      b = selected[i],
      t = b.elapsed_s;
    if (
      b.physics_tick - a.physics_tick !== 1 ||
      ![
        [18, 20],
        [22, 24],
        [24, 26],
        [28, 30],
      ].some(([start, end]) => a.elapsed_s > start + 0.1 && t < end - 0.1)
    )
      continue;
    const before = schedule(mode, t - 0.001),
      after = schedule(mode, t + 0.001),
      dt = b.fixed_time - a.fixed_time;
    if (dt <= 0 || dt > 0.02)
      throw Error("Unexpected physics-step clock interval");
    const aa = trackedAngles(q(a, "tracked")),
      ba = trackedAngles(q(b, "tracked"));
    const dy = (after.yaw - before.yaw) / 0.002,
      da = (after.angle - before.angle) / 0.002,
      dh = (after.height - before.height) / 0.002;
    values.push({
      extra: b.tick_in_frame > 1,
      wallDeltaMs: (t - a.elapsed_s) * 1000,
      fixedDeltaMs: dt * 1000,
      yawRatio: Math.abs(dy) > 1 ? wrap(ba.yaw - aa.yaw) / (dy * dt) : null,
      pitchRatio:
        Math.abs(da) > 1 ? wrap(ba.angle - aa.angle) / (da * dt) : null,
      heightRatio:
        Math.abs(dh) > 0.001 ? (b.tracked_y - a.tracked_y) / (dh * dt) : null,
    });
  }
  const result = {};
  for (const [key, keep] of Object.entries({
    all: () => true,
    first: (r) => !r.extra,
    extra: (r) => r.extra,
  })) {
    const a = values.filter(keep);
    result[key] = {
      n: a.length,
      wallDeltaMs: stats(a.map((r) => r.wallDeltaMs)),
      fixedDeltaMs: stats(a.map((r) => r.fixedDeltaMs)),
      yawStepRatio: stats(a.map((r) => r.yawRatio)),
      pitchStepRatio: stats(a.map((r) => r.pitchRatio)),
      heightStepRatio: stats(a.map((r) => r.heightRatio)),
    };
  }
  return result;
}
function segment(rows, start, end) {
  const r = rows.filter((r) => r.elapsed_s >= start && r.elapsed_s <= end);
  if (r.length < 2) return null;
  const a = r[0],
    b = r.at(-1),
    dt = b.elapsed_s - a.elapsed_s,
    fps = (r.length - 1) / dt,
    nominalDt = (fps > 90 ? 1 : 2) / 120;
  return {
    seconds: dt,
    renderHz: fps,
    physicsHz: (b.fixed_tick - a.fixed_tick) / dt,
    footVelocityReal: sub(vec(b, "foot"), vec(a, "foot")).map((v) => v / dt),
    footDisplacement: sub(vec(b, "foot"), vec(a, "foot")),
    ticksPerRender: stats(
      r.slice(1).map((v, i) => v.fixed_tick - r[i].fixed_tick)
    ),
    perFramePacingFraction:
      r
        .slice(1)
        .filter(
          (v) =>
            v.render_dt_s >= 0.75 * nominalDt &&
            v.render_dt_s <= 1.25 * nominalDt
        ).length /
      (r.length - 1),
  };
}
// Keep phase and body state distinct. Prefix reads occur before native input
// processing; fixed_exit is after RigManager code, before Unity's solver step.
function phaseMetrics(rows, mode) {
  let previousLate = null,
    enter = null,
    previousEnter = null;
  const values = [],
    lateValues = [];
  for (const r of rows) {
    if (r.phase.startsWith("late_")) {
      if (previousLate) {
        const anchorQ = multiply(
          inverse(q(previousLate, "chest")),
          q(previousLate, "host")
        );
        lateValues.push({
          phase: r.phase,
          time: r.elapsed_s,
          errorM: norm(chestAnchorError(r, previousLate)),
          errorDeg: quaternionAngle(
            q(r, "host"),
            multiply(q(r, "chest"), anchorQ)
          ),
        });
      }
      if (r.phase === "late_exit") previousLate = r;
      continue;
    }
    if (r.phase === "fixed_enter_before_mod") {
      previousEnter = enter;
      enter = r;
    }
    if (r.phase !== "fixed_before_native_after_mod" || !enter || !previousLate)
      continue;
    const error = chestAnchorError(r, previousLate),
      ticks = previousEnter ? r.physics_tick - previousEnter.physics_tick : 0;
    const velocity =
      ticks > 0
        ? sub(vec(enter, "chest"), vec(previousEnter, "chest")).map(
            (v) => (v * 120) / ticks
          )
        : null;
    const anchorQ = multiply(
      inverse(q(previousLate, "chest")),
      q(previousLate, "host")
    );
    const predicted = multiply(q(r, "chest"), anchorQ);
    const lag = velocity ? projectedLagSeconds(error, velocity) : null;
    values.push({
      time: r.elapsed_s,
      extra: r.tick_in_frame > 1,
      hookShift: norm(sub(vec(r, "host"), vec(enter, "host"))),
      hookRotation: quaternionAngle(q(r, "host"), q(enter, "host")),
      anchorError: norm(error),
      anchorAngle: quaternionAngle(q(r, "host"), predicted),
      lagMs: lag === null ? null : lag * 1000,
    });
  }
  const groups = {};
  for (const key of [
    "fixed_enter_before_mod",
    "fixed_before_native_after_mod",
    "fixed_exit",
    "fixed_exit_first",
    "fixed_exit_extra",
    "late_exit",
  ]) {
    const phase = key.startsWith("fixed_exit_") ? "fixed_exit" : key;
    const r = rows.filter(
      (r) =>
        r.phase === phase &&
        r.elapsed_s >= 18 &&
        r.elapsed_s < 30 &&
        (key === "fixed_exit_first"
          ? r.tick_in_frame === 1
          : key === "fixed_exit_extra"
          ? r.tick_in_frame > 1
          : true)
    );
    const yawAges = [],
      angleAges = [],
      heightAges = [],
      heightErrors = [],
      angleErrors = [],
      yawErrors = [];
    for (const row of r) {
      const t = row.elapsed_s,
        actual = trackedAngles(q(row, "tracked")),
        a = schedule(mode, t);
      heightErrors.push(row.tracked_y - a.height);
      angleErrors.push(wrap(actual.angle - a.angle));
      yawErrors.push(wrap(actual.yaw - a.yaw));
      // Near ramp corners a delayed sample may cross into a hold or another
      // slope. Dividing that error by the current slope would give a false age.
      const ages = inferredAges(mode, t, { ...actual, height: row.tracked_y });
      if (ages.yawMs !== null) yawAges.push(ages.yawMs);
      if (ages.pitchMs !== null) angleAges.push(ages.pitchMs);
      if (ages.heightMs !== null) heightAges.push(ages.heightMs);
    }
    groups[key] = {
      rows: r.length,
      yawAgeMs: stats(yawAges),
      pitchAgeMs: stats(angleAges),
      heightAgeMs: stats(heightAges),
      heightErrorM: stats(heightErrors),
      pitchErrorDeg: stats(angleErrors),
      yawErrorDeg: stats(yawErrors),
    };
  }
  const slot = {};
  for (const [name, predicate] of Object.entries({
    pre: (r) => r.time >= 12 && r.time < 17,
    turn: (r) => r.time >= 18 && r.time < 30,
    turnFirst: (r) => r.time >= 18 && r.time < 30 && !r.extra,
    turnExtra: (r) => r.time >= 18 && r.time < 30 && r.extra,
    post: (r) => r.time >= 32 && r.time < 39,
  })) {
    const a = values.filter(predicate);
    slot[name] = {
      n: a.length,
      hookShiftM: stats(a.map((v) => v.hookShift)),
      hookRotationDeg: stats(a.map((v) => v.hookRotation)),
      chestAnchorErrorM: stats(a.map((v) => v.anchorError)),
      chestAnchorAngleDeg: stats(a.map((v) => v.anchorAngle)),
      projectedLagMs: stats(a.map((v) => v.lagMs)),
    };
  }
  const lateSlot = {};
  for (const phase of [
    "late_enter_before_restore",
    "late_before_native_after_restore",
    "late_exit",
  ]) {
    const a = lateValues.filter(
      (r) => r.phase === phase && r.time >= 18 && r.time < 30
    );
    lateSlot[phase] = {
      n: a.length,
      chestAnchorErrorM: stats(a.map((r) => r.errorM)),
      chestAnchorAngleDeg: stats(a.map((r) => r.errorDeg)),
    };
  }
  return { input: groups, slot, lateSlot };
}
export function sampleCurve(rows) {
  const source = rows.filter(
    (r) => r.phase === "fixed_before_native_after_mod"
  );
  const curve = [];
  let i = 0;
  for (let t = 12; t <= 39 + 0.00001; t += 0.1) {
    while (i + 1 < source.length && source[i + 1].elapsed_s <= t) i++;
    const a = source[i],
      b = source[i + 1];
    if (!a || !b || t < a.elapsed_s || b.elapsed_s - a.elapsed_s > 0.05)
      continue;
    const f = (t - a.elapsed_s) / (b.elapsed_s - a.elapsed_s);
    const blend = (p) => vec(a, p).map((v, k) => v + (vec(b, p)[k] - v) * f);
    const aq = multiply(inverse(q(a, "chest")), q(a, "host")),
      bq = multiply(inverse(q(b, "chest")), q(b, "host"));
    let sign = aq.reduce((s, v, k) => s + v * bq[k], 0) < 0 ? -1 : 1;
    const qq = aq.map((v, k) => v + (sign * bq[k] - v) * f),
      qn = norm(qq);
    curve.push({
      t: +t.toFixed(3),
      foot: blend("foot"),
      hostChest: blend("host_chest"),
      hostChestQ: qq.map((v) => v / qn),
      trackedHeight: a.tracked_y + (b.tracked_y - a.tracked_y) * f,
    });
  }
  const origin = curve.find((r) => r.t === 18);
  if (origin) for (const r of curve) r.footFrom18 = sub(r.foot, origin.foot);
  return curve;
}
export function compareCurves(a, b, start = 18, end = 30) {
  const other = new Map(b.map((r) => [r.t, r]));
  const distances = [],
    hostDistances = [],
    angles = [];
  for (const r of a) {
    const s = other.get(r.t);
    if (!s || r.t < start || r.t > end) continue;
    if (r.footFrom18 && s.footFrom18)
      distances.push(norm(sub(r.footFrom18, s.footFrom18)));
    hostDistances.push(norm(sub(r.hostChest, s.hostChest)));
    angles.push(quaternionAngle(r.hostChestQ, s.hostChestQ));
  }
  return {
    pathDifferenceM: stats(distances),
    hostChestPositionDifferenceM: stats(hostDistances),
    hostChestRotationDifferenceDeg: stats(angles),
  };
}
async function main() {
  const [root, output, flag] = process.argv.slice(2);
  if (!output) throw Error("Supply run root and separate JSON");
  const progress = JSON.parse(
    await fs.readFile(path.join(root, "progress.json"), "utf8")
  );
  if (flag && flag !== "--partial") throw Error("Unknown analysis option");
  if (
    flag !== "--partial" &&
    (progress.active ||
      progress.phase !== "complete" ||
      progress.completed !== progress.total)
  ) {
    throw Error("Run must complete before final analysis");
  }
  const results = [];
  for (const line of (
    await fs.readFile(path.join(root, "retry-requests.tsv"), "utf8")
  )
    .trim()
    .split(/\r?\n/)) {
    const [id, gun, hz, angle, height, stick, mode] = line.split("\t"),
      dir = path.join(root, `${id}-${gun}-${hz}Hz-${angle}deg`);
    if (Number(hz) !== 120)
      throw Error(
        "Turning analysis is calibrated for 120 Hz; use matrix validation for other rates."
      );
    if (flag === "--partial") {
      try {
        await fs.access(path.join(dir, "mod-state-end.txt"));
      } catch {
        continue;
      }
    }
    const frames = await readCsv(path.join(dir, "attempt-01-frames.csv")),
      phases = await readCsv(path.join(dir, "slot-phases.csv"));
    if (
      !phases.length ||
      !["tracked_qw", "command_yaw", "foot_y"].every((k) =>
        Number.isFinite(phases[0][k])
      )
    )
      throw Error("Missing extended phase columns");
    results.push({
      id,
      mode,
      frameRows: frames.length,
      phaseRows: phases.length,
      segments: {
        pre: segment(frames, 12, 17),
        turn: segment(frames, 18, 30),
        post: segment(frames, 32, 39),
      },
      ...phaseMetrics(phases, mode),
      inputSteps: inputStepMetrics(phases, mode),
      curve: sampleCurve(phases),
      modState: await fs.readFile(path.join(dir, "mod-state-end.txt"), "utf8"),
      geometryStart: await fs.readFile(
        path.join(dir, "geometry-start.txt"),
        "utf8"
      ),
      geometryEnd: await fs.readFile(
        path.join(dir, "geometry-end.txt"),
        "utf8"
      ),
      status: await fs.readFile(
        path.join(dir, "attempt-01-status.txt"),
        "utf8"
      ),
    });
  }
  const withinVanillaRepeatability = [];
  for (let i = 0; i < results.length; i++)
    for (let j = i + 1; j < results.length; j++)
      if (results[i].mode === results[j].mode)
        withinVanillaRepeatability.push({
          a: results[i].id,
          b: results[j].id,
          mode: results[i].mode,
          ...compareCurves(results[i].curve, results[j].curve),
        });
  await fs.mkdir(path.dirname(output), { recursive: true });
  await fs.writeFile(
    output,
    JSON.stringify(
      {
        root,
        partial: !!progress.active,
        progress,
        results,
        withinConditionRepeatability: withinVanillaRepeatability,
      },
      null,
      2
    ),
    { flag: "wx" }
  );
  for (const r of results)
    console.log(
      `${r.id} ${r.mode} preHz=${r.segments.pre.physicsHz.toFixed(
        3
      )} postVz=${r.segments.post.footVelocityReal[2].toFixed(3)} slotTurnM=${
        r.slot.turn.hookShiftM.median
      } inputYawAge=${r.input.fixed_exit.yawAgeMs.median} inputHeightAge=${
        r.input.fixed_exit.heightAgeMs.median
      }`
    );
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href)
  await main();
