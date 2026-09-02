# Contributing to BoneworksPerformance

This document covers development builds, profiling, generated reports, and the
automated experiments used to investigate BONEWORKS performance. User-facing
installation and settings belong in [README.md](README.md).

## Build and test

Build the release mod with:

```powershell
dotnet build projects/Boneworks/BoneworksPerformance/BoneworksPerformance.csproj -c Release
```

Build the profiler and automated test tools with:

```powershell
dotnet build projects/Boneworks/BoneworksPerformance/BoneworksPerformance.csproj -c Debug
```

The local project configuration copies the resulting DLL into the BONEWORKS
`Mods` directory. Release builds omit the profiler implementation. Run the
pose-history tests with:

```powershell
dotnet run --project tests/BoneworksPerformance.Tests/BoneworksPerformance.Tests.csproj -c Release
```

Keep the profiling mod isolated while collecting baselines. Other mods can add
CPU work, allocations, exceptions, Harmony patches, and Unity objects that make
the profile unrepresentative.

## Debug profiler controls

Press the right thumbstick to start or stop a capture. A red indicator and
MelonLoader log message show when recording is active. Other mods can call:

```csharp
Sst.BoneworksPerformance.Mod.StartProfiling();
Sst.BoneworksPerformance.Mod.StopProfiling();
Sst.BoneworksPerformance.Mod.ToggleProfiling();
```

Each capture gets its own directory under
`UserData/BoneworksPerformance/profiles`. Captures do not overwrite one another.
The directory contains CSV files, folded stacks, metadata, and a self-contained
`report.html`.

The HTML report separates main-thread and worker scales and supports
click-to-zoom through native and IL2CPP stacks. Generated IL2CPP methods and
their native entry points come from runtime metadata. An optional IL2CppDumper
`script.json` supplies names for shared generics and stripped methods that are
missing from generated wrappers. If a matching UnityPlayer PDB is beside
`UnityPlayer.dll`, DbgHelp resolves native engine frames. Symbol loading and
stack resolution run outside the capture loop.

## Capture modes

`MetricsOnly` records frames and PlayerLoop data without CPU sampling. It is the
lowest-overhead option and the correct mode for A/B performance comparisons.

`Basic` samples the Unity main thread at 200 Hz and reports leaf instructions.

`Detailed` samples the main thread at 250 Hz with native stack unwinding and
records up to 32 native frames per sample. It samples one round-robin worker
stack on ordinary ticks. During a long instrumented completion wait, it takes
one bounded snapshot of up to three workers. A separate below-normal-priority
thread reads GPU-engine counters at 10 Hz and is excluded from CPU sampling.

Continuous sampling has a measurable fixed cost. `metadata.txt` records average
profiler work, average thread suspension, and the worst observed suspension.
`frames.csv` records process CPU time and the profiler's Update, FixedUpdate,
GUI, and frame-capture costs. Reject captures where this overhead is large
enough to change the workload being measured.

## Frame and PlayerLoop data

Every sampled stack records the PlayerLoop phase active at that instant.
Timestamp probes wrap Unity's top-level loop entries and direct FixedUpdate
subsystems.

- `frames.csv` records rendered-frame time, fixed-tick count, simulation debt,
  heap state, GC counters, process CPU time, and profiler self-cost.
- `player-loop-phases.csv` contains exact per-frame phase timings.
- `stalls.csv` isolates frames below 90 FPS and correlates their longest exact
  phase with GC activity, heap state, and profiler overhead.
- `cpu-samples.csv` preserves exact native addresses and the grouped display
  stacks used by the flamegraph.

Mono's native GC lifecycle callback records collection and stop-the-world
phases in `mono-gc-events.csv`. Per-frame heap capacity, used bytes, and
generation collection counters are also present in `frames.csv`. The GUI
indicator caches its style and content. Its event count, time, and managed-heap
delta are measured separately.

## Physics-body and exception diagnostics

