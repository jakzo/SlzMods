import fs from "node:fs/promises";
import { pathToFileURL } from "node:url";
export function compare(absent, full) {
  const decode = (t) =>
    t.rows.map((row) =>
      Object.fromEntries(t.headers.map((h, i) => [h, row[i]]))
    );
  const baseline = decode(absent),
    other = new Map(decode(full).map((r) => [r.case_id, r]));
  if (
    other.size !== full.rows.length ||
    baseline.length !== other.size ||
    new Set(baseline.map((r) => r.case_id)).size !== baseline.length
  )
    throw Error("Duplicate or unmatched case IDs");
  return baseline.map((a) => {
    const b = other.get(a.case_id);
    if (
      !b ||
      [
        "rifle_prefab",
        "requested_hz",
        "look_angle_from_down_deg",
        "hmd_height_m",
        "player_height_m",
      ].some((k) => a[k] !== b[k])
    )
      throw Error("Configuration mismatch " + a.case_id);
    if (
      !["absent", "baseline_without_performance_mod"].includes(a.condition) ||
      b.condition !== "full"
    )
      throw Error("Expected absent and full conditions");
    const bothValid = a.status === "valid" && b.status === "valid";
    const delta = (key) => {
      if (!bothValid) return null;
      if (
        [a[key], b[key]].some(
          (value) =>
            value === null ||
            value === undefined ||
            String(value).trim() === "" ||
            !Number.isFinite(Number(value))
        )
      ) {
        throw Error("Invalid accepted velocity for " + a.case_id + ": " + key);
      }
      return Number(b[key]) - Number(a[key]);
    };
    return {
      case_id: a.case_id,
      gun: a.rifle_prefab,
      hz: a.requested_hz,
      angle: a.look_angle_from_down_deg,
      absentStatus: a.status,
      fullStatus: b.status,
      bothValid,
      absentReason: a.reason,
      fullReason: b.reason,
      horizontalDeltaMps: delta("world_horizontal_median_m_s"),
      verticalDeltaMps: delta("world_vertical_median_m_s"),
      vxDeltaMps: delta("world_vx_median_m_s"),
      vzDeltaMps: delta("world_vz_median_m_s"),
      absentFrameLog: a.frame_log,
      fullFrameLog: b.frame_log,
    };
  });
}
if (
  process.argv[1] &&
  import.meta.url === pathToFileURL(process.argv[1]).href
) {
  const [a, b, output] = process.argv.slice(2);
  if (!output)
    throw Error(
      "Supply absent verified JSON, full verified JSON, NEW output JSON"
    );
  const absent = JSON.parse(await fs.readFile(a, "utf8")),
    full = JSON.parse(await fs.readFile(b, "utf8"));
  await fs.writeFile(
    output,
    JSON.stringify(
      {
        absentRun: absent.runDirectory,
        fullRun: full.runDirectory,
        limits:
          "One qualified result per configuration is descriptive, not a reliability or equivalence proof. Repeat the same subset in fresh sessions to measure spread. Failures and lateral velocity retained.",
        results: compare(absent, full),
      },
      null,
      2
    ),
    { flag: "wx" }
  );
}
