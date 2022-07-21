_Utilities for Boneworks speedrunning._

# Features

- Speedrun mode
  - Enabling this mode will disable practice features and temporarily reset the save state
    - Returning to the main menu while enabled will also trigger a reset of the save
    - Preferences like height and turn settings are maintained through a save reset
    - Deactivate speedrun mode to restore your save to before you enabled speedrun mode
  - Runs recorded in this mode are allowed to be submitted to the leaderboard
  - You must disable all other mods before enabling this mode
  - Pressing A + B on both controllers while in the menu will enable/disable this mode
    - Alternatively you can use CTRL + S on the keyboard (game window must be focused)
  - When this mode is enabled, there will be a green Boneworks logo in the loading screen (Steam game version only)
  - For 100% runs you can press CTRL + H on the keyboard to enable 100% speedrun mode
    - The difference between this and regular speedrun mode is that it will not reset the save when going back to the main menu
    - The Boneworks logo in the loading screen will be _blue_ in this mode
- Make boss claw always patrol to the area near the finish in Streets
  - Boss claw cabin appears _green_ when this feature is on, so that you're aware RNG is being manipulated
- Teleport to a chosen location to practice parts of a level again
  - Pressing B on both controllers at the same time sets the teleport point to the position you are currently standing at
  - Clicking the right controller thumbstick teleports you to the set point
  - Clicking A and B on the left controller at the same time resets the level
    - Useful for situations like in Museum when you teleport back to retry valve flying and need the valve to be back at its starting location
- Blindfold (Steam game version only)
  - For practicing and performing blindfolded runs
  - To blindfold yourself, press CTRL + B on the keyboard (game window must be focused)
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

Most settings require restarting the level to take effect.

# Links

- Repository: https://github.com/jakzo/BoneworksSpeedrunTools
- Thunderstore: https://boneworks.thunderstore.io/package/jakzo/SpeedrunTools/
- How I reverse engineer the game: https://jakzo.github.io/BoneworksSpeedrunTools/
