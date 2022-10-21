using MelonLoader;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SLZ.Marrow.Warehouse;
using SLZ.Bonelab;
using Sst.Utilities;

namespace Sst {
class SplitsTimer {
  private static SplitsTimer Instance;

  private TMPro.TextMeshPro _tmp;
  private Splits _splits = new Splits();
  private MelonPreferences_Entry<bool> _prefHide;

  // public readonly Pref<bool> PrefShowUnderWrist = new Pref<bool>() {
  //   Id = "showUnderWrist",
  //   Name = "Show timer under hand",
  //   Description = "Timer is invisible unless you point your wrist upwards.",
  //   DefaultValue = false,
  // };

  public SplitsTimer() {
    Instance = this;
    Livesplit.SetState(false, false);
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
      _tmp = splitsText.AddComponent<TMPro.TextMeshPro>();
      _tmp.alignment = TMPro.TextAlignmentOptions.BottomRight;
      _tmp.fontSize = 0.5f;
      _tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
      var rigManager = Bonelab.GetRigManager();
      splitsText.transform.SetParent(
          rigManager.ControllerRig.leftController.transform);
      // if (PrefShowUnderWrist.Read()) {
      //   // TODO
      //   _tmp.rectTransform.localPosition = new Vector3(-0.36f, 0.24f, 0f);
      //   _tmp.rectTransform.localRotation = Quaternion.Euler(46f, 356f, 3f);
      // } else {
      _tmp.rectTransform.localPosition = new Vector3(-0.36f, 0.24f, 0f);
      _tmp.rectTransform.localRotation = Quaternion.Euler(46f, 356f, 3f);
      // }
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
    Livesplit.SetState(false, true, Mod.Instance.CurrentLevel?.Title ?? "");
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
