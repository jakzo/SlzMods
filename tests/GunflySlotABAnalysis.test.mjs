import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import {
  rotate,
  chestAnchorError,
  projectedLagSeconds,
  summarizePhases,
} from "../projects/Boneworks/BoneworksFrameFix/dev/Gunfly/analysis/analyze-gunfly-slot-ab.mjs";
import { readCsv } from "../projects/Boneworks/BoneworksFrameFix/dev/Gunfly/analysis/analyze-gunfly-slot-ab.mjs";

test("raw CSV parsing preserves missing samples and rejects malformed numeric data", async (context) => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "gunfly-csv-"));
  context.after(() => fs.rm(directory, { recursive: true, force: true }));
  const file = path.join(directory, "frames.csv");
  await fs.writeFile(file, "phase,value\nfixed_exit,\n");
  assert.deepEqual(await readCsv(file), [{ phase: "fixed_exit", value: null }]);
  for (const line of [
    "fixed_exit,NaN",
    "fixed_exit,Infinity",
    "fixed_exit,1,2",
  ]) {
    await fs.writeFile(file, "phase,value\n" + line + "\n");
    await assert.rejects(readCsv(file));
  }
});
test("rigid chest anchor includes rotation and offset, not just bone-origin subtraction", () => {
  const half = Math.sqrt(0.5);
  assert.ok(Math.abs(rotate([0, half, 0, half], [1, 0, 0])[2] + 1) < 1e-12);
  const r = {
    chest_x: 2,
    chest_y: 3,
    chest_z: 4,
    chest_qx: 0,
    chest_qy: half,
    chest_qz: 0,
    chest_qw: half,
    host_x: 2,
    host_y: 3,
    host_z: 3,
  };
  assert.ok(
    Math.hypot(
      ...chestAnchorError(r, {
        host_chest_x: 1,
        host_chest_y: 0,
        host_chest_z: 0,
      })
    ) < 1e-12
  );
});
test("projected lag has correct sign and rejects stationary ambiguity", () => {
  assert.ok(
    Math.abs(projectedLagSeconds([0, 0, 10 / 120], [0, 0, -10]) - 1 / 120) <
      1e-12
  );
  assert.equal(projectedLagSeconds([1, 0, 0], [0, 0, 0]), null);
});
test("phase pairs distinguish first stale tick and corrected extra tick without rewriting geometry", () => {
  const make = (phase, frame, tick, tickInFrame, chestZ, hostZ) => ({
    phase,
    unity_frame: frame,
    physics_tick: tick,
    tick_in_frame: tickInFrame,
    elapsed_s: 50 + tick / 120,
    chest_x: 0,
    chest_y: 0,
    chest_z: chestZ,
    chest_qx: 0,
    chest_qy: 0,
    chest_qz: 0,
    chest_qw: 1,
    host_x: 0,
    host_y: 0,
    host_z: hostZ,
    mag_x: 0,
    mag_y: 0,
    mag_z: hostZ,
    host_chest_x: 0,
    host_chest_y: 0,
    host_chest_z: 0,
    mag_host_x: 0,
    mag_host_y: 0,
    mag_host_z: 0,
    host_local_x: 0,
    host_local_y: 0,
    host_local_z: 0,
  });
  const rows = [
    make("late_exit", 10, 0, 1, 0, 0),
    make("fixed_enter_before_mod", 11, 1, 1, -0.1, 0),
    make("fixed_before_native_after_mod", 11, 1, 1, -0.1, 0),
    make("fixed_enter_before_mod", 11, 2, 2, -0.2, 0),
    make("fixed_before_native_after_mod", 11, 2, 2, -0.2, -0.2),
  ];
  const result = summarizePhases(rows, 120);
  assert.equal(result.groups.lagFirst.hookHostShiftM.median, 0);
  assert.equal(result.groups.lagExtra.hookHostShiftM.median, 0.2);
  assert.equal(result.groups.lagExtra.anchorErrorM.median, 0);
});
