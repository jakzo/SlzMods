using System;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using TMPro;
using SLZ.Marrow.Warehouse;
using SLZ.Bonelab;
using Sst.Utilities;
using System.Collections.Generic;

namespace Sst.SpeedrunTimer {
class TimeTrialDisplayFix : MonoBehaviour {
  private static HashSet<string> TIME_TRIAL_LEVELS = new HashSet<string>() {
    Utilities.Levels.Barcodes.STREET_PUNCHER,
    Utilities.Levels.Barcodes.SPRINT_BRIDGE,
  };

  private static TimeTrial_GameController _controller;
  private static BoneLeaderManager _leaderboardManager;
  private static TextMeshPro _tmpLabel;

  public static void OnInitialize() { LevelHooks.OnLevelStart += OnLevelStart; }

  private static void OnLevelStart(LevelCrate level) {
    Dbg.Log($"TimeTrialDisplayFix OnLevelStart");

    if (!TIME_TRIAL_LEVELS.Contains(level.Barcode.ID))
      return;

    Dbg.Log($"TimeTrialDisplayFix TIME_TRIAL_LEVELS");

    _controller = GameObject.FindObjectOfType<TimeTrial_GameController>();
    if (_controller == null)
      return;

    _leaderboardManager = GameObject.FindObjectOfType<BoneLeaderManager>();
    if (_leaderboardManager == null)
      return;

    Dbg.Log($"TimeTrialDisplayFix DisplayTimeOnFinish");

    _controller.onSessionEnd.AddListener(new Action(OnSessionEnd));
  }

  public static void OnSessionEnd() {
    if (_controller == null || _leaderboardManager == null)
      return;

    Dbg.Log($"TimeTrialDisplayFix OnSessionEnd: {_controller.tsTimerString}");

    var leaderboardTitle = _leaderboardManager.text_TitleBoard;
    var leaderboardCanvas = leaderboardTitle.transform.parent;

    var label = new GameObject("TimeTrialDisplayFix_Label");
    label.transform.SetParent(leaderboardCanvas, false);
    label.transform.localPosition = new Vector3(0f, 0f, 3f);

    _tmpLabel = label.AddComponent<TextMeshPro>();
    _tmpLabel.alignment = TextAlignmentOptions.Center;
    _tmpLabel.font = leaderboardTitle.font;
    _tmpLabel.fontSize = 4f;
    _tmpLabel.rectTransform.sizeDelta = new Vector2(10f, 1f);
  }

  [HarmonyPatch(typeof(BaseGameController),
                nameof(BaseGameController.UpdateTimeDisplay))]
  class BaseGameController_UpdateTimeDisplay_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(BaseGameController __instance) {
      if (__instance == _controller && _tmpLabel != null) {
        _tmpLabel.SetText($"Time: {_controller.tsTimerString}");
      }
    }
  }
}
}
