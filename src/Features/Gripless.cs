using MelonLoader;
using UnityEngine;
using HarmonyLib;
using StressLevelZero.Rig;

namespace SpeedrunTools.Features {
public class Gripless : Feature {
  public static bool IsGripDisabled = false;

  public readonly Hotkey HotkeyGripless =
      new Hotkey() { Predicate = (cl, cr) => Mod.GameState.currentSceneIdx !=
                                                 Utils.SCENE_MENU_IDX &&
                                             Utils.GetKeyControl() &&
                                             Input.GetKey(KeyCode.G),
                     Handler = () => {
                       if (IsGripDisabled) {
                         MelonLogger.Msg("Enabling grip");
                         IsGripDisabled = false;
                       } else {
                         MelonLogger.Msg("Disabling grip");
                         IsGripDisabled = true;
                       }
                     } };

  public override void OnDisabled() { IsGripDisabled = false; }

  // Controller.CacheInputs()/.ProcessFingers() call the SteamVR_Action API
  // to get inputs then sets these properties, so we can reset them right
  // after this call before the game does anything using them
  [HarmonyPatch(typeof(Controller), nameof(Controller.CacheInputs))]
  class Controller_CacheInputs_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(Controller __instance) {
      if (!IsGripDisabled ||
          Mod.GameState.currentSceneIdx == Utils.SCENE_MENU_IDX)
        return;

      __instance._primaryAxis = 0;
      __instance._primaryInteractionButton = false;
      __instance._primaryInteractionButtonDown = false;
      __instance._primaryInteractionButtonUp = false;
      __instance._secondaryInteractionButton = false;
      __instance._secondaryInteractionButtonDown = false;
      __instance._secondaryInteractionButtonUp = false;
      __instance._thumbstickTouch = false;
    }
  }

  [HarmonyPatch(typeof(Controller), nameof(Controller.ProcessFingers))]
  class Controller_ProcessFingers_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(Controller __instance) {
      if (!IsGripDisabled ||
          Mod.GameState.currentSceneIdx == Utils.SCENE_MENU_IDX)
        return;

      __instance._processedIndex = 0;
      __instance._processedMiddle = 0;
      __instance._processedRing = 0;
      __instance._processedPinky = 0;
    }
  }
}
}