Detailed mode times `PhysBody.UpdateColliders` and hashes the geometry of body
and finger colliders after each call. `frames.csv` correlates its cost and
unchanged-output count with frame time. `physbody-update-colliders.csv`
aggregates results by physics body. An unchanged hash is evidence that output
geometry stayed the same, not proof that the method performed no internal
writes.

`exceptions.csv` groups IL2CPP exceptions and MelonLoader errors by a stable
source hash without collecting a new managed stack trace every frame.

## Synchronization and completion waits

`wait-correlations.csv` pairs a sampled main-thread native wait stack with the
worker sampled on the same profiler tick. The pair is close in time. It does not
prove that the worker owns the synchronization object.

Detailed mode instruments the verified BONEWORKS Unity completion-counter wait:

- `completion-waits.csv` records duration, PlayerLoop phase, target and
  completed counters, caller addresses, and the resolved call stack.
- `completion-wait-workers.csv` records one bounded snapshot of up to three
  busy workers when the wait remains active across two 250 Hz samples.

Short waits do not trigger a worker snapshot.

## Graphics diagnostics

The graphics profiler resolves Unity's live graphics device and instruments
the pending-work virtual call that owns the completion fence.

- `graphics-pending-work.csv` records duration, completion target serial,
  PlayerLoop phase, and nested completion-wait IDs.
- `graphics-adapters.csv` lists DXGI adapters, LUID matches for SteamVR and
  process GPU engines, and vendor/device matches for Unity's reported device.
- `gpu-engines.csv` samples nonzero Windows GPU-engine counters for BONEWORKS at
  10 Hz, including adapter, LUID, and engine type.

These files help identify hybrid-GPU copy or presentation work without hooking
every D3D command.

Detailed mode can request Unity's binary profiler output. This BONEWORKS Unity
player may decline to create the optional file. `metadata.txt` records whether
the request succeeded.

## OpenVR pose profiling

Basic and Detailed captures can run a dedicated OpenVR sampler. The worker only
calls OpenVR and writes numeric structures. It never accesses Unity objects.

- `openvr-pose-history.csv` records background head and controller positions
  and rotations independently of rendered frames.
- `fixed-tick-poses.csv` records a main-thread OpenVR query for each Unity fixed
  tick.
- `resampled-fixed-tick-poses.csv` maps `Time.fixedTime` through the rendered
  frame clock, selects surrounding background samples, and records the
  interpolated pose used for comparison.

Position uses linear interpolation. Rotation uses spherical interpolation. The
configured delay normally makes a sample after the target time available
without prediction. The CSV records target time, sample timestamps,
interpolation fraction, pose age, bracket status, and difference from the raw
fixed-tick query. A target beyond the newest sample is marked `AfterHistory`
instead of being reported as interpolated.

`metadata.txt` reports requested and actual sampling rates, average and maximum
query costs, query errors, scheduler overruns, and OpenVR device indices.

The release tracking path extends this work. It keeps a timestamped pose history
on a background thread, maps each fixed tick onto that history, and applies the
selected head and hand poses. Future targets use bounded short-term prediction.
Clock correction is limited to 25 percent of one fixed interval. Positive
correction pauses while recent raw headset motion indicates a possible physical
jump. The head and hands share a pose clock.

## Automated workloads

Debug builds recognize command-line switches and empty sentinel directories
under `UserData/BoneworksPerformance`. Sentinels avoid Steam's custom-argument
confirmation dialog.

| Sentinel | Command-line switch | Purpose |
| --- | --- | --- |
| `RunAutomatedBenchmark` | `--bw-performance-benchmark` | Loads the captured Runoff gun-fly workload. |
| `RunAutomatedAB` | `--bw-performance-ab` | Runs the profiler-overhead A/B sequence. |
| `RunOptimizationAB` | `--bw-performance-optimization-ab` | Compares CPU-set, process-priority, and LOD experiments. |
| `RunFrameQueueAB` | `--bw-performance-frame-queue-ab` | Compares graphics queue depths. |
| `RunPoseHistorySmokeTest` | `--bw-pose-history-smoke-test` | Records a five-second main-menu OpenVR smoke test. |
| `RunFixedPoseSmoothnessTest` | `--bw-fixed-pose-smoothness-test` | Runs the synthetic fixed-update HMD motion and jump-protection test. |
| `RunGunflyCatchUpAB` | `--bw-gunfly-catchup-ab` | Compares pose-clock catch-up behavior in the Runoff workload. |

