using MelonLoader;
using HarmonyLib;
using SLZ.Rig;

namespace SpeedrunTools {
class GameStateHooks {
  [HarmonyPatch(typeof(BoneworksSceneManager),
                nameof(BoneworksSceneManager.LoadNext))]
  class BoneworksSceneManager_LoadNext_Patch {
    [HarmonyPrefix()]
    internal static void Prefix() { GameState.didPrevLevelComplete = true; }
  }

  [HarmonyPatch(typeof(BoneworksSceneManager),
                nameof(BoneworksSceneManager.LoadScene),
                new System.Type[] { typeof(string) })]
  class BoneworksSceneManager_LoadScene_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(string sceneName) {
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
