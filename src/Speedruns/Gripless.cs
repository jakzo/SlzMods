using HarmonyLib;
using StressLevelZero.Rig;

namespace SpeedrunTools.Speedruns {
class Gripless {
  [HarmonyPatch(typeof(BaseController),
                nameof(BaseController.GetPrimaryInteractionButton))]
  class BaseController_GetPrimaryInteractionButton_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix(bool __result) {
      if (Mode.CurrentMode == Mode.GRIPLESS &&
          Mod.s_gameState.currentSceneIdx != Utils.SCENE_MENU_IDX) {
        __result = false;
        return false;
      }
      return true;
    }
  }

  [HarmonyPatch(typeof(BaseController),
                nameof(BaseController.GetSecondaryInteractionButton))]
  class BaseController_GetSecondaryInteractionButton_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix(bool __result) {
      if (Mode.CurrentMode == Mode.GRIPLESS &&
          Mod.s_gameState.currentSceneIdx != Utils.SCENE_MENU_IDX) {
        __result = false;
        return false;
      }
      return true;
    }
  }

  [HarmonyPatch(typeof(BaseController),
                nameof(BaseController.GetIndexCurlAxis))]
  class BaseController_GetIndexCurlAxis_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix(float __result) {
      if (Mode.CurrentMode == Mode.GRIPLESS &&
          Mod.s_gameState.currentSceneIdx != Utils.SCENE_MENU_IDX) {
        __result = 0;
        return false;
      }
      return true;
    }
  }

  [HarmonyPatch(typeof(BaseController),
                nameof(BaseController.GetMiddleCurlAxis))]
  class BaseController_GetMiddleCurlAxis_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix(float __result) {
      if (Mode.CurrentMode == Mode.GRIPLESS &&
          Mod.s_gameState.currentSceneIdx != Utils.SCENE_MENU_IDX) {
        __result = 0;
        return false;
      }
      return true;
    }
  }

  [HarmonyPatch(typeof(BaseController), nameof(BaseController.GetRingCurlAxis))]
  class BaseController_GetRingCurlAxis_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix(float __result) {
      if (Mode.CurrentMode == Mode.GRIPLESS &&
          Mod.s_gameState.currentSceneIdx != Utils.SCENE_MENU_IDX) {
        __result = 0;
        return false;
      }
      return true;
    }
  }

  [HarmonyPatch(typeof(BaseController),
                nameof(BaseController.GetPinkyCurlAxis))]
  class BaseController_GetPinkyCurlAxis_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix(float __result) {
      if (Mode.CurrentMode == Mode.GRIPLESS &&
          Mod.s_gameState.currentSceneIdx != Utils.SCENE_MENU_IDX) {
        __result = 0;
        return false;
      }
      return true;
    }
  }
}
}
