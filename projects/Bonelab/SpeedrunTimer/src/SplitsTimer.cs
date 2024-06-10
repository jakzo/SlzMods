using System;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Sst.Utilities;

#if ML6
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppTMPro;
#else
using SLZ.Bonelab;
using SLZ.Marrow.Warehouse;
using TMPro;
#endif

namespace Sst.SpeedrunTimer {
class SplitsTimer {
  private static Color FINISH_COLOR = new Color(0.2f, 0.8f, 0.2f);

  private static SplitsTimer Instance;

  private TextMeshPro _tmp;
  private TextMeshPro _tmpIl;
  private Splits _splits = new Splits();
  private bool _isFinished = false;
  private static MelonPreferences_Entry<bool> _prefHide;
  private static MelonPreferences_Entry<bool> _prefHideIl;
  private static MelonPreferences_Entry<bool> _prefHideSplits;

  public SplitsTimer() {
    Instance = this;
    Livesplit.SetState(true, false);
  }

  public static void OnInitialize() {
    _prefHide = Mod.Instance.PrefCategory.CreateEntry(
        "hide", false, "Hide in-game timer",
        "Stops the timer from displaying on your wrist. Does not hide loading screen timer.");
    _prefHideIl = Mod.Instance.PrefCategory.CreateEntry(
        "hideIl", false, "Hide level timer",
        "Stops the individual level timer from displaying on your wrist.");
    _prefHideSplits = Mod.Instance.PrefCategory.CreateEntry(
        "hideSplits", false, "Hides split display in loading screen",
        "Stops the individual level times from displaying in the loading screen.");
  }

  public void Reset() {
    _isFinished = false;
    _splits.Reset();
    Mod.Instance.SplitsServer?.Reset();
    Livesplit.SetState(false, false);
  }

  public void OnLoadingScreen(LevelCrate nextLevel) {
    if (nextLevel == null)
      return;

    if (_isFinished) {
      _splits.Reset();
      _isFinished = false;
    }

    if (nextLevel.Barcode == Levels.LabworksBarcodes.MAIN_MENU &&
        LevelHooks.PrevLevel.Barcode == Levels.LabworksBarcodes.THRONE_ROOM) {
      _splits.Split(null);
      Finish();
    } else {
      Livesplit.SetState(true, false, nextLevel.Title);

      if (nextLevel.Barcode == Levels.Barcodes.DESCENT ||
          nextLevel.Barcode == Levels.LabworksBarcodes.BREAKROOM) {
        Dbg.Log("Attempting to start timer");
        Mod.Instance.SplitsServer?.Start();
        _splits.ResetAndPause(nextLevel);
      } else if (_splits.TimeStart.HasValue) {
        Dbg.Log("Splitting timer");
        Mod.Instance.SplitsServer?.Split();
        _splits.Pause();
        _splits.Split(nextLevel);
      }
    }

    var time = _splits.GetTime();
    if (time.HasValue) {
      Mod.Instance.SplitsServer?.PauseGameTime();
      Mod.Instance.SplitsServer?.SetGameTime(time.Value);

      SplitsRenderer.RenderLoadingWatermark(time.Value);
      if (!_prefHideSplits.Value)
        SplitsRenderer.RenderSplits(_splits);
    }
  }

  public void OnLevelStart(LevelCrate level) {
    Livesplit.SetState(false, false, level.Title);

    Mod.Instance.SplitsServer?.ResumeGameTime();

    if (!_isFinished)
      _splits.ResumeIfStarted();

    AddTimerToWrist();
  }

  private void AddTimerToWrist() {
    if (_prefHide.Value)
      return;

    var splitsText = new GameObject("SpeedrunTimer_Wrist_Text");
    _tmp = splitsText.AddComponent<TextMeshPro>();
    _tmp.alignment = TextAlignmentOptions.BottomRight;
    _tmp.fontSize = 0.5f;
    _tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
    if (_isFinished)
      _tmp.color = FINISH_COLOR;
    Bonelab.DockToWrist(splitsText);

    if (_prefHideIl.Value)
      return;

    var ilText = new GameObject("SpeedrunTimer_Wrist_IL");
    var ilTextOffset = new GameObject("SpeedrunTimer_Wrist_IL_Offset");
    ilTextOffset.transform.parent = ilText.transform;
    _tmpIl = ilTextOffset.AddComponent<TextMeshPro>();
    _tmpIl.alignment = TextAlignmentOptions.BottomRight;
    _tmpIl.fontSize = 0.3f;
    _tmpIl.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
    _tmpIl.rectTransform.localPosition = new Vector3(-0.005f, -0.03f, 0.005f);
    _tmpIl.color = new Color(0.2f, 0.2f, 0.2f);
    Bonelab.DockToWrist(ilText);
  }

  public void OnUpdate() {
    if (_tmp != null) {
      var time = _splits.GetTime();
      if (time == null)
        return;
      _tmp.gameObject.active = true;
      _tmp.SetText(SplitsRenderer.DurationToString(time.Value));
    }
    if (_tmpIl != null) {
      var time = _splits.GetCurrentSplitTime();
      if (time == null)
        return;
      _tmpIl.gameObject.active = true;
      _tmpIl.SetText(SplitsRenderer.DurationToString(time.Value));
    }
  }

  public void Finish() {
    Livesplit.SetState(false, true, LevelHooks.CurrentLevel?.Title ?? "");
    _splits.Pause();
    Mod.Instance.SplitsServer?.Split();
    Mod.Instance.SplitsServer?.SetGameTime(_splits.GetTime().Value);
    _isFinished = true;
    MelonLogger.Msg($"Stopping timer at: {_splits.GetTime()}");
    if (_tmp != null)
      _tmp.color = FINISH_COLOR;
  }

  [HarmonyPatch(typeof(TaxiController), nameof(TaxiController.Start))]
  class TaxiController_Start_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(TaxiController __instance) {
      Dbg.Log("TaxiController_Start_Patch");
      __instance.OnPlayerSeated.AddListener(new Action(Instance.Finish));
    }
  }
}
}
