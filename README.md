_Utilities for Boneworks speedrunning._

# Features

- Make boss claw always patrol to the area near the finish in Streets
  - Boss claw cabin appears _green_ when this feature is on, so that you're aware RNG is being manipulated
- Teleport to a chosen location to practice parts of a level again
  - Pressing B on both controllers at the same time sets the teleport point to the position you are currently standing at
  - Clicking the right controller thumbstick teleports you to the set point
  - Clicking A and B on the left controller at the same time resets the level
    - Useful for situations like in Museum when you teleport back to retry valve flying and need the valve to be back at its starting location
- Reset your save without restarting the game
  - Keeps your preferences so you don't need to set things like height and turn settings after a reset
  - To reset you must be in the main menu and press A + B on both controllers at the same time
    - Alternatively you can use CTRL + R on the keyboard to trigger a reset (game window must be focussed)
  - Backs up the old save at the location your game save files are kept
    - By default the backup will be at `%UserProfile%\AppData\LocalLow\Stress Level Zero\BONEWORKS.backup`
    - Restore the save by replacing the `BONEWORKS` directory with the `BONEWORKS.backup` one
    - It **does not** save a backup if a backup already exists
- Blindfold (Steam game version only)
  - For practicing and performing blindfolded runs
  - To blindfold yourself, press CTRL + B on the keyboard (game window must be focussed)
  - This will make the VR headset display pitch black but the game window will still show the game (for spectating or to see where you are while practicing)

# Installation

- Make sure [Melon Loader](https://melonwiki.xyz/#/?id=what-is-melonloader) is installed in Boneworks
- Download [the SpeedrunTools mod from Thunderstore](https://boneworks.thunderstore.io/package/jakzo/SpeedrunTools/) (click on "Manual Download")
- Open the downloaded `.zip` file and extract `Mods/SpeedrunTools.dll` into `BONEWORKS/Mods/SpeedrunTools.dll`
  - You can usually find your `BONEWORKS` directory at `C:\Program Files (x86)\Steam\steamapps\common\BONEWORKS\BONEWORKS`

# Configuration

You can change some things (like where the boss claw moves to) by using MelonPreferencesManager:

- Install [MelonPreferencesManager](https://github.com/sinai-dev/MelonPreferencesManager) (download the IL2CPP version)
- Open the menu in-game using F5 to change config options

For boss claw settings to take effect you must restart the level.

# Links

- Repository: https://github.com/jakzo/BoneworksSpeedrunTools
- Thunderstore: https://boneworks.thunderstore.io/package/jakzo/SpeedrunTools/
- How I reverse engineer the game: https://jakzo.github.io/BoneworksSpeedrunTools/
