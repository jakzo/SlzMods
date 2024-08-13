Adds support for hand tracking.

## Installation

- This mod is for the **patch 4 Quest standalone** version of the game
- Install **Melon Loader 0.5.7** via Lemon Loader
- Add the hand tracking permission to the game APK using [Quest Patcher](https://github.com/Lauriethefish/QuestPatcher)
  - **IMPORTANT: This will RESET YOUR GAME and you will LOSE YOUR SAVE AND MODS so back them up first**
  - Until [my PR to add the "mod loader: none" option](https://github.com/Lauriethefish/QuestPatcher/pull/188) is merged, you will need to download [my fork of Quest Patcher](https://github.com/jakzo/QuestPatcher/releases) instead of the official one
  - Go to the "Tools & Options" tab -> click the "Change App" button -> select `com.StressLevelZero.BONELAB`
  - Go to the "Patching" tab -> **select `None` for the mod loader** (you should have already installed Melon Loader)
  - Click "Patching Options" -> **scroll to "Hand Tracking Type" -> select `V2`**
  - Click the button to patch the game
- Copy this mod to `/sdcard/Android/data/com.StressLevelZero.BONELAB/files/Mods/*.dll`
- Make sure hand tracking is enabled in your Quest settings
  - You should know by putting your controllers down while in the Quest menu and it switches to hands within a few seconds
- Start the game
- For best results make sure your settings match these defaults:
  - Locomotion controlled by left controller (right handed mode)
  - Movement direction is direction of head

## Usage

- Click on UIs by pinching
- Open the in-game menu by doing the standard Quest hand tracking menu gesture (left hand open, palm towards your face then touch the tips of your index finger and thumb)
- Walk by moving your hands up and down alternately in a running motion
- Grab by curling middle, ring and pinky fingers into a fist (same effect as pressing trigger and grip on controller)
- Force pull objects by forming a fist with these three fingers then flicking your wrist (in any direction)
- Held items like guns are triggered by curling the index finger

## Tips

Hand tracking's accuracy is _very_ limited. Don't expect everything to work perfectly. I did put in effort to make it less frustrating by taking into account the tracking confidence and assuming certain things when the hands are out of view, but it won't always do what you want if the headset can't see your hands/fingers. Here are some tips to help make your experience smoother:

- When running, make sure the headset has a clear view of your hands by either:
  - Holding your hands a bit higher and further forwards than normal (so they are not too close to the headset)
  - Looking downwards
- When running, instead of pointing your hands forwards in a fist, open them and have your palms facing you
- Don't hold guns with two hands while aiming (headset gets confused and will make your in-game hands do weird things because your IRL hands are overlapping)
- Don't hold guns too close to your face while aiming (hand will lose tracking if too close to headset)
- While throwing things, climbing or any other action, try not to bring your hands too close to the headset or out of your eye sight

## Fun facts

SLZ has already added a bunch of hand tracking code. By just adding the hand tracking permission to the APK, `MarrowGame.xr.HandLeft/Right` will start tracking the hand position! Finger poses are not updated because the code is missing from the `OculusHandActionMap`, however they do actually have finger pose and gesture code in the generic `HandActionMap.ProcessesHand` method. I tried to lean on their work and get it working but there were too many gaps and it ended being easier to reimplement everything myself.
