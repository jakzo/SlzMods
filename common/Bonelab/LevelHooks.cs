using System;
using MelonLoader;
using UnityEngine.SceneManagement;
using HarmonyLib;
using SLZ.Marrow.Warehouse;
using SLZ.Marrow.SceneStreaming;
using SLZ.Rig;

namespace Sst.Utilities {
static class LevelHooks {
  public static LevelCrate PrevLevel;
  public static LevelCrate CurrentLevel;
  public static LevelCrate NextLevel;
  public static RigManager RigManager;
  public static BasicTrackingRig BasicTrackingRig;
  public static bool IsLoading { get => !CurrentLevel; }

  public static event Action<LevelCrate> OnLoad;
  public static event Action<LevelCrate> OnLevelStart;

  private static Scene _loadingScene;

  private static void SafeInvoke(string name, Action<LevelCrate> action,
                                 LevelCrate level) {
    try {
      action?.Invoke(level);
    } catch (Exception ex) {
      MelonLogger.Error($"Failed to execute {name} event: {ex.ToString()}");
    }
  }

  private static void WaitForLoadFinished() {
    if (_loadingScene.isLoaded)
      return;
    MelonEvents.OnUpdate.Unsubscribe(WaitForLoadFinished);

    CurrentLevel = NextLevel ?? SceneStreamer.Session.Level ?? CurrentLevel;
    NextLevel = null;
    Dbg.Log($"OnLevelStart {CurrentLevel?.Title}");
    SafeInvoke("OnLevelStart", OnLevelStart, CurrentLevel);
  }

  // [HarmonyPatch(typeof(LoadingScene), nameof(LoadingScene.Start))]
  // class LoadingScene_Start_Patch {
  //   [HarmonyPrefix()]
  //   internal static void Prefix(LoadingScene __instance) {
  //     Dbg.Log("LoadingScene_Start_Patch");
  //     _loadingScene = __instance.gameObject.scene;
  //     MelonEvents.OnUpdate.Subscribe(WaitForLoadFinished);
  //   }
  // }

  [HarmonyPatch(typeof(BasicTrackingRig), nameof(BasicTrackingRig.Awake))]
  class BasicTrackingRig_Awake_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(BasicTrackingRig __instance) {
      Dbg.Log("BasicTrackingRig_Awake_Patch");
      _loadingScene = __instance.gameObject.scene;
      if (CurrentLevel)
        PrevLevel = CurrentLevel;
      CurrentLevel = null;
      NextLevel = SceneStreamer.Session.Level;
      RigManager = null;
      BasicTrackingRig = __instance;
      MelonEvents.OnUpdate.Subscribe(WaitForLoadFinished);
      Dbg.Log($"OnLoad {NextLevel?.Title}");
      SafeInvoke("OnLoad", OnLoad, NextLevel);
    }
  }

  [HarmonyPatch(typeof(RigManager), nameof(RigManager.Awake))]
  class RigManager_Awake_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(RigManager __instance) {
      Dbg.Log("RigManager_Awake_Patch");
      RigManager = __instance;
      BasicTrackingRig = null;
    }
  }

  // [HarmonyPatch(
  //     typeof(SceneStreamer), nameof(SceneStreamer.Load),
  //     new Type[] { typeof(LevelCrateReference), typeof(LevelCrateReference)
  //     })]
  // class SceneStreamer_Load_Patch {
  //   [HarmonyPrefix()]
  //   internal static void Prefix(LevelCrateReference level,
  //                               LevelCrateReference loadLevel) {
  //     var nextLevel = level.Crate;
  //     Dbg.Log($"SceneStreamer_Load_Patch, next level = {nextLevel?.Title}");
  //     if (CurrentLevel)
  //       PrevLevel = CurrentLevel;
  //     CurrentLevel = null;
  //     NextLevel = nextLevel;
  //     RigManager = null;

  //     // _loadingScene =
  //     // SceneManager.GetSceneByName(loadLevel.Crate.MainScene.Asset.name);
  //     // MelonEvents.OnUpdate.Subscribe(WaitForLoadFinished);

  //     SafeInvoke("OnLoad", OnLoad, NextLevel);
  //   }
  // }

  // [HarmonyPatch(typeof(SceneStreamer), nameof(SceneStreamer.Reload))]
  // class SceneStreamer_Reload_Patch {
  //   [HarmonyPrefix()]
  //   internal static void Prefix() {
  //     Dbg.Log("SceneStreamer_Reload_Patch");
  //     PrevLevel = CurrentLevel;
  //     CurrentLevel = null;
  //     if (CurrentLevel)
  //       NextLevel = CurrentLevel;
  //     RigManager = null;
  //     OnLoad?.Invoke(NextLevel);
  //   }
  // }
}
}
