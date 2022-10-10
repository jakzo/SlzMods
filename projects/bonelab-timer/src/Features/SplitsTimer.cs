using MelonLoader;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using SLZ.Marrow.Warehouse;
using SLZ.Bonelab;

namespace Sst.Features {
class SplitsTimer : Feature {
  private static SplitsTimer Instance;

  private TMPro.TextMeshPro _tmp;
  private Splits _splits = new Splits();

  public readonly Pref<bool> PrefHide = new Pref<bool>() {
    Id = "hide",
    Name = "Hide in-game timer",
    Description =
        "Hides the timer on your wrist. Does not hide loading screen timer.",
    DefaultValue = false,
  };

  // public readonly Pref<bool> PrefShowUnderWrist = new Pref<bool>() {
  //   Id = "showUnderWrist",
  //   Name = "Show timer under hand",
  //   Description = "Timer is invisible unless you point your wrist upwards.",
  //   DefaultValue = false,
  // };

  public SplitsTimer() { Instance = this; }

  private bool IsAllowed() {
    var illegitimacyReasons = Utilities.AntiCheat.ComputeRunLegitimacy();
    if (illegitimacyReasons.Count == 0)
      return true;

    var reasonMessages = string.Join(
        "", illegitimacyReasons.Select(reason => $"\n» {reason.Value}"));
    MelonLogger.Msg(
        $"Cannot show timer due to run being illegitimate because:{reasonMessages}");
    return false;
  }

  public override void OnLoadingScreen(LevelCrate prevLevel,
                                       LevelCrate nextLevel) {
    if (nextLevel == null)
      return;

    if (nextLevel.Title == Utils.LEVEL_TITLE_DESCENT) {
      Utils.LogDebug("Attempting to start timer");
      if (IsAllowed())
        _splits.ResetAndPause(nextLevel);
    } else if (_splits.TimeStart.HasValue) {
      Utils.LogDebug("Splitting timer");
      _splits.Pause();
      _splits.Split(nextLevel);
    }

    var time = _splits.GetTime();
    if (time.HasValue) {
      RenderLoadingWatermark(time.Value);
      RenderSplits(_splits);
    }
  }

  private void RenderLoadingWatermark(System.TimeSpan time) {
    var splitsText = new GameObject("SpeedrunTimer_Watermark");
    splitsText.layer = LayerMask.NameToLayer("Background");
    var tmp = splitsText.AddComponent<TMPro.TextMeshPro>();
    tmp.alignment = TMPro.TextAlignmentOptions.TopRight;
    tmp.fontSize = 0.5f;
    tmp.transform.SetParent(GameObject.Find("Main Camera").transform);
    tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.8f);
    tmp.rectTransform.localPosition = new Vector3(0, 0, 1);
    tmp.rectTransform.localRotation = Quaternion.Euler(0, 0, 0);
    tmp.SetText(
        $"{BuildInfo.Name} v{BuildInfo.Version}\n{Utils.DurationToString(time)}");
  }

  private void RenderSplits(Splits splits) {}

  public override void OnLevelStart(LevelCrate level) {
    if (!PrefHide.Read()) {
      var splitsText = new GameObject("SpeedrunTimer_Wrist_Text");
      _tmp = splitsText.AddComponent<TMPro.TextMeshPro>();
      _tmp.alignment = TMPro.TextAlignmentOptions.BottomRight;
      _tmp.fontSize = 0.5f;
      _tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
      splitsText.transform.SetParent(
          Mod.GameState.rigManager.ControllerRig.leftController.transform);
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

  public override void OnUpdate() {
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
    _tmp.SetText(Utils.DurationToString(time.Value));
  }

  public void Finish() {
    _splits.Pause();
    MelonLogger.Msg($"Stopping timer at: {_splits.GetTime()}");
    if (_tmp != null)
      _tmp.color = new Color(0.2f, 0.8f, 0.2f);
  }

  private class Splits {
    public List<Split> Items = new List<Split>();
    public System.DateTime? TimeStart;
    public System.DateTime? TimeEnd;
    public System.DateTime? TimeStartRelative;
    public System.DateTime? TimePause;
    public System.DateTime TimeLastSplitStartRelative = System.DateTime.Now;

    public void ResetAndPause(LevelCrate firstLevel) {
      ResetAndStart(firstLevel);
      TimePause = TimeStart;
    }

    public void ResetAndStart(LevelCrate firstLevel) {
      var now = System.DateTime.Now;
      TimeEnd = TimePause = null;
      TimeStart = TimeStartRelative = TimeLastSplitStartRelative = now;
      Items = new List<Split>() {
        new Split() {
          Level = firstLevel,
          Name = firstLevel.name,
          TimeStart = now,
        },
      };
    }

    public void Pause() { TimePause = System.DateTime.Now; }

    public void ResumeIfStarted() {
      if (TimePause == null)
        return;
      var delta = System.DateTime.Now - TimePause.Value;
      TimeStartRelative += delta;
      TimeLastSplitStartRelative += delta;
      TimePause = null;
    }

    public System.TimeSpan? GetTime() =>
        (TimeEnd ?? TimePause ?? System.DateTime.Now) - TimeStartRelative;

    public void Split(LevelCrate nextLevel) {
      var lastItem = Items[Items.Count - 1];
      var now = System.DateTime.Now;
      lastItem.TimeEnd = now;
      var splitTimeRelative = TimePause ?? now;
      lastItem.Duration = splitTimeRelative - TimeLastSplitStartRelative;
      TimeLastSplitStartRelative = splitTimeRelative;
      Items.Add(new Split() {
        Level = nextLevel,
        Name = nextLevel.name,
        TimeStart = now,
      });
    }
  }

  private class Split {
    public LevelCrate Level;
    public string Name;
    public System.DateTime? TimeStart;
    public System.DateTime? TimeEnd;
    public System.TimeSpan? Duration;
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
