import fs from "node:fs/promises";
import path from "node:path";
import { readCsv } from "./analyze-gunfly-slot-ab.mjs";
const [root, output] = process.argv.slice(2);
if (!root || !output)
  throw Error("Supply a completed comparison run and a NEW output JSON path");
const requests = (
  await fs.readFile(path.join(root, "retry-requests.tsv"), "utf8")
)
  .trim()
  .split(/\r?\n/)
  .map((l) => l.split("\t"));
const results = [];
for (const [id, gun, hzText, angle] of requests) {
  const hz = Number(hzText),
    log = path.join(
      root,
      `${id}-${gun}-${hz}Hz-${angle}deg`,
      "attempt-01-frames.csv"
    );
  if (!Number.isFinite(hz) || hz <= 0) throw Error("Invalid physics rate");
  const rows = await readCsv(log);
  let checked = 0,
    maxError = 0,
    invalid = 0;
  for (let i = 0; i < rows.length; i++) {
    const r = rows[i];
    if (!r.velocity_valid) {
      if (["x", "y", "z"].some((a) => r[`foot_v${a}_game_m_s`] !== null))
        invalid++;
      continue;
    }
    let start = i - 1;
    while (
      start > 0 &&
      r.fixed_tick - rows[start].fixed_tick < Math.max(5, Math.floor(hz / 10))
    )
      start--;
    if (start < 0) throw Error("Valid velocity without previous row");
    const p = rows[start],
      dt = (r.fixed_tick - p.fixed_tick) / hz;
    if (!Number.isFinite(dt) || dt <= 0)
      throw Error("Invalid foot velocity interval");
    for (const a of ["x", "y", "z"]) {
      const expected = (r[`foot_${a}`] - p[`foot_${a}`]) / dt,
        actual = r[`foot_v${a}_game_m_s`];
      if (!Number.isFinite(expected) || !Number.isFinite(actual))
        throw Error(`Missing valid foot velocity ${id} row ${i}`);
      maxError = Math.max(maxError, Math.abs(expected - actual));
      checked++;
    }
  }
  if (maxError > 1e-9 || invalid)
    throw Error(`Foot velocity audit failed ${id}: ${maxError}, ${invalid}`);
  results.push({
    id,
    log,
    checkedComponents: checked,
    maxAbsoluteError: maxError,
    invalidRowsWithVelocity: invalid,
  });
}
await fs.writeFile(
  output,
  JSON.stringify(
    {
      root,
      method:
        "Independently recomputed from foot positions and integer fixed-tick span; same declared 0.1-physics-second lookback, integer Hz/10 division.",
      results,
    },
    null,
    2
  ),
  { flag: "wx" }
);
console.log(
  JSON.stringify({
    cases: results.length,
    components: results.reduce((s, r) => s + r.checkedComponents, 0),
    maxError: Math.max(...results.map((r) => r.maxAbsoluteError)),
  })
);
