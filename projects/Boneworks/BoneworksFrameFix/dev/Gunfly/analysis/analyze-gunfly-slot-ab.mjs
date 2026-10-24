// Shared slot-pose math and numeric CSV parsing for the comparison recorder.
import fs from "node:fs/promises";
const dot = (a, b) => a.reduce((s, v, i) => s + v * b[i], 0),
  sub = (a, b) => a.map((v, i) => v - b[i]),
  add = (a, b) => a.map((v, i) => v + b[i]),
  scale = (a, s) => a.map((v) => v * s),
  norm = (a) => Math.hypot(...a);
const vec = (r, p) => ["x", "y", "z"].map((a) => r[p + "_" + a]);
const quat = (r, p) => ["x", "y", "z", "w"].map((a) => r[p + "_q" + a]);
export function rotate(q, v) {
  const u = q.slice(0, 3),
    w = q[3],
    uv = dot(u, v),
    uu = dot(u, u),
    cross = [
      u[1] * v[2] - u[2] * v[1],
      u[2] * v[0] - u[0] * v[2],
      u[0] * v[1] - u[1] * v[0],
    ];
  return add(add(scale(u, 2 * uv), scale(v, w * w - uu)), scale(cross, 2 * w));
}
export function chestAnchorError(current, late) {
  return sub(
    vec(current, "host"),
    add(
      vec(current, "chest"),
      rotate(quat(current, "chest"), vec(late, "host_chest"))
    )
  );
}
export function projectedLagSeconds(error, velocity) {
  const speed2 = dot(velocity, velocity);
  return speed2 > 1 ? -dot(error, velocity) / speed2 : null;
}
const quantile = (a, q) => {
  const s = a.filter(Number.isFinite).sort((x, y) => x - y);
  if (!s.length) return null;
  const p = (s.length - 1) * q,
    i = Math.floor(p);
  return s[i] + (s[Math.min(i + 1, s.length - 1)] - s[i]) * (p - i);
};
const stats = (a) => {
  const valid = a.filter(Number.isFinite);
  return {
    count: valid.length,
    median: quantile(valid, 0.5),
    p05: quantile(valid, 0.05),
    p95: quantile(valid, 0.95),
    maximum: valid.length ? Math.max(...valid) : null,
  };
};
export function summarizePhases(rows, hz, lagStart = 30) {
  let late = null,
    enter = null,
    previousEnter = null,
    lateVelocity = null;
  const measures = [],
    lateMeasures = [];
  for (const r of rows) {
    if (r.phase.startsWith("late_")) {
      if (r.phase === "late_enter_before_restore" && enter)
        lateVelocity = scale(sub(vec(r, "chest"), vec(enter, "chest")), hz);
      if (late) {
        const error = chestAnchorError(r, late),
          lag = lateVelocity ? projectedLagSeconds(error, lateVelocity) : null;
        lateMeasures.push({
          time: r.elapsed_s,
          phase: r.phase,
          anchorErrorM: norm(error),
          lagMs: lag === null ? null : lag * 1000,
        });
      }
      if (r.phase === "late_exit") late = r;
      continue;
    }
    if (r.phase === "fixed_enter_before_mod") {
      previousEnter = enter;
      enter = r;
      continue;
    }
    if (
      r.phase !== "fixed_before_native_after_mod" ||
      !enter ||
      r.physics_tick !== enter.physics_tick ||
      !late ||
      late.unity_frame >= r.unity_frame
    )
      continue;
    const error = chestAnchorError(r, late),
      elapsedTicks = previousEnter
        ? r.physics_tick - previousEnter.physics_tick
        : 0;
    const velocity =
      elapsedTicks > 0
        ? scale(
            sub(vec(enter, "chest"), vec(previousEnter, "chest")),
            hz / elapsedTicks
          )
        : null;
    const lag = velocity ? projectedLagSeconds(error, velocity) : null;
    measures.push({
      time: r.elapsed_s,
      tick: r.physics_tick,
      tickInFrame: r.tick_in_frame,
      hookHostShiftM: norm(sub(vec(r, "host"), vec(enter, "host"))),
      hookMagShiftM: norm(sub(vec(r, "mag"), vec(enter, "mag"))),
      anchorErrorM: norm(error),
      anchorError: error,
      projectedLagMs: lag === null ? null : lag * 1000,
      localMag: vec(r, "mag_host"),
      localHost: vec(r, "host_local"),
    });
  }
  const groups = {};
  for (const [key, predicate] of Object.entries({
    matched: (r) => r.time >= 20 && r.time < 29,
    lagFirst: (r) => r.time >= lagStart + 1 && r.tickInFrame === 1,
    lagExtra: (r) => r.time >= lagStart + 1 && r.tickInFrame > 1,
  })) {
    const selected = measures.filter(predicate);
    groups[key] = {
      rows: selected.length,
      hookHostShiftM: stats(selected.map((r) => r.hookHostShiftM)),
      hookMagShiftM: stats(selected.map((r) => r.hookMagShiftM)),
      anchorErrorM: stats(selected.map((r) => r.anchorErrorM)),
      projectedLagMs: stats(selected.map((r) => r.projectedLagMs)),
      magLocalMedian: ["x", "y", "z"].map((_, i) =>
        quantile(
          selected.map((r) => r.localMag[i]),
          0.5
        )
      ),
    };
  }
  const lateGroups = {};
  for (const phase of [
    "late_enter_before_restore",
    "late_before_native_after_restore",
    "late_exit",
  ])
    for (const regime of ["matched", "lag"]) {
      const selected = lateMeasures.filter(
        (r) =>
          r.phase === phase &&
          (regime === "matched"
            ? r.time >= 20 && r.time < 29
            : r.time >= lagStart + 1)
      );
      lateGroups[regime + "_" + phase] = {
        rows: selected.length,
        anchorErrorM: stats(selected.map((r) => r.anchorErrorM)),
        projectedLagMs: stats(selected.map((r) => r.lagMs)),
      };
    }
  return {
    groups,
    lateGroups,
    warning:
      "Lag is a geometric projection onto current chest velocity, relative to the chest-local host anchor recorded at previous LateUpdate. It is not a direct runtime timestamp. Native skin/bone interpolation can affect it. Direct same-tick hook displacement is recorded separately.",
  };
}
export async function readCsv(file) {
  const [header, ...lines] = (await fs.readFile(file, "utf8"))
      .trim()
      .split(/\r?\n/),
    keys = header.split(",");
  return lines.map((line, n) => {
    const values = line.split(",");
    if (values.length !== keys.length)
      throw Error(`Field count mismatch ${file}:${n + 2}`);
    return Object.fromEntries(
      values.map((value, index) => {
        if (keys[index] === "phase") return [keys[index], value];
        if (value === "") return [keys[index], null];
        const number = Number(value);
        if (!Number.isFinite(number))
          throw Error(`Invalid numeric field ${file}:${n + 2} ${keys[index]}`);
        return [keys[index], number];
      })
    );
  });
}
