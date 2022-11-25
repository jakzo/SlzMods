using System;
using MelonLoader;
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
  public static bool IsLoading { get => !CurrentLevel; }

  public static event Action<LevelCrate> OnLoad;
  public static event Action<LevelCrate> OnLevelStart;

  private static void SafeInvoke(string name, Action<LevelCrate> action,
                                 LevelCrate level) {
    try {
      action?.Invoke(level);
    } catch (Exception ex) {
      MelonLogger.Error($"Failed to execute {name} event: {ex.ToString()}");
    }
  }

  [HarmonyPatch(typeof(RigManager), nameof(RigManager.Awake))]
  class RigManager_Awake_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(RigManager __instance) {
      Dbg.Log($"RigManager_Awake_Patch");
      CurrentLevel = NextLevel ?? SceneStreamer.Session.Level ?? CurrentLevel;
      NextLevel = null;
      RigManager = __instance;
      SafeInvoke("OnLevelStart", OnLevelStart, CurrentLevel);
    }
  }

  [HarmonyPatch(
      typeof(SceneStreamer), nameof(SceneStreamer.Load),
      new Type[] { typeof(LevelCrateReference), typeof(LevelCrateReference) })]
  class SceneStreamer_Load_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(LevelCrateReference level) {
      var nextLevel = level.Crate;
      Dbg.Log($"SceneStreamer_Load_Patch, next level = {nextLevel?.Title}");
      if (CurrentLevel)
        PrevLevel = CurrentLevel;
      CurrentLevel = null;
      NextLevel = nextLevel;
      RigManager = null;
      SafeInvoke("OnLoad", OnLoad, NextLevel);
    }
  }

  [HarmonyPatch(typeof(SceneStreamer), nameof(SceneStreamer.Reload))]
  class SceneStreamer_Reload_Patch {
    [HarmonyPrefix()]
    internal static void Prefix() {
      Dbg.Log("SceneStreamer_Reload_Patch");
      PrevLevel = CurrentLevel;
      CurrentLevel = null;
      if (CurrentLevel)
        NextLevel = CurrentLevel;
      RigManager = null;
      OnLoad?.Invoke(NextLevel);
    }
  }
}
}
