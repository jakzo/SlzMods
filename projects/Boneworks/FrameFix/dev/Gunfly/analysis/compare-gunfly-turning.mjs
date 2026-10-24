import fs from "node:fs/promises";
import { compareCurves, median } from "./analyze-gunfly-turning.mjs";
const [absentFile, fullFile, halfFile, output] = process.argv.slice(2);
if (!output)
  throw Error(
    "Supply absent, full, half analysis JSON and separate comparison output"
  );
const inputs = await Promise.all(
  [absentFile, fullFile, halfFile].map(async (file) => ({
    file,
    data: JSON.parse(await fs.readFile(file, "utf8")),
  }))
);
if (
  inputs.some(
    (r) =>
      r.data.partial ||
      r.data.progress.active ||
      r.data.progress.completed !== 9
  )
)
  throw Error("Incomplete turning batch");
const conditions = ["absent", "full", "half"];
let referenceGun;
for (const [index, { data }] of inputs.entries()) {
  if (
    data.results.length !== 9 ||
    new Set(data.results.map((row) => row.id)).size !== 9
  ) {
    throw Error("Expected nine unique turning trials");
  }
  for (const row of data.results) {
    if (!row.modState.startsWith("condition=" + conditions[index] + " ")) {
      throw Error(
        "Turning input files do not match absent/full/half conditions"
      );
    }
    const gun = row.geometryStart.match(/^prefab=(\S+)/)?.[1];
    if (!gun) throw Error("Missing gun prefab in geometry");
    referenceGun ??= gun;
    if (gun !== referenceGun) throw Error("Compare one gun at a time");
  }
  for (const mode of ["straight", "yaw-turn", "pitch-height"]) {
    if (data.results.filter((row) => row.mode === mode).length !== 3) {
      throw Error("Expected three trials for each turning scenario");
    }
  }
}
const summarize = (values) => {
  const v = values.filter(Number.isFinite);
  return {
    n: v.length,
    median: median(v),
    minimum: v.length ? Math.min(...v) : null,
    maximum: v.length ? Math.max(...v) : null,
  };
};
const result = {
  sources: inputs.map((r) => ({ analysis: r.file, raw: r.data.root })),
  modes: [],
  limits: [
    "Controlled single-gun sequences at 120 physics Hz, three trials per scenario per condition. This is not proof for other guns, rates or arbitrary input motions.",
    "Numerical equality of whole trajectories is not expected even between vanilla repeats. Paired curves are compared alongside native-to-native spread.",
    "Pose ages are inferred from known constant-rate scripted ramps at a named callback phase. They are not timestamps attached by a real headset.",
    "Fixed-exit means after native RigManager fixed processing and before the Unity solver step, not the pose seen on the display.",
    "Host/chest anchor error uses the previous LateUpdate relative anchor. It must not be described as an unconditional rendered-frame delay.",
    "All measured trajectories and failing input/cadence checks remain in the raw data. No arbitrary time shifting or rotation alignment is applied to make curves match.",
  ],
};
const geometry = inputs.flatMap(({ data }) =>
  data.results.flatMap((r) => [r.geometryStart, r.geometryEnd])
);
const colliderNumbers = (text) =>
  (
    text
      .split(/\r?\n/)
      .filter((l) => l.startsWith("collider=") || l.startsWith("box_center="))
      .join("\n")
      .match(/[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[Ee][-+]?\d+)?/g) ?? []
  ).map(Number);
const magPoint = (text) => {
  const match = text
    .replaceAll("\\n", "\n")
    .match(/(?:^|\n)mag_in_host=([^\r\n]+)/);
  if (!match) throw Error("Missing magazine geometry");
  const point = match[1].split(",").map(Number);
  if (point.length !== 3 || !point.every(Number.isFinite))
    throw Error("Invalid magazine geometry");
  return point;
};
const nativeReference = colliderNumbers(geometry[0]),
  magReference = magPoint(geometry[0]);
