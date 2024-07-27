using MelonLoader;
using UnityEngine;
using HarmonyLib;
using StressLevelZero.Utilities;

namespace Sst.Features {
class RestartLevel : Feature {
  private static int? s_lockedLevel;

  public RestartLevel() {
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) =>
          Mod.GameState.currentSceneIdx != Utils.SCENE_MENU_IDX &&
          Utils.GetKeyControl() && Input.GetKey(KeyCode.R),
      Handler = () => SetLevelLocked(s_lockedLevel == null),
    });
  }

  private void SetLevelLocked(bool levelLocked) {
    if (levelLocked) {
      s_lockedLevel = Mod.GameState.currentSceneIdx;
      MelonLogger.Msg("Locking level (will now restart when finishing " +
                      "instead of going to next level)");
    } else {
      s_lockedLevel = null;
      MelonLogger.Msg("Unlocking level");
    }
  }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    if (buildIndex == s_lockedLevel)
      return;
    if (s_lockedLevel != null)
      SetLevelLocked(false);
  }

  [HarmonyPatch(
      typeof(BoneworksSceneManager), nameof(BoneworksSceneManager.LoadNext)
  )]
  class BoneworksSceneManager_LoadNext_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix() {
      if (s_lockedLevel == null)
        return true;

      BoneworksSceneManager.ReloadScene();
      return false;
    }
  }
}
}
