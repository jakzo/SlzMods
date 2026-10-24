// Pure trajectory and clean-window metrics. No old strategy-search runner.

const quantile = (values, q) => {
  const a = values.filter(Number.isFinite).sort((x, y) => x - y);
  if (!a.length) return null;
  const p = (a.length - 1) * q,
    i = Math.floor(p);
  return a[i] + (a[Math.min(i + 1, a.length - 1)] - a[i]) * (p - i);
};
const stats = (values) => ({
  max: Math.max(...values.map(Math.abs)),
  rms: Math.sqrt(values.reduce((s, v) => s + v * v, 0) / values.length),
});
export function summarizeFromRest(rows, origin, slope = 0, target = 500) {
  if (
    !rows.length ||
    !["x", "y", "z", "game_time"].every((k) => Number.isFinite(origin[k]))
  )
    throw Error("Missing prelaunch origin or game clock");
  for (let i = 0; i < rows.length; i++) {
    if (
      !["elapsed_s", "foot_x", "foot_y", "foot_z", "game_time"].every((k) =>
        Number.isFinite(rows[i][k])
      )
    )
      throw Error("Missing from-rest trajectory field");
    if (
      rows[i].elapsed_s < 0 ||
      (i && rows[i].elapsed_s <= rows[i - 1].elapsed_s)
    )
      throw Error("Non-monotonic from-rest time");
  }
  const pathRows = [
    {
      elapsed_s: 0,
      foot_x: origin.x,
      foot_y: origin.y,
      foot_z: origin.z,
      game_time: origin.game_time,
    },
  ];
  let previous = pathRows[0],
    crossing = null;
  for (const row of rows) {
    const progress = origin.z - row.foot_z;
    if (progress >= target) {
      const previousProgress = origin.z - previous.foot_z;
      const fraction =
        (target - previousProgress) / (progress - previousProgress);
      crossing = Object.fromEntries(
        ["elapsed_s", "foot_x", "foot_y", "foot_z", "game_time"].map((k) => [
          k,
          previous[k] + fraction * (row[k] - previous[k]),
        ])
      );
      pathRows.push(crossing);
      break;
    }
    pathRows.push(row);
    previous = row;
  }
  const route = pathRows.map(
      (r) => r.foot_y - origin.y + slope * (r.foot_z - origin.z)
    ),
    cross = pathRows.map((r) => r.foot_x - origin.x);
  return {
    targetHorizontalMetres: target,
    reachedTarget: !!crossing,
    realSeconds: crossing?.elapsed_s ?? null,
    gameSeconds: crossing ? crossing.game_time - origin.game_time : null,
    progressMetres: origin.z - pathRows.at(-1).foot_z,
    routeSlope: slope,
    altitudeOrSlopeError: stats(route),
    crossTrack: stats(cross),
    pathWithinHalfMeter: stats(route).max <= 0.5 && stats(cross).max <= 0.5,
    sampledPathOnly: true,
    linearCrossingInterpolation: !!crossing,
    warning:
      "Path and elapsed-time metrics only. Verify native rest evidence, input fidelity, achieved cadence and stable measurement windows separately. A recovered endpoint does not erase an earlier violation.",
  };
}
// Development-window admission only. Full-route constraints and independent
// finalist trials remain separate, even when a late window is clean and stable.
export function findCleanWindows(rows, hz, start = 10) {
  let count = 0,
    latest = null,
    nextCheck = start + 5;
  for (let end = 1; end < rows.length; end++) {
    const last = rows[end];
    if (last.elapsed_s < nextCheck) continue;
    nextCheck = last.elapsed_s + 0.25;
    let first = end;
    while (first > 0 && last.elapsed_s - rows[first].elapsed_s < 1) first--;
    let stableFirst = first;
    while (stableFirst > 0 && last.elapsed_s - rows[stableFirst].elapsed_s < 5)
      stableFirst--;
    if (
      rows[stableFirst].elapsed_s < start ||
      last.elapsed_s - rows[stableFirst].elapsed_s < 4.975
    )
      continue;
    const seconds = last.elapsed_s - rows[first].elapsed_s;
    const fps = (end - first) / seconds,
      physicsHz = (last.fixed_tick - rows[first].fixed_tick) / seconds;
    if (Math.abs(fps / hz - 1) > 0.01 || Math.abs(physicsHz / hz - 1) > 0.01)
      continue;
    let valid = true;
    for (let i = first + 1; i <= end; i++) {
      const r = rows[i],
        p = rows[i - 1];
      if (
        r.fixed_tick - p.fixed_tick !== 1 ||
        r.render_dt_s < 0.75 / hz ||
        r.render_dt_s > 1.25 / hz ||
        r.velocity_valid !== 1 ||
        ![
          r.world_vx_m_s,
          r.world_vy_m_s,
          r.world_vz_m_s,
          r.raw_hmd_y_m,
          p.command_height,
          r.look_angle_from_down_deg,
          p.command_angle,
          r.left_x,
          r.left_y,
          r.right_x,
          r.right_y,
        ].every(Number.isFinite) ||
        Math.abs(r.raw_hmd_y_m - p.command_height) > 0.0001 ||
        Math.abs(r.look_angle_from_down_deg - p.command_angle) > 0.1 ||
        Math.abs(r.left_x) > 0.00001 ||
        Math.abs(r.left_y + 1) > 0.00001 ||
        Math.abs(r.right_x) > 0.00001 ||
        Math.abs(r.right_y + 1) > 0.00001 ||
        r.sprint_held !== 1 ||
        r.native_jog !== 1
      ) {
        valid = false;
        break;
      }
    }
    if (!valid) continue;
    const stable = rows
      .slice(stableFirst, end + 1)
      .filter((r) => r.velocity_valid === 1);
    if (
      stable.length < hz * 4 ||
      stable.filter((r) => r.magazine_contact === 1).length < 5
    )
      continue;
    const h = stable.map((r) => Math.hypot(r.world_vx_m_s, r.world_vz_m_s)),
      v = stable.map((r) => r.world_vy_m_s),
      x = stable.map((r) => r.world_vx_m_s);
    if (![...h, ...v, ...x].every(Number.isFinite)) continue;
    const hm = quantile(h, 0.5),
      vm = quantile(v, 0.5),
      spread = (a) => quantile(a, 0.95) - quantile(a, 0.05),
      drift = (a) =>
        Math.abs(
          quantile(a.slice(0, a.length >> 1), 0.5) -
            quantile(a.slice(a.length >> 1), 0.5)
        );
    if (
      spread(h) > Math.max(0.35, hm * 0.025) ||
      drift(h) > Math.max(0.12, hm * 0.005) ||
      spread(v) > Math.max(0.4, Math.abs(vm) * 0.025) ||
      drift(v) > Math.max(0.15, Math.abs(vm) * 0.0075) ||
      spread(x) > Math.max(0.35, hm * 0.02) ||
      (hm < 0.5 && Math.abs(vm) < 0.5)
    )
      continue;
    const a = rows[first],
      measurement = rows.slice(first + 1, end + 1);
    count++;
    latest = {
      startElapsed: a.elapsed_s,
      endElapsed: last.elapsed_s,
      seconds,
      renderFps: fps,
      physicsHz,
      gameMedianWindowSeconds: seconds,
      stabilityWindowSeconds: last.elapsed_s - rows[stableFirst].elapsed_s,
      footVelocityReal: [
        (last.foot_x - a.foot_x) / seconds,
        (last.foot_y - a.foot_y) / seconds,
        (last.foot_z - a.foot_z) / seconds,
      ],
      worldVelocityGameMedian: [
        quantile(
          measurement.map((r) => r.world_vx_m_s),
          0.5
        ),
        quantile(
          measurement.map((r) => r.world_vy_m_s),
          0.5
        ),
        quantile(
          measurement.map((r) => r.world_vz_m_s),
          0.5
        ),
      ],
    };
  }
  return {
    candidateCount: count,
    latest,
    warning:
      "Late stable/cadence window only, not full-route or from-rest qualification. Latest qualifying window, not fastest cherry-picked window.",
  };
}
export function summarize(rows, hz, slope = 0, start = 10) {
  const selected = rows.filter((r) => r.elapsed_s >= start);
  if (selected.length < 2) throw Error("Missing trajectory samples");
  for (let i = 0; i < selected.length; i++) {
    const r = selected[i];
    if (
      !["elapsed_s", "fixed_tick", "foot_x", "foot_y", "foot_z"].every((k) =>
        Number.isFinite(r[k])
      )
    )
      throw Error("Missing numeric trajectory field");
    if (i && r.elapsed_s <= selected[i - 1].elapsed_s)
      throw Error("Non-monotonic time");
  }
  const first = selected[0],
    last = selected.at(-1),
    duration = last.elapsed_s - first.elapsed_s;
  const terminal = selected.filter((r) => r.elapsed_s >= last.elapsed_s - 10),
    a = terminal[0],
    seconds = last.elapsed_s - a.elapsed_s;
  const route = selected.map(
    (r) => r.foot_y - first.foot_y + slope * (r.foot_z - first.foot_z)
  );
  const lateral = selected.map((r) => r.foot_x - first.foot_x);
  const terminalRoute = terminal.map(
    (r) => r.foot_y - first.foot_y + slope * (r.foot_z - first.foot_z)
  );
  let clean = 0;
  for (let i = 1; i < terminal.length; i++)
    if (
      terminal[i].fixed_tick - terminal[i - 1].fixed_tick === 1 &&
      terminal[i].render_dt_s >= 0.75 / hz &&
      terminal[i].render_dt_s <= 1.25 / hz
    )
      clean++;
  const heightErrors = [],
    angleErrors = [],
    yawErrors = [];
  for (let i = 1; i < selected.length; i++) {
    const r = selected[i],
      previous = selected[i - 1];
    if (r.elapsed_s < 11) continue;
    if (Number.isFinite(previous.command_height))
      heightErrors.push(r.raw_hmd_y_m - previous.command_height);
    if (Number.isFinite(previous.command_angle))
      angleErrors.push(r.look_angle_from_down_deg - previous.command_angle);
    if (Number.isFinite(previous.command_yaw)) {
      const actual = (Math.atan2(r.head_fx, r.head_fz) * 180) / Math.PI;
      yawErrors.push(((actual - previous.command_yaw + 540) % 360) - 180);
    }
  }
  return {
    diagnosticOnly: true,
    referenceElapsed: first.elapsed_s,
    routeSlope: slope,
    seconds: duration,
    routeError: stats(route),
    crossTrack: stats(lateral),
    withinHalfMeterCruisePath:
      stats(route).max <= 0.5 && stats(lateral).max <= 0.5,
    terminalRouteError: stats(terminalRoute),
    terminalRealSeconds: seconds,
    terminalFootVelocityReal: [
      (last.foot_x - a.foot_x) / seconds,
      (last.foot_y - a.foot_y) / seconds,
      (last.foot_z - a.foot_z) / seconds,
    ],
    terminalPhysicsHz: (last.fixed_tick - a.fixed_tick) / seconds,
    terminalRenderFps: (terminal.length - 1) / seconds,
    terminalCleanFraction: clean / (terminal.length - 1),
    terminalGameToRealRatio:
      Number.isFinite(last.game_time) && Number.isFinite(a.game_time)
        ? (last.game_time - a.game_time) / seconds
        : null,
    terminalContactFraction:
      terminal.filter((r) => r.magazine_contact === 1).length / terminal.length,
    heightRange: [
      quantile(
        selected.map((r) => r.raw_hmd_y_m),
        0
      ),
      quantile(
        selected.map((r) => r.raw_hmd_y_m),
        1
      ),
    ],
    angleRange: [
      quantile(
        selected.map((r) => r.look_angle_from_down_deg),
        0
      ),
      quantile(
        selected.map((r) => r.look_angle_from_down_deg),
        1
      ),
    ],
    priorCommandHeightError: heightErrors.length ? stats(heightErrors) : null,
    priorCommandAngleError: angleErrors.length ? stats(angleErrors) : null,
    priorCommandYawError: yawErrors.length ? stats(yawErrors) : null,
  };
}
