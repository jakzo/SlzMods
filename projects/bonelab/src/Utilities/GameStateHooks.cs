using MelonLoader;
using HarmonyLib;
using SLZ.Bonelab;

namespace SpeedrunTools {
class GameStateHooks {
  [HarmonyPatch(typeof(BonelabInternalGameControl),
                nameof(BonelabInternalGameControl.LevelComplete))]
  class BonelabInternalGameControl_LevelComplete_Patch {
    [HarmonyPrefix()]
    internal static void Prefix() { Mod.GameState.didPrevLevelComplete = true; }
  }

  [HarmonyPatch(typeof(BonelabInternalGameControl),
                nameof(BonelabInternalGameControl.JustJumpToLevel),
                new System.Type[] { typeof(string) })]
  class BonelabInternalGameControl_JustJumpToLevel_Patch {
    [HarmonyPrefix()]
    internal static void
    Prefix(SLZ.Marrow.Warehouse.LevelCrateReference level) {
      Utils.LogDebug($"LoadScene: {sceneName}");
      GameState.nextSceneIdx = Utils.SCENE_INDEXES_BY_NAME[sceneName];
    }
  }

  [HarmonyPatch(typeof(CVRCompositor), nameof(CVRCompositor.FadeGrid))]
  class CVRCompositor_FadeGrid_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(float fSeconds, bool bFadeIn) {
      if (bFadeIn) {
        GameState.prevSceneIdx = GameState.currentSceneIdx;
        var prevSceneIdx = GameState.currentSceneIdx ?? 0;
        GameState.currentSceneIdx = null;
        GameState.rigManager = null;
        OnFeatureCallback(feature => feature.OnLoadingScreen(
                              GameState.nextSceneIdx ?? 0, prevSceneIdx));
      } else {
        GameState.currentSceneIdx = GameState.nextSceneIdx;
        GameState.nextSceneIdx = null;
        GameState.rigManager = Utilities.Boneworks.GetRigManager();
        OnFeatureCallback(
            feature => feature.OnLevelStart(GameState.currentSceneIdx ?? 0));
        GameState.didPrevLevelComplete = false;
      }
    }
  }
}
}
