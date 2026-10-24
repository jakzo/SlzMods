# Optimization experiments

## 2026-09-02 gun-fly fixed-tick catch-up test

The isolated Runoff fixture compared the normal `RigManager.FixedUpdate` path
with an experimental catch-up path on the second and later physics ticks in a
rendered frame. The catch-up path realigned the rig root and ran the body solve
before physics. A controlled alternating 2.5 cm displacement between physics
ticks made stale back-slot placement observable without a headset. The sequence
was baseline, treatment, treatment, baseline; every condition used a two-second
warmup and six seconds of measurement, with periodic 45 ms stalls to force
multi-tick frames.

| Condition | Catch-up ticks | Magazine touching chest | Typical treatment cost |
| --- | ---: | ---: | ---: |
| Baseline 1 | 143 | 67.8% | - |
| Treatment 1 | 153 | 87.6% | 0.140 ms mean, excluding one hitch |
| Treatment 2 | 163 | 84.7% | 0.142 ms mean, excluding one hitch |
| Baseline 2 | 142 | 81.7% | - |

The paired averages were 74.8% for baseline and 86.1% for treatment, an 11.3
percentage-point improvement. Treatment reached 100% contact on even-numbered
catch-up ticks, but odd-numbered ticks remained near baseline. Each treatment
condition also contained one roughly 100 ms process hitch; the median treatment
cost was 0.131-0.137 ms and P95 was 0.198 ms, so those hitches were not normal
execution cost.

This supports the underlying stale-Update hypothesis, but rejects the current
catch-up implementation as a release change. Its benefit is incomplete and the
baseline drift between the first and fourth conditions is too large for a
precise effect estimate. The next experiment should identify and advance the
specific back-slot or inventory transform owner, rather than rerunning a broad
body solve, then validate the result during a repeatable headset gun fly.

### Slot-only follow-up

Decompilation showed that `HandWeaponSlotReciever.UpdateTransform` only records
the weapon's local offset when the game slots it. The weapon then follows a
GameWorld skeleton transform. `GameWorldSkeletonRig.OnLateUpdate` updates that
skeleton once per rendered frame, after every physics tick in the frame has
already run.

The follow-up treatment saved the slotted weapon's pose relative to the physics
chest. Before each additional physics tick in a rendered frame, it placed only
the weapon host from the current physics-chest pose. It restored the original
local pose before the game's normal `LateUpdate`. The fixture established
magazine contact at each measurement boundary and kept that initial reference
fixed for the condition.

| Condition | Catch-up ticks | Magazine touching chest | Median treatment cost |
| --- | ---: | ---: | ---: |
| Baseline 1 | 301 | 60.5% | - |
| Treatment 1 | 285 | 100.0% | about 0.063 ms |
| Treatment 2 | 294 | 100.0% | about 0.063 ms |
| Baseline 2 | 293 | 49.8% | - |

The treatment's P95 weapon-host tracking error was 0 mm in both treatment arms.
It preserved contact on every measured catch-up tick, including odd and even
ticks. This is the implemented release approach. It does not rerun body IK,
animation, inventory interaction, or arbitrary `Update` methods.

## 2026-09-01 headsetless graphics queue test

The isolated Runoff fixture compared graphics queue depths 1, 2, and 3 in a
symmetric eight-capture sequence. Each MetricsOnly capture used a three-second
warmup and eight seconds of samples. The startup queue depth was 2.

SteamVR's render path reset `QualitySettings.maxQueuedFrames` to `-1` on almost
every rendered frame. The test runner therefore reapplied the requested value
from `OnUpdate` and counted the resets. There were about 103 resets during each
11-second warmup-and-capture condition.

| Queue depth | Frames | Mean frame | Median | P95 | Mean FPS |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 | 144 | 109.74 ms | 109.48 ms | 110.21 ms | 9.11 |
| 2 | 292 | 109.54 ms | 109.48 ms | 110.19 ms | 9.13 |
| 3 | 145 | 109.51 ms | 109.47 ms | 110.23 ms | 9.13 |

The headsetless SteamVR compositor spent about 100.2 ms idle per frame, so the
absolute frame rate does not represent headset play. The queue depths had no
measurable effect even while forcibly reapplied. Combined with the backend
resetting the property every frame, this is not a useful optimization target.

## 2026-08-31 headless Runoff scheduler and LOD test

The automated Runoff fixture ran eight MetricsOnly captures in this order:

1. Baseline
2. Windows performance-core CPU sets
3. Performance-core CPU sets and High process priority
4. The previous settings with `QualitySettings.lodBias` reduced from 1 to 0.75
5. The same four conditions in reverse order

Each capture used a three-second warmup and eight seconds of samples. BONEWORKS
ran without a headset, so the absolute frame rates do not represent VR play.
The paired comparisons are useful for rejecting no-op changes and selecting
settings for a headset test.

| Condition | Mean FPS | Mean frame | Median | P95 | P99 | FPS change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Baseline | 52.12 | 19.19 ms | 18.60 ms | 20.64 ms | 29.57 ms | - |
| Performance cores | 52.30 | 19.12 ms | 18.34 ms | 20.24 ms | 28.20 ms | +0.35% |
| Performance cores and High priority | 53.52 | 18.69 ms | 18.06 ms | 19.90 ms | 26.70 ms | +2.69% |
| Previous settings and 0.75 LOD bias | 54.29 | 18.42 ms | 17.96 ms | 19.54 ms | 25.89 ms | +4.16% |

Performance-core placement alone is too small to call an optimization. High
priority accounts for most of the scheduler result, but it could compete with
the SteamVR compositor and must be tested with a headset before becoming a
default. The lower LOD bias adds about 1.4% over the scheduler settings and has
the expected visual cost.

Two proposed visual changes were rejected before capture. Realtime reflection
probe updates were already disabled, and the game already used one shadow
cascade.
