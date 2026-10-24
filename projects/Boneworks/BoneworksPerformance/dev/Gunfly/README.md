# Gunfly experiments

Runtime sources: ../../src/Debug/Gunfly/{Shared,Matrix,Comparison}.
Offline analysis and the MelonMCP launcher live beside this file.
Tests remain in the repository's tests/Gunfly\* directories.

## Build

Build from the repository root:

```powershell
dotnet build projects/Boneworks/BoneworksPerformance/GunflyBenchmarks.csproj -c Debug
```

Output: BoneworksPerformance/bin/Gunfly/Debug/BoneworksPerformance.Gunfly.Debug.dll.
This helper is intentionally separate from the MelonMod. A true mod-off run must
not load BoneworksPerformance.dll at all. Loading this helper does not register
a MelonMod or start a test. Neither Debug nor Release of the normal mod includes
its sources. Building the helper as Release fails explicitly.
Do not put the helper in Mods. MelonMCP loads it through reflection.

Start a fresh game for EVERY independent batch and condition. Do not rebuild a
loaded DLL, hot-replace assemblies, or run both runners consecutively in one
process. IL2CPP injected observer types and spawned weapons survive managed
cleanup. Use only MelonMCP plus any required controller-role compatibility for
baseline, and add the Release performance mod for mod-on. Back up preferences
and other mods before changing the installed set. This launcher does not change
installed mods or launch/close the game.

Set this PowerShell variable from the repository root:

```powershell
$gunfly='projects/Boneworks/BoneworksPerformance/dev/Gunfly'
```

## Prepare the game

Prepare through MelonMCP, one command at a time:

```powershell
& "$gunfly/run-gunfly.ps1" Inspect
& "$gunfly/run-gunfly.ps1" LoadScene
```

Wait for BlankBox and the rig to load, then:

```powershell
& "$gunfly/run-gunfly.ps1" Prepare
```

If native controller indices are invalid, run:

```powershell
& "$gunfly/run-gunfly.ps1" Compatibility
```

Do this before loading the scene. It supplies the
legacy role-hint fallback without overriding pose/action data. Cleanup preserves
Harmony owner jakzo.boneworks.openvr-role-compatibility. SessionTools.Devices()
reports actual devices and cached controller indices.

## Scored matrix

Matched Update and FixedUpdate rates:

```powershell
& "$gunfly/run-gunfly.ps1" Matrix -Condition absent
& "$gunfly/run-gunfly.ps1" Matrix -Condition full
```

Run these in DIFFERENT game sessions. Each generates the original 320 cases
with the original IDs, eight rifle variants, 120/90/144/180 Hz and ten angles.
Optional -IncludeSmgs appends 80 cases for one native smg_MP5 and smg_MP5K.
Keep these expanded results separate from the historical 320-rifle result.

To run only reviewed retries or repeat controls:

```powershell
& "$gunfly/run-gunfly.ps1" Subset -Condition absent -Requests /absolute/path/subset.tsv
```

TSV fields, no header: unique safe ID, native prefab, Hz, angle from straight down.
Supported angles: 45,0,15,30,60,75,90,105,120,-15.
IDs allow letters, numbers, underscores and hyphens only.
No half-rate scored matrix, since acceptance requires one fixed tick per render.

## Mod-on/off comparison

Record flight, slot and magazine lag:

```powershell
& "$gunfly/run-gunfly.ps1" Comparison -Condition absent -Requests "$gunfly/requests/turning.tsv"
```

Repeat in fresh sessions with -Condition full and -Condition half.
half means Release mod on, rendering at 60 Hz and physics at 120 Hz from launch.
The supplied nine-case plan has three repeats each of straight, yaw-turn and
pitch-height in rotating order. Requests have seven TSV fields:
ID, prefab, Hz, angle 45, height 1.76, crouch -1, mode.
Native MP5/MP5K and the matrix rifles are supported. The retained turning
analysis is calibrated for 120 Hz and rejects other rates.
Yaw ramps at 18..20s to+45,22..24 to0,24..26 to-45,28..30 to0.
Pitch/height pulses use the same windows, angles 60 then 30, height 1.66 at peaks.
All other times use angle 45, height 1.76, yaw 0. Trials last 40 s, unscored.

## Protocol

