# BoneworksPerformance

BoneworksPerformance improves tracking consistency during slow frames and makes
the physics-rate option in BONEWORKS work as intended.

## Installation

1. Install the BONEWORKS version of
   [MelonLoader](https://melonwiki.xyz/#/?id=what-is-melonloader).
2. Copy `BoneworksPerformance.dll` into the game's `Mods` directory.

The default Steam installation is usually at:

```text
C:\Program Files (x86)\Steam\steamapps\common\BONEWORKS\BONEWORKS
```

Credits to @Lakatrazz for the `PhysicsTickSelector` mod which was used as
reference for the physics-rate option. Do not run it at the same time. Its
behavior is included in BoneworksPerformance.

## What the mod does

### Smoother tracking during dropped frames

The mod records OpenVR head and controller poses on a background thread. Each
physics tick receives the pose that belongs to that point in time, interpolated
from the recorded history. Buttons, triggers, and thumbsticks are unchanged.

In the original game multiple physics ticks would be processed one after another
in quick succession during dropped frames before rendering and would use the
current tracked headset position at the time, meaning it would see a very
similar position for the set of physics tick. During jumps it would look like
inconsistent and choppy movement instead of a smooth motion which kills the
jump's momentum.

Tracking is delayed by one physics tick by default. This small delay gives the
mod samples on both sides of the requested time and prevents repeated or uneven
head and hand movement when several physics ticks run in one rendered frame.

### Super-jump protection

When the headset has moved upward at least 0.5 metres per second over the last
0.1 seconds, the mod pauses positive tracking-clock correction. Upward movement
keeps its original speed instead of being compressed while the game catches up
after a slow frame. Normal bounded catch-up resumes when the headset stops
rising.

### Gun-fly protection

When a slow rendered frame contains several physics ticks, BONEWORKS normally
leaves a weapon on the player's body at the chest pose from the previous
rendered frame. A glitched magazine can lose contact with the player during
those ticks and stop a gun fly. The mod fixes this by updating the weapon slot
position every physics frame instead of every rendered frame, the same as if
the game were rendering at full speed with no dropped frames.

### Physics-rate menu

BONEWORKS has a physics-rate selection, but SteamVR normally replaces it with
the headset display frequency. BoneworksPerformance disables that SteamVR lock
and applies the rate selected in the game's menu.

### Performance display

The optional display above the left hand contains:

- `FPS`: rendered Unity updates per second
- `Fixed`: physics updates per second
- `VR client`: the rate of the OpenVR application's `WaitGetPoses` calls
- `Display`: the headset's configured display frequency

`VR client` is not the headset refresh rate. It is calculated from the latest
OpenVR client frame interval, so it may fluctuate or appear higher than the
display frequency.

## Settings

Settings are in the `BoneworksPerformance` section of
`UserData/MelonPreferences.cfg`. You can also change them with an
IL2CPP-compatible MelonPreferences manager. Restart BONEWORKS after changing
them.

| Setting                             | Default | Effect                                                                                                   |
| ----------------------------------- | ------: | -------------------------------------------------------------------------------------------------------- |
| `SmoothTrackingDuringFrameDrops`    |  `true` | Uses timestamped OpenVR history for head and hand poses during physics ticks.                            |
| `TrackingSamplesPerSecond`          |   `250` | Background tracking rate. Values are clamped to 90 through 1000 Hz. Higher values use slightly more CPU. |
| `TrackingSmoothingDelay`            |     `1` | Tracking delay measured in physics ticks.                                                                |
| `ProtectSuperJumpsDuringFrameDrops` |  `true` | Prevents positive tracking-clock correction while the headset is rising quickly.                         |
| `JumpDetectionSpeed`                |   `0.5` | Upward headset speed in metres per second that activates jump protection.                                |
| `JumpDetectionTime`                 |   `0.1` | Seconds of tracking history used to measure upward speed.                                                |
| `ShowPerformanceStats`              | `false` | Shows the performance display above the left hand.                                                       |
| `UsePhysicsRateMenu`                |  `true` | Uses the physics rate selected in BONEWORKS instead of the headset frequency.                            |
| `KeepSlottedWeaponsWithPhysics`     |  `true` | Keeps body-slot weapons at the current physics-chest pose during multi-tick rendered frames.             |

## Troubleshooting

If tracking feels delayed, keep `TrackingSmoothingDelay` at `1` and confirm
that the game is running at the intended physics rate. Larger delay values are
mainly useful for testing.

If the physics rate still follows the headset, remove or disable other mods
that write `Time.fixedDeltaTime` or SteamVR's
`lockPhysicsUpdateRateToRenderFrequency` setting.

If the performance display does not appear, enter a level and make sure
`ShowPerformanceStats` is enabled. The display attaches after the player rig
and left controller exist.

For building, profiling, automated tests, report formats, and implementation
notes, see [CONTRIBUTING.md](CONTRIBUTING.md).
