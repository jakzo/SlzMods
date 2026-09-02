## Unreleased

- Preserve the game's separate back-slot and open-inventory weapon rotations.
  Inventory transitions now discard any temporary fixed-tick pose, and chest
  catch-up pauses while the receiver is in UI mode.
- Keep occupied body-slot weapons aligned with the physics chest on additional
  physics ticks in one rendered frame. The isolated Runoff test retained
  glitched-magazine contact on 100% of 579 treatment catch-up ticks, compared
  with 50-60% in paired baselines, at about 0.063 ms per treated tick.
- Add a debug-only automated Runoff A/B fixture for measuring stale gun-fly
  contact and fixed-tick treatment cost.
- Record timestamped OpenVR head and controller poses on a dedicated thread and
  separately at every Unity fixed tick.
- Add pose-history CSV files, report metadata, a headsetless smoke-test mode,
  and unit tests for pose math and the concurrent history buffer.
- Add opt-in performance-core CPU sets, High process priority, and reduced LOD
  bias experiments with a symmetric automated MetricsOnly comparison.
- Add Detailed-mode diagnostics for `PhysBody.UpdateColliders` cost, unchanged
  collider outputs, and recurring exception identities.
- Simplify the Runoff fixture to one teleport, glitched-M16 restoration, and a
  frozen physics-rig foot ball, with no input or head-pose injection.
- Correct Mono GC event names to match the native profiler event enum.
- Resolve UnityPlayer frames from its matching PDB and fill GameAssembly symbol
  gaps from an optional IL2CppDumper map.
- Tag sampled stacks with their exact PlayerLoop phase and time every direct
  FixedUpdate subsystem per rendered frame.
- Record Unity and SteamVR DXGI adapter identity and LUIDs in Detailed mode.
- Time Unity's graphics pending-work call and link it to completion waits.
- Sample per-process Windows GPU engines at 10 Hz with adapter and engine IDs.

## 0.1.0

- Add the mod shell and debug-only performance capture tools.
