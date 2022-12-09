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

  public SplitsTimer() {
    Instance = this;
    Livesplit.SetState(true, false);
  }

  public void OnInitialize() {
    _prefHide = Mod.Instance.PrefCategory.CreateEntry<bool>(
        "hide", false, "Hide in-game timer",
        "Stops the timer from displaying on your wrist. Does not hide loading screen timer.");
  }

  public void Reset() {
    _splits.Reset();
    Livesplit.SetState(false, false);
  }

  public void OnLoadingScreen(LevelCrate nextLevel) {
    if (nextLevel == null)
      return;

    Livesplit.SetState(true, false, nextLevel.Title);

    if (nextLevel.Title == Levels.TITLE_DESCENT) {
      Dbg.Log("Attempting to start timer");
      _splits.ResetAndPause(nextLevel);
    } else if (_splits.TimeStart.HasValue) {
      Dbg.Log("Splitting timer");
      _splits.Pause();
      _splits.Split(nextLevel);
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

    _splits.ResumeIfStarted();
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

  [HarmonyPatch(typeof(GameControl_Outro),
                nameof(GameControl_Outro.ReachedTaxi))]
  class GameControl_Outro_ReachedTaxi_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(GameControl_Outro __instance) {
      var taxiController =
          __instance.TaxiSequence.GetComponentInChildren<TaxiController>();
      if (taxiController != null) {
        taxiController.OnPlayerSeated.AddListener(
            new System.Action(Instance.Finish));
      } else {
        MelonLogger.Warning(
            "No TaxiController found. Split timer will not stop on finish.");
      }
    }
  }
}
}