if (!nativeReference.length) throw Error("Missing native collider geometry");
result.geometry = {
  snapshots: geometry.length,
  nativeColliderNumericCountsAgree: geometry.every(
    (t) => colliderNumbers(t).length === nativeReference.length
  ),
  maximumColliderNumericDifference: Math.max(
    ...geometry.map((t) =>
      Math.max(
        ...colliderNumbers(t).map((v, k) => Math.abs(v - nativeReference[k]))
      )
    )
  ),
  maximumMagInHostDifferenceM: Math.max(
    ...geometry.map((t) =>
      Math.hypot(...magPoint(t).map((v, k) => v - magReference[k]))
    )
  ),
};
for (const mode of ["straight", "yaw-turn", "pitch-height"]) {
  const groups = inputs.map(({ data }) =>
    data.results.filter((r) => r.mode === mode)
  );
  const item = {
    mode,
    conditions: {},
    pairedAgainstVanilla: {},
    nativeRepeatability: inputs[0].data.withinConditionRepeatability.filter(
      (r) => r.mode === mode
    ),
  };
  for (let n = 0; n < groups.length; n++) {
    const name = ["vanilla120", "mod120", "mod60"][n],
      rows = groups[n];
    item.conditions[name] = {
      ids: rows.map((r) => r.id),
      preVelocity: [0, 1, 2].map((k) =>
        summarize(rows.map((r) => r.segments.pre.footVelocityReal[k]))
      ),
      turnVelocity: [0, 1, 2].map((k) =>
        summarize(rows.map((r) => r.segments.turn.footVelocityReal[k]))
      ),
      postVelocity: [0, 1, 2].map((k) =>
        summarize(rows.map((r) => r.segments.post.footVelocityReal[k]))
      ),
      turnPhysicsHz: summarize(rows.map((r) => r.segments.turn.physicsHz)),
      turnRenderHz: summarize(rows.map((r) => r.segments.turn.renderHz)),
      turnPacingFraction: summarize(
        rows.map((r) => r.segments.turn.perFramePacingFraction)
      ),
      fixedExitYawAgeMs: summarize(
        rows.map((r) => r.input.fixed_exit.yawAgeMs.median)
      ),
      fixedExitPitchAgeMs: summarize(
        rows.map((r) => r.input.fixed_exit.pitchAgeMs.median)
      ),
      fixedExitHeightAgeMs: summarize(
        rows.map((r) => r.input.fixed_exit.heightAgeMs.median)
      ),
      firstTickYawStepRatio: summarize(
        rows.map((r) => r.inputSteps.first.yawStepRatio.median)
      ),
      extraTickYawStepRatio: summarize(
        rows.map((r) => r.inputSteps.extra.yawStepRatio.median)
      ),
      firstTickPitchStepRatio: summarize(
        rows.map((r) => r.inputSteps.first.pitchStepRatio.median)
      ),
      extraTickPitchStepRatio: summarize(
        rows.map((r) => r.inputSteps.extra.pitchStepRatio.median)
      ),
      firstTickHeightStepRatio: summarize(
        rows.map((r) => r.inputSteps.first.heightStepRatio.median)
      ),
      extraTickHeightStepRatio: summarize(
        rows.map((r) => r.inputSteps.extra.heightStepRatio.median)
      ),
      slotHookShiftTurnM: summarize(
        rows.map((r) => r.slot.turn.hookShiftM.maxAbs)
      ),
      slotHookRotationTurnDeg: summarize(
        rows.map((r) => r.slot.turn.hookRotationDeg.maxAbs)
      ),
      firstTickAnchorLagMs: summarize(
        rows.map((r) => r.slot.turnFirst.projectedLagMs.median)
      ),
      extraTickAnchorLagMs: summarize(
        rows.map((r) => r.slot.turnExtra.projectedLagMs.median)
      ),
      beforeLateRestoreAnchorErrorM: summarize(
        rows.map(
          (r) => r.lateSlot.late_enter_before_restore.chestAnchorErrorM.p95
        )
      ),
      afterLateRestoreAnchorErrorM: summarize(
        rows.map(
          (r) =>
            r.lateSlot.late_before_native_after_restore.chestAnchorErrorM.p95
        )
      ),
      queryErrors: rows.map((r) => ({ id: r.id, state: r.modState })),
    };
  }
  for (let n = 1; n < groups.length; n++)
    item.pairedAgainstVanilla[["mod120", "mod60"][n - 1]] = groups[n].map(
      (r) => {
        const reference = groups[0].find((a) => a.id === r.id);
        if (!reference) throw Error("Missing paired trial");
        return {
          id: r.id,
          turn: compareCurves(reference.curve, r.curve, 18, 30),
          post: compareCurves(reference.curve, r.curve, 32, 39),
        };
      }
    );
  result.modes.push(item);
}
await fs.writeFile(output, JSON.stringify(result, null, 2), { flag: "wx" });
for (const m of result.modes) {
  console.log(m.mode);
  for (const [condition, c] of Object.entries(m.conditions))
    console.log(
      condition,
      JSON.stringify({
        turnHz: c.turnPhysicsHz,
        postVelocity: c.postVelocity.map((v) => v.median),
        yawAge: c.fixedExitYawAgeMs.median,
        heightAge: c.fixedExitHeightAgeMs.median,
        slotShiftMax: c.slotHookShiftTurnM,
      })
    );
}
