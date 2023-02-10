using MelonLoader;
using UnityEngine;
using StressLevelZero.Rig;

namespace Sst.Features {
public class Gripless : Feature {
  public static bool IsGripDisabled = false;

  public Gripless() {
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) =>
          Mod.GameState.currentSceneIdx != Utils.SCENE_MENU_IDX &&
          (Utils.GetKeyControl() && Input.GetKey(KeyCode.G) ||
#if DEBUG
           cr.GetThumbStick()
#else
           false
#endif
               ),
      Handler = () => {
        if (IsGripDisabled) {
          MelonLogger.Msg("Enabling grip");
          IsGripDisabled = false;
        } else {
          MelonLogger.Msg("Disabling grip");
          IsGripDisabled = true;
        }
      },
    });
  }

  public override void OnDisabled() { IsGripDisabled = false; }

  // Controller.CacheInputs() call the SteamVR_Action API to get inputs then
  // sets these properties, so we can reset them right after this call before
  // the game does anything using them
  public static void OnCacheInputs(Controller controller) {
    if (IsGripDisabled && Mod.GameState.currentSceneIdx != Utils.SCENE_MENU_IDX)
      CancelGrip(controller);
  }
  public static void CancelGrip(Controller controller) {
    controller._primaryAxis = 0;
    controller._primaryInteractionButton = false;
    controller._primaryInteractionButtonDown = false;
    controller._primaryInteractionButtonUp = false;
    controller._secondaryInteractionButton = false;
    controller._secondaryInteractionButtonDown = false;
    controller._secondaryInteractionButtonUp = false;
    controller._thumbstickTouch = false;
  }

  public static void OnProcessFingers(Controller controller) {
    if (IsGripDisabled && Mod.GameState.currentSceneIdx != Utils.SCENE_MENU_IDX)
      CancelProcessFingers(controller);
  }
  public static void CancelProcessFingers(Controller controller) {
    controller._processedIndex = 0;
    controller._processedMiddle = 0;
    controller._processedRing = 0;
    controller._processedPinky = 0;
  }

  public static void OnSolveGrip(Controller controller) {
    if (IsGripDisabled && Mod.GameState.currentSceneIdx != Utils.SCENE_MENU_IDX)
      CancelSolveGrip(controller);
  }
  public static void CancelSolveGrip(Controller controller) {
    controller._gripForce = 0;
    controller._solvedGrip = 0;
    controller._solvedGripVelocity = 0;
  }
}
}
