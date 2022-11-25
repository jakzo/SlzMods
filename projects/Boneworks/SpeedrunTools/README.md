# Features

- Speedrun mode
  - Enabling this mode will disable practice features and temporarily alter the save state so that a run will meet the leaderboard submission rules
  - There are various modes depending on what type of speedrun you are performing:
    - Normal
      - Activate with CTRL + S or by pressing A + B on both controllers
      - In this mode the save state will be reset every time you leave the main menu and start a new game
    - Newgame+
      - Activate with CTRL + N
      - In this mode the save state will be updated to have everything unlocked
    - 100%
      - Activate with CTRL + H
      - In this mode the save state will be reset only when initially enabled
      - Activating this will also reset all Steam achievements (so that the reclamation bin in Zombie Warehouse must be unlocked during the run)
  - Deactivate any mode with any of the activation hotkeys while in the main menu
  - Preferences like height and turn settings are maintained through save state resets and loads
  - Deactivate speedrun mode to restore your original save state
  - You must disable all other mods before enabling any speedrun mode
  - You can tell that a speedrun mode is enabled based on the text added to the loading screen (Steam game version only)
    - Includes mod version and approximate run time to make splicing harder (don't worry if the time does not match LiveSplit exactly)
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
    - If you do this during the main menu it will activate "blindfolded speedrun mode" where it will reset your save each time before starting a new game
  - This will make the VR headset display pitch black but the game window will still show the game (for spectating or to see where you are while practicing)
- Gripless
  - For practicing and performing gripless runs (where triggers/finger grips on controllers do nothing and you cannot pick things up)
  - To enable, press CTRL + G on the keyboard (game window must be focused)
    - If you do this during the main menu it will activate "gripless speedrun mode" where it will reset your save each time before starting a new game
  - This will make the controller trigger/finger buttons do nothing in the game so you cannot grab any objects
- One controller
  - Only one controller can be used and inputs from the other will be ignored
  - Ford's arm will also be removed
  - The menu button will still work even on the disabled controller
  - To enable, press CTRL + O on the keyboard (game window must be focused)
    - This will switch from off -> only left controller -> only right controller -> off -> ...
    - If you do this during the main menu it will activate "one-controller gripless speedrun mode" where it will reset your save each time before starting a new game
- Armless
  - Removes both of Ford's arms
  - To enable, press CTRL + A on the keyboard (game window must be focused)
    - If you do this during the main menu it will activate "armless speedrun mode" where it will reset your save each time before starting a new game

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

- Source code: https://github.com/jakzo/SlzSpeedrunTools/tree/main/projects/Boneworks/SpeedrunTools
- Thunderstore: https://boneworks.thunderstore.io/package/jakzo/SpeedrunTools/