Both runners inject base OpenVR pose/action APIs, not cached body inputs.
Height calibration 176 cm, floor 0, full standing. Right stick up 2 s, then down;
left backward sprint starts at 2 s. The native gun and matching mag are spawned,
glitched through native socket transitions and holstered on the right shoulder.
No cached weapon reuse across gun switches. Native collider geometry is never
edited. Teleport to origin and zero body velocities ONLY before each attempt.
The launch pad disappears at 10 s. Scene environment colliders/renderers are
disabled during the test, preserving the rig, gun and magazine colliders.
XR rendering is temporarily disabled. The physical display rate is not changed.
Synthetic wall-clock pacing does not guarantee achieved cadence. Logs determine
validity. These desktop tests do not benchmark compositor/headset presentation.

Matrix rules retained: five-second stable motion and contact, final clean
one-second matched-cadence window, exact pose/actions, <=1% average rate error.
Up to two attempts per case, uncrouching again on retry, timeout 60 s.
Failures have blank measurement fields, never invented zero speeds.
Runtime paths and raw rigidbody velocities remain alongside world displacement
velocities. Lateral drift is reported, not corrected.

Output: UserData/GunflyMeasurements/{matrix,turning-ab}-condition-timestamp.
Each run saves requests, protocol, progress.json, results-raw.csv, raw frame
logs, geometry and status. Comparison also saves slot-phases.csv and mod-state.
Fixed phases bracket RigManager code. fixed_exit is BEFORE the Unity solver,
not an image/display timestamp. Both OpenVR sampling threads query the same
pure stopwatch-based input schedule.

## Progress and cleanup

While active, poll `progress.json` through files only. MCP calls disturb timing.
Use Status only outside measurement or to diagnose a suspected stall.
Stop aborts the active runner and restores its saved state.
After completion or Stop:

```powershell
& "$gunfly/run-gunfly.ps1" Cleanup
```

Cleanup removes benchmark hooks/pads/observers and restores scene fixture roots.
Restore covers player calibration, original shoulder gun, timing, XR and
temporary in-memory mod preferences. Restart before another batch.

## Verify and export

Offline matrix verification from repository root:

```powershell
dotnet run --project tests/GunflyMatrixAnalysis -c Release -- RUN NEW-verified.json
```

For subsets or the expanded 400-case matrix, use:

```powershell
dotnet run --project tests/GunflyMatrixAnalysis -c Release -- --verify-subset RUN NEW-verified.json
```

This independently reproduces accepted medians and scans failed raw logs for
qualifying windows, preferring 2 s then 1 s. It never edits raw logs.

```powershell
dotnet run --project tests/GunflyMatrixAnalysis -c Release -- --retry-plan NEW-verified.json NEW-selection-directory
```

Review retry-selection.json and both raw attempts. The retained selection policy
is specific to the 320-rifle matrix, not proof that skipped configurations cannot
fly. It proposes at most two additional attempts for supported flaky failures.
After fresh subset runs and independent --verify-subset verification:

```powershell
dotnet run --project tests/GunflyMatrixAnalysis -c Release -- --merge-retries ORIGINAL-verified.json SELECTION/retry-selection.json NEW-merged.json RETRY-verified.json
```

The merge preserves original status, reasons, logs, selection and retry provenance.
Use NEW paths. Do not overwrite the original study.

Use Node.js for the remaining commands:

```powershell
node "$gunfly/analysis/export-table.mjs" VERIFIED-or-MERGED.json NEW-results.csv
node "$gunfly/analysis/compare-matrix.mjs" ABSENT-verified.json FULL-verified.json NEW-comparison.json
node "$gunfly/analysis/analyze-gunfly-turning.mjs" ABSENT-RUN NEW-absent.json
```

Repeat analysis for full and half runs, then:

```powershell
node "$gunfly/analysis/compare-gunfly-turning.mjs" NEW-absent.json NEW-full.json NEW-half.json NEW-comparison.json
```

Retained slot/trajectory utilities support analysis tests and older recordings.
Do not equate one similar speed measurement with reliability or trajectory
equivalence. Inspect cadence, all failed launches, repeat spread, full paths,
local magazine geometry and phase-specific slot movement.

## Tests

```powershell
dotnet run --project tests/GunflyMatrix -c Release
dotnet run --project tests/GunflyTurning/ScheduleTests.csproj -c Release
node --test tests/Gunfly*Analysis.test.mjs tests/GunflyExports.test.mjs
```

Superseded Gunfly\* helper projects, numbered scratch snippets and strategy search
forks were removed. Historical investigations and all recorded outputs remain.
The separate pre-existing profiler/Runoff debug tools were not changed.
