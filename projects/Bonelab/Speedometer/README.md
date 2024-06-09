Shows how fast you're moving.

# Installation

- Make sure [Melon Loader](https://melonwiki.xyz/#/?id=what-is-melonloader) is installed in Bonelab
  - To install Melon Loader for Quest follow the [instructions here](https://github.com/LemonLoader/MelonLoader/wiki/Installation)
- Download [the mod from Thunderstore](https://bonelab.thunderstore.io/package/jakzo/Speedometer/) (click on "Manual Download")
- Open the downloaded `.zip` file and find the Mod `.dll` file matching your version of the game and Melon Loader in the `Mods` folder
  - For example, the file at `Mods/Speedometer.P3.ML5.dll` is the mod for Bonelab patch 3 using MelonLoader 0.5.x
- Extract this file into `BONELAB/Mods/Speedometer_*.dll` which is usually at:

  - Steam: `C:\Program Files (x86)\Steam\steamapps\common\BONELAB\BONELAB`
  - Oculus: `C:\Program Files\Oculus\Software\Software\stress-level-zero-inc-bonelab`
  - Quest: `/sdcard/Android/data/com.StressLevelZero.BONELAB/files`

# Settings

You can change these settings by modifying `UserData/MelonPreferences.cfg` in the game directory or by using the [MelonPreferencesManager](https://github.com/sinai-dev/MelonPreferencesManager) mod (download the IL2CPP version and press F5 to while in-game to open).

Most settings require restarting the level to take effect.

### `units`

Units to measure speed in.

- `MS` = meters-per-second and is equivalent to game-units-per-second
- `KPH` = kilometers-per-hour
- `MPH` = miles-per-hour

### `right_hand`

Set to `true` to display your speed over your right hand instead of left.

### `window_duration`

The way the speedometer works is it measures the distance between your current and position and your position a certain amount of time in the past to calculate how fast you moved in this time frame. This setting specifies the time frame. A higher time frame will give you a smoother speed measurement (your average speed) which is useful for measuring the speed of inconsistent movement (eg. hopping).

# Links

- Source code: https://github.com/jakzo/SlzSpeedrunTools/tree/main/projects/Bonelab/Speedometer
- Thunderstore: https://bonelab.thunderstore.io/package/jakzo/Speedometer/
