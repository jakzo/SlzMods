import test from "node:test";
import assert from "node:assert/strict";
import { csv } from "../projects/Boneworks/FrameFix/dev/Gunfly/analysis/export-table.mjs";
import { compare } from "../projects/Boneworks/FrameFix/dev/Gunfly/analysis/compare-matrix.mjs";
test("CSV preserves quotes, commas, newlines and blank cells", () => {
  assert.equal(
    csv({ headers: ["a", "b"], rows: [['x,"y"\nz', null]] }),
    '"a","b"\r\n"x,""y""\nz",""\r\n'
  );
  assert.throws(() => csv({ headers: ["a"], rows: [["a", "b"]] }));
});
const headers = [
  "case_id",
  "condition",
  "rifle_prefab",
  "requested_hz",
  "look_angle_from_down_deg",
  "hmd_height_m",
  "player_height_m",
  "status",
  "reason",
  "world_horizontal_median_m_s",
  "world_vertical_median_m_s",
  "world_vx_median_m_s",
  "world_vz_median_m_s",
  "frame_log",
];
const table = (condition, status = "valid", speed = "20") => ({
  headers,
  rows: [
    [
      "001",
      condition,
      "M16",
      "120",
      "45",
      "1.76",
      "1.76",
      status,
      "evidence",
      speed,
      "-7",
      "0",
      "-20",
      "attempt-01.csv",
    ],
  ],
});
test("matrix comparison keeps speed deltas and failures distinct", () => {
  assert.equal(
    compare(table("absent"), table("full", "valid", "21"))[0]
      .horizontalDeltaMps,
    1
  );
  const failed = compare(
    table("absent"),
    table("full", "no_valid_measurement", "")
  )[0];
  assert.equal(failed.bothValid, false);
  assert.equal(failed.horizontalDeltaMps, null);
  assert.equal(failed.fullStatus, "no_valid_measurement");
});
test("matrix comparison rejects unlike configurations and wrong conditions", () => {
  const changed = table("full");
  changed.rows[0][3] = "144";
  assert.throws(() => compare(table("absent"), changed));
  assert.throws(() => compare(table("full"), table("absent")));
});
test("accepted missing or non-finite velocities cannot become zero deltas", () => {
  for (const value of ["", null, "NaN", "Infinity"]) {
    assert.throws(() =>
      compare(table("absent"), table("full", "valid", value))
    );
  }
});
