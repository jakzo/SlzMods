Timer for Bonelab speedruns.

The timer will appear and start when loading into Descent, pauses during loading screens and finishes when sitting in the taxi. Also has an option to time individual levels (see configuration section below).

As a convenience it also stops your mods from being deleted when you delete your save from the menu but **this does not work on the Quest version of patch 4** due to a Melon Loader limitation.

# Installation

- Make sure [Melon Loader](https://melonwiki.xyz/#/?id=what-is-melonloader) is installed in Bonelab
  - To install Melon Loader for Quest follow the [instructions here](https://github.com/LemonLoader/MelonLoader/wiki/Installation)
  - For PC: Patch 3 or before must use Melon Loader 0.5.x and patch 4 onwards must use 0.6.x
  - For Quest: Lemon Loader currently installs Melon Loader 0.5.x (and works with patch 4)
- Download [the mod from Thunderstore](https://bonelab.thunderstore.io/package/jakzo/SpeedrunTimer/) (click on "Manual Download")
- Open the downloaded `.zip` file and open the folder corresponding to your game and Melon Loader version
- Extract the `Mods/SpeedrunTimer.Px.MLx.dll` file into `BONELAB/Mods/SpeedrunTimer.Px.MLx.dll` which is usually at:
  - Steam: `C:\Program Files (x86)\Steam\steamapps\common\BONELAB\BONELAB`
  - Oculus: `C:\Program Files\Oculus\Software\Software\stress-level-zero-inc-bonelab`
  - Quest: `/sdcard/Android/data/com.StressLevelZero.BONELAB/files`

## Livesplit Integration

Because no Livesplit autosplitter exists for the game yet and it's hard to create one, there is an autosplitter which is controlled by this mod. For the autosplitter to work, this mod must be installed and running. To set up the autosplitter:

- Download the [BonelabSpeedrunTimer.asl](https://raw.githubusercontent.com/jakzo/SlzMods/main/projects/Bonelab/SpeedrunTimer/scripts/BonelabSpeedrunTimer.asl) autosplitter (right click on [this link](https://raw.githubusercontent.com/jakzo/SlzMods/main/projects/Bonelab/SpeedrunTimer/scripts/BonelabSpeedrunTimer.asl) -> save link)
- Start Livesplit and edit layout settings (right click -> edit layout)
- Click the `+` icon and select "control" -> "scriptable auto splitter"
- Click the "layout settings" button then select the "scriptable auto splitter" tab
- Click the "browse" button and select the BonelabSpeedrunTimer.asl file you downloaded
- If it worked you should see some options appear

By default the autosplitter will pause during loading screens, split every time the level changes (not on level reload) or when sitting in the taxi and reset when exiting the taxi or changing levels after sitting in the taxi. Create your splits accordingly.

If you've set up the autosplitter you may not want the in-game timer anymore. You can disable it by setting the `hide` option to `true` (see instructions below).

### Quest

This mod also works with Livesplit One and Quest. See the instructions at [http://vr.jf.id.au/](http://vr.jf.id.au/input-viewer/instructions.html) to set it up.

## Input Viewer

This mod also supports an input viewer for showing your controller inputs and headset position to be used as an overlay for gameplay recordings. You can find instructions to install the input viewer at [http://vr.jf.id.au/](http://vr.jf.id.au/).

# Configuration

You can change some settings by editing the file at `BONELAB/MelonLoader/MelonPreferences.cfg` or by using MelonPreferencesManager:

- Install [MelonPreferencesManager](https://github.com/sinai-dev/MelonPreferencesManager) (download the IL2CPP version)
- Open the menu in-game using F5 to change config options

Most settings require restarting the level to take effect.

# Links

- Source code: https://github.com/jakzo/SlzMods/tree/main/projects/Bonelab/SpeedrunTimer
- Thunderstore: https://bonelab.thunderstore.io/package/jakzo/SpeedrunTimer/
