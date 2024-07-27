using System;
using HarmonyLib;
using UnityEngine;

#if ML6
using Il2CppSLZ.Bonelab;
using Il2CppTMPro;
#else
using SLZ.Bonelab;
using TMPro;
#endif

namespace Sst.SpeedrunTimer {
class TimeTrialDisplayFix {
  public const string LABEL_NAME = "TimeTrialDisplayFix_Label";

  [HarmonyPatch(
      typeof(BoneLeaderManager),
      nameof(BoneLeaderManager.SubmitLeaderboardScore)
  )]
  class BoneLeaderManager_SubmitLeaderboardScore_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(BoneLeaderManager __instance, uint score) =>
        DisplayTime(__instance, score);
  }

  public static void
  DisplayTime(BoneLeaderManager leaderManager, uint milliseconds) {
    var leaderboardTitle = leaderManager.text_TitleBoard;
    var leaderboardCanvas = leaderboardTitle.transform.parent;

    var tmp = leaderboardCanvas.Find(LABEL_NAME)?.GetComponent<TextMeshPro>() ??
        CreateTimeDisplay(leaderboardCanvas, leaderboardTitle);

    var time =
        SplitsRenderer.DurationToString(TimeSpan.FromMilliseconds(milliseconds)
        );
    tmp.SetText($"Time: {time}");
  }

  private static TextMeshPro
  CreateTimeDisplay(Transform canvas, TMP_Text title) {
    var go = new GameObject(LABEL_NAME);
    go.transform.SetParent(canvas, false);
    go.transform.localPosition = new Vector3(0f, 1.2f, 3f);

    var tmp = go.AddComponent<TextMeshPro>();
    tmp.alignment = TextAlignmentOptions.Center;
    tmp.font = title.font;
    tmp.fontSize = 4f;
    tmp.rectTransform.sizeDelta = new Vector2(10f, 1f);
    return tmp;
  }
}
}
