import test from "node:test";
import assert from "node:assert/strict";
import {
  multiply,
  quaternionAngle,
  trackedAngles,
  schedule,
  compareCurves,
  inferredAges,
  inputStepMetrics,
} from "../projects/Boneworks/BoneworksFrameFix/dev/Gunfly/analysis/analyze-gunfly-turning.mjs";
test("quaternion comparison is sign independent and relative product preserves identity", () => {
  const h = Math.sqrt(0.5),
    q = [0, h, 0, h];
  assert.ok(
    quaternionAngle(
      q,
      q.map((v) => -v)
    ) < 1e-5
  );
  assert.ok(quaternionAngle(multiply([0, -h, 0, h], q), [0, 0, 0, 1]) < 1e-5);
  assert.ok(Math.abs(trackedAngles(q).yaw - 90) < 1e-8);
});
test("analysis schedule matches declared ramp endpoints and heights", () => {
  assert.equal(schedule("yaw-turn", 19).yaw, 22.5);
  assert.equal(schedule("yaw-turn", 27).yaw, -45);
  assert.equal(schedule("pitch-height", 21).angle, 60);
  assert.ok(Math.abs(schedule("pitch-height", 21).height - 1.66) < 1e-10);
  assert.equal(schedule("pitch-height", 27).angle, 30);
  assert.equal(schedule("pitch-height", 31).height, 1.76);
});
test("path and chest-relative pose deviations are separately retained", () => {
  const a = [
    {
      t: 20,
      footFrom18: [0, 0, 10],
      hostChest: [0, 0, 0],
      hostChestQ: [0, 0, 0, 1],
    },
  ];
  const b = [
    {
      t: 20,
      footFrom18: [0, 1, 10],
      hostChest: [0.2, 0, 0],
      hostChestQ: [0, Math.sqrt(0.5), 0, Math.sqrt(0.5)],
    },
  ];
  const r = compareCurves(a, b);
  assert.equal(r.pathDifferenceM.median, 1);
  assert.equal(r.hostChestPositionDifferenceM.median, 0.2);
  assert.ok(Math.abs(r.hostChestRotationDifferenceDeg.median - 90) < 1e-10);
});
test("known pose delays are recovered on either ramp direction, never at corners", () => {
  for (const t of [19, 23, 25, 29]) {
    const previous = schedule("pitch-height", t - 0.008);
    const age = inferredAges("pitch-height", t, previous);
    assert.ok(Math.abs(age.heightMs - 8) < 1e-6);
    assert.ok(Math.abs(age.pitchMs - 8) < 1e-6);
    assert.ok(
      Math.abs(
        inferredAges("yaw-turn", t, schedule("yaw-turn", t - 0.008)).yawMs - 8
      ) < 1e-6
    );
  }
  assert.equal(
    inferredAges("pitch-height", 20, schedule("pitch-height", 19.992)).heightMs,
    null
  );
  assert.equal(
    inferredAges("straight", 19, schedule("straight", 18)).heightMs,
    null
  );
});
test("two fresh back-to-back poses are not mistaken for evenly spaced simulation input", () => {
  const sample = (tick, t, angle, extra) => {
    const half = (angle * Math.PI) / 360;
    return {
      phase: "fixed_exit",
      physics_tick: tick,
      tick_in_frame: extra ? 2 : 1,
      elapsed_s: t,
      fixed_time: tick / 120,
      tracked_qx: 0,
      tracked_qy: Math.sin(half),
      tracked_qz: 0,
      tracked_qw: Math.cos(half),
      tracked_y: 1.76,
    };
  };
  const rows = [
    sample(1, 19, 22.5, true),
    sample(2, 19 + 1 / 60, 22.875, false),
    sample(3, 19 + 1 / 60 + 0.00001, 22.875225, true),
  ];
  const result = inputStepMetrics(rows, "yaw-turn");
  assert.ok(Math.abs(result.first.yawStepRatio.median - 2) < 1e-8);
  assert.ok(result.extra.yawStepRatio.median < 0.002);
  assert.ok(Math.abs(result.extra.fixedDeltaMs.median - 1000 / 120) < 1e-8);
});
