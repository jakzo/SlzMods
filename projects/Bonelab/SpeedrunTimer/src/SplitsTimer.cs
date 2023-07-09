using MelonLoader;
using HarmonyLib;
using UnityEngine;
using TMPro;
using SLZ.Marrow.Warehouse;
using SLZ.Bonelab;
using Sst.Utilities;

namespace Sst.SpeedrunTimer {
class SplitsTimer {
  private static SplitsTimer Instance;

  private TextMeshPro _tmp;
  private Splits _splits = new Splits();
  private MelonPreferences_Entry<bool> _prefHide;
  private MelonPreferences_Entry<bool> _prefIl;

  public SplitsTimer() {
    Instance = this;
    Livesplit.SetState(true, false);
  }

  public void OnInitialize() {
    _prefHide = Mod.Instance.PrefCategory.CreateEntry<bool>(
        "hide", false, "Hide in-game timer",
        "Stops the timer from displaying on your wrist. Does not hide loading screen timer.");
    _prefIl = Mod.Instance.PrefCategory.CreateEntry<bool>(
        "il", false, "Time individual levels",
        "Resets the timer at every level instead of timing whole campaign.");
  }

  public void Reset() {
    _splits.Reset();
    Livesplit.SetState(false, false);
  }

  public void OnLoadingScreen(LevelCrate nextLevel) {
    if (nextLevel == null)
      return;

    Livesplit.SetState(true, false, nextLevel.Title);

    if (_prefIl.Value) {
      if (_splits.TimeStart.HasValue)
        _splits.Pause();
    } else {
      if (nextLevel.Barcode == Levels.Barcodes.DESCENT) {
        Dbg.Log("Attempting to start timer");
        _splits.ResetAndPause(nextLevel);
      } else if (_splits.TimeStart.HasValue) {
        Dbg.Log("Splitting timer");
        _splits.Pause();
        _splits.Split(nextLevel);
      }
    }

    var time = _splits.GetTime();
    if (time.HasValue) {
      SplitsRenderer.RenderLoadingWatermark(time.Value);
      SplitsRenderer.RenderSplits(_splits);
    }
  }

  public void OnLevelStart(LevelCrate level) {
    Livesplit.SetState(false, false, level.Title);

    if (!_prefHide.Value) {
      var splitsText = new GameObject("SpeedrunTimer_Wrist_Text");
      _tmp = splitsText.AddComponent<TextMeshPro>();
      _tmp.alignment = TextAlignmentOptions.BottomRight;
      _tmp.fontSize = 0.5f;
      _tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
      Utilities.Bonelab.DockToWrist(_tmp.gameObject);
    }

    if (_prefIl.Value) {
      _splits.Reset();
      if (Levels.IsMenu(level.Barcode)) {
        _splits.Reset();
      } else {
        _splits.ResetAndStart(level);
      }
    } else {
      _splits.ResumeIfStarted();
    }
  }

  public void OnUpdate() {
    if (_tmp == null)
      return;
    var time = _splits.GetTime();
    if (time == null)
      return;
    // if (PrefShowUnderWrist.Read()) {
    //   // TODO
    //   var isHidden = false;
    //   if (isHidden) {
    //     _tmp.gameObject.active = false;
    //     return;
    //   }
    // }
    _tmp.gameObject.active = true;
    _tmp.SetText(SplitsRenderer.DurationToString(time.Value));
  }

  public void Finish() {
    Livesplit.SetState(false, true, LevelHooks.CurrentLevel?.Title ?? "");
    _splits.Pause();
    MelonLogger.Msg($"Stopping timer at: {_splits.GetTime()}");
    if (_tmp != null)
      _tmp.color = new Color(0.2f, 0.8f, 0.2f);
  }

  [HarmonyPatch(typeof(TaxiController), nameof(TaxiController.Start))]
  class TaxiController_Start_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(TaxiController __instance) {
      Dbg.Log("TaxiController_Start_Patch");
      __instance.OnPlayerSeated.AddListener(new System.Action(Instance.Finish));
    }
  }
}
}
