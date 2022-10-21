using MelonLoader;
using System;
using System.Collections.Generic;
using HarmonyLib;
using SLZ.Marrow.SceneStreaming;
using SLZ.Marrow.Warehouse;
using SLZ.Rig;
using Sst.Utilities;
using System.Linq;

namespace Sst {
public class Mod : MelonMod {
  public const string PREF_CATEGORY_ID = BuildInfo.Name;

  private static SplitsTimer _timer = new SplitsTimer();
  private bool _isDisabled = false;
  private LoadingScene _activeLoadingScene;

  public MelonPreferences_Category PrefCategory;
  public LevelCrate CurrentLevel;
  public LevelCrate NextLevel;

  public static Mod Instance;
  public Mod() { Instance = this; }

  public override void OnInitializeMelon() {
    Dbg.Init(PREF_CATEGORY_ID);
    PrefCategory = MelonPreferences.CreateCategory(PREF_CATEGORY_ID);
    _timer.OnInitialize();
  }

  public override void OnUpdate() {
    if (_isDisabled)
      return;

    if (_activeLoadingScene != null &&
        !_activeLoadingScene.gameObject.scene.isLoaded) {
      Dbg.Log("loading scene unloaded");
      _activeLoadingScene = null;

      if (CurrentLevel == null && NextLevel != null)
        DoLevelStart();
    }

    _timer.OnUpdate();
  }

  private void DoLevelStart() {
    CheckIfAllowed();
    if (_isDisabled)
      return;

    CurrentLevel = NextLevel;
    NextLevel = null;
    _timer.OnLevelStart(CurrentLevel);
  }

  private void CheckIfAllowed() {
    var illegitimacyReasons = AntiCheat.ComputeRunLegitimacy();
    if (illegitimacyReasons.Count == 0) {
      _isDisabled = false;
      return;
    }

    if (_isDisabled)
      return;

    _timer.Reset();
    _isDisabled = true;
    var reasonMessages = string.Join(
        "", illegitimacyReasons.Select(reason => $"\n» {reason.Value}"));
    MelonLogger.Msg(
        $"Cannot show timer due to run being illegitimate because:{reasonMessages}");
  }

  [HarmonyPatch(typeof(SceneStreamer), nameof(SceneStreamer.Load),
                new System.Type[] { typeof(LevelCrateReference),
                                    typeof(LevelCrateReference) })]
  class SceneStreamer_Load_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(LevelCrateReference level) {
      Dbg.Log($"Load: {level.Crate.Title}");
      Mod.Instance.NextLevel = level.Crate;
    }
  }

  [HarmonyPatch(typeof(LoadingScene), nameof(LoadingScene.Start))]
  class LoadingScene_Start_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(LoadingScene __instance) {
      Dbg.Log("LoadingScene_Start_Patch");
      Mod.Instance.CheckIfAllowed();
      if (Mod.Instance._isDisabled)
        return;

      Instance._activeLoadingScene = __instance;
      Mod.Instance.CurrentLevel = null;
      _timer.OnLoadingScreen(Mod.Instance.NextLevel);
    }
  }
}
}
