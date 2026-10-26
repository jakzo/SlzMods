# FrameFix

> Allows any computer or headset to use game movement and mechanics that require high frame rates and refresh rates.

## Installation

1. Install version 0.5.7 of [MelonLoader](https://melonwiki.xyz/#/?id=what-is-melonloader).
2. Copy `BoneworksFrameFix.dll` into the game's `Mods` directory.
   - The default Steam installation is usually at `C:\Program Files (x86)\Steam\steamapps\common\BONEWORKS\BONEWORKS`
3. **⚠️ IMPORTANT ⚠️:** Start the game and check the in-game "physics rate" setting in the main menu since that will be used instead of your headset refresh rate.

Credits to Lakatrazz for the `PhysicsTickSelector` mod which was used as reference for the physics rate option!

## Why?

The primary goal of this mod is to make the game behave the same as it does on a fast computer experiencing no frame drops and high refresh rate, even on slow hardware. This levels the playing field for speedruns since most movement and tricks benefit from high refresh rates and good frame rates which are only achievable for expensive top-tier CPUs and certain headsets.

## How it works

When the vanilla game with no mod is running well it will render the image to the headset, then calculate a physics tick, then render the next image to headset and repeat. But under lag, it renders the image to the headset, then calculates multiple physics ticks in a row before rendering the next image to the headset. Due to the way the game was made, certain things only happen when the game renders the image to the headset, such as getting the new position of the headset and updating the position of weapons in the inventory slots. These cause issues because:

- During an IRL jump under lag the headset moves up then stops for a couple of frames, then suddenly teleports up a bit further and stops for another few frames. This jerky movement doesn't give good momentum so stops a super jump from working as good as it would if the headset was continuously rising with each physics frame.
- During gun fly the glitched magazine pushes into the player, the player gets pushed away since the inventory position is fixed, then the immediate frame after the mag is positioned back inside the player. But under lag the player gets pushed away, the glitched mag position is not updated and if the player has already been pushed clear of the mag they will have no momentum and simply fall.

In general, this mod fixes these issues by changing the game code to instead do these things between physics ticks rather than on rendered frames.

## What it improves

- **Smoother tracking during dropped frames**
  - The mod continuously reads headset inputs in a separate thread in the background and saves them to a short-term history.
  - During lag the game computes the intermediate physics ticks between rendered frames as fast as it can, meaning each one is not computed at regular intervals, so the saved headset input which corresponds to the time of the physics tick is applied for that frame.
  - Catches up inputs when the game runs slower than real-life (happens under heavy lag).
  - Inputs for times between collected input samples are interpolated between the values.
  - **Benefits**
    - Faster b-hopping
    - Higher real-life augmented jumps
    - Makes anything needing smooth movement better
- **Jump protection**
  - Detects real-life jumps by monitoring headset upwards velocity and pauses input catch-up for the duration of the jump so the in-game movement is as smooth as the real-life movement for the duration of the real-life jump.
  - Also applies slomo and jump buttons according to the real-life jump timing, not the game's uneven or delayed timing.
  - **Benefits**
    - Higher real-life augmented jumps
- **Gun fly protection**
  - Updates the inventory weapon positions between physics ticks instead of before visual frame render.
  - This keeps the glitched mag in the correct position and contacting the player's colliders during gun fly.
  - **Benefits**
    - Prevents sudden drops out of the sky which happen during frame drops

### Physics rate menu

The game has a physics rate option in the menu but it has not effect because SteamVR forces it to the headset refresh rate. This mod overrides this behavior and uses the game physics rate instead.

You can also use the `CustomPhysicsTickRate` option instead to select a custom refresh rate. This is useful to simulate what you could achieve on the vanilla game using a specific headset.

## Settings

| Setting                             | Default | Effect                                                                                                    |
| ----------------------------------- | ------: | --------------------------------------------------------------------------------------------------------- |
| `SmoothTrackingDuringFrameDrops`    |  `true` | Uses one timestamped physics clock for HMD position, jump, and slow-motion clicks.                        |
| `ProtectGunFlyDuringFrameDrops`     |  `true` | Keeps holstered weapons with the physics chest between additional physics ticks in a rendered frame.      |
| `ProtectSuperJumpsDuringFrameDrops` |  `true` | Prevents positive tracking-clock correction while the headset is rising quickly.                          |
| `UsePhysicsRateMenu`                |  `true` | Uses the physics rate selected in BONEWORKS instead of the headset frequency.                             |
| `CustomPhysicsTickRate`             |     `0` | Positive values force that rate; 0 lets `UsePhysicsRateMenu` select menu or normal headset-rate behavior. |

There are also several advanced settings. These can negatively impact your gameplay if set incorrectly so avoid editing these unless you know what you are doing.

## Known issues

- By default, head position and jump/slomo inputs are buffered by one physics tick, about 6.9 ms at 144 Hz. This is not the total motion-to-screen latency; the history can fall further behind during heavy lag. Hand tracking and head rotation are not buffered by this mod.
- Under heavy lag some things are not perfectly identical and there may still be slight penalties to b-hop speed and jump height (but way less than there would be without the mod)
