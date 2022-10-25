using UnityEngine.Events;
using HarmonyLib;
using SLZ.Marrow.Warehouse;
using SLZ.Marrow.SceneStreaming;

namespace Sst.Utilities {
static class LevelHooks {
  public static LevelCrate PrevLevel;
  public static LevelCrate CurrentLevel;
  public static LevelCrate NextLevel;
  public static bool IsLoading { get => _activeLoadingScene != null; }

  public static UnityEvent<LevelCrate> OnLoad = new UnityEvent<LevelCrate>();
  public static UnityEvent<LevelCrate> OnLevelStart =
      new UnityEvent<LevelCrate>();

  private static LoadingScene _activeLoadingScene;

  public static void OnUpdate() {
    if (_activeLoadingScene != null &&
        !_activeLoadingScene.gameObject.scene.isLoaded) {
      _activeLoadingScene = null;
      CurrentLevel = NextLevel;
      NextLevel = null;
      OnLevelStart.Invoke(CurrentLevel);
    }
  }

  [HarmonyPatch(typeof(LoadingScene), nameof(LoadingScene.Start))]
  class LoadingScene_Start_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(LoadingScene __instance) {
      _activeLoadingScene = __instance;
      PrevLevel = CurrentLevel;
      CurrentLevel = null;
      NextLevel = SceneStreamer._session._level.Crate;
      Dbg.Log($"LoadingScene_Start_Patch, next level = {NextLevel.Title}");
      OnLoad.Invoke(NextLevel);
    }
  }
}
}