The Runoff benchmark teleports once to the captured gun-fly point, restores the
captured M16 and glitched magazine, and freezes the physics rig's foot-ball
rigidbody. It does not inject controller input or alter the head pose.

The profiler-overhead sequence compares metrics-only operation with and without
the GUI, Detailed instrumentation, and Unity's binary-profiler request.

The optimization sequence runs a symmetric eight-capture MetricsOnly comparison
of baseline behavior, Windows performance-core CPU sets, High process priority,
and a 25 percent lower LOD bias. See `EXPERIMENTS.md` for recorded results. These
settings remain opt-in because scheduler results need validation with the
SteamVR compositor active.

The frame-queue sequence compares the startup graphics queue setting with queue
depths 1, 2, and 3. Every condition gets a three-second warmup. SteamVR normally
resets this Unity setting every frame, so the runner reapplies it from
`OnUpdate`, records the reset count, and restores the startup value afterward.

The fixed-pose smoothness test runs in the main menu without a headset. It moves
a synthetic HMD horizontally at a known speed, simulates vertical jump motion,
injects a render-thread hitch, and records the pose seen by each fixed tick. It
writes `fixed-hmd-motion.csv`, `summary.txt`, and `plot.html` under
`UserData/BoneworksPerformance/smoothness-tests`.

Remove a sentinel after a test unless repeated execution is intentional.

## Debug preferences

Debug-only settings live in the `BoneworksPerformance` MelonPreferences section:

- `ProfilingMode`: `MetricsOnly`, `Basic`, or `Detailed`
- `MaximumCaptureSeconds`: automatic capture limit
- `EnableCpuSampling`: statistical CPU sampling
- `EnableCompletionWaitInstrumentation`: Unity completion waits
- `EnableGraphicsInstrumentation`: graphics pending work and GPU engines
- `EnableUnityBinaryProfiler`: optional Unity binary-profiler request
- `EnableMonoGcEvents`: Mono collection lifecycle events
- `EnableTargetedDiagnostics`: `PhysBody.UpdateColliders` and exception groups
- `EnablePoseHistoryRecorder`: background and fixed-tick OpenVR CSVs
- `ShowProfilingIndicator`: in-game red recording indicator
- `AutoProfileMainMenu`: automatic main-menu smoke capture
- `AutoProfileMainMenuSeconds`: automatic capture length
- `AutomatedRunoffBenchmark`: captured gun-fly workload
- `AutomatedDiagnosticSequence`: profiler-overhead comparison
- `PerformanceCoresOnly`: assign unpinned game and Unity workers to Windows'
  fastest CPU efficiency class
- `HighProcessPriority`: set BONEWORKS to High process priority
- `ReduceLodBias`: use 75 percent of the current quality setting's LOD bias
- `OptimizationCaptureSeconds`: duration of optimization A/B captures
- `FrameQueueCaptureSeconds`: duration of frame-queue captures
- `Il2CppDumperScriptPath`: optional matching IL2CppDumper `script.json`

## Review checklist

Before accepting a performance change:

1. Compare symmetric baselines before and after the experimental condition.
2. Confirm profiler self-time and suspension cost did not change the workload.
3. Test with SteamVR and a headset when the change affects compositor pacing,
   tracking, physics rate, or graphics queues.
4. Check that physics, tracking, saved settings, and speedrun behavior remain
   unchanged unless the change explicitly targets one of them.
5. Build both Debug and Release configurations and run the pose-history tests.
