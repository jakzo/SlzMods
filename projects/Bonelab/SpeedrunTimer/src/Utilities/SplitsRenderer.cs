using System;
using UnityEngine;
using MelonLoader;
using System.Linq;
using Sst.Utilities;
using System.Text.RegularExpressions;

#if ML6
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Sst.SpeedrunTimer {
class SplitsRenderer {
  private static float SPLITS_WIDTH = 0.3f;
  private static float SPLITS_LINE_HEIGHT = 0.05f;
  private static float SPLITS_LEFT = -0.1f;
  private static float SPLITS_FONT_SIZE = 0.3f;

  public static void RenderLoadingWatermark(TimeSpan time) {
    Dbg.Log("RenderLoadingWatermark");
    var head = LevelHooks.BasicTrackingRig?.head;
    if (!head) {
      MelonLogger.Warning("Failed to render watermark in loading screen " +
                          "because could not find head position");
      return;
    }
    var text = CreateText(head, "Watermark");
    text.alignment = TextAlignmentOptions.TopRight;
    text.fontSize = 0.5f;
    text.rectTransform.sizeDelta = new Vector2(0.8f, 0.8f);
    text.rectTransform.localPosition = new Vector3(0f, 0f, 1f);
    text.color = new Color(0.4f, 0.4f, 0.6f);
    var debugText = "";
#if DEBUG
    debugText = " DEBUG";
#endif
    var timeStr = DurationToString(time);
    var modNames = string.Join(
        "\n", MelonMod.RegisteredMelons.Select(mod => mod.Info.Name)
    );
    text.SetText(
        $"{BuildInfo.Name} v{AppVersion.Value}{debugText}\n{timeStr}\n\nMods:\n{modNames}"
    );
  }

  public static void RenderSplits(Splits splits) {
    var head = LevelHooks.BasicTrackingRig?.head;
    if (!head)
      return;
    var container = new GameObject($"{BuildInfo.Name}_Splits");
    container.transform.SetParent(head.transform);
    container.transform.localPosition = new Vector3(-0.4f, 0.25f, 1f);
    for (var i = 0; i < splits.Items.Count; i++) {
      var split = splits.Items[i];
      var top = i * -SPLITS_LINE_HEIGHT;

      var timeTextWidth = 0f;
      if (split.Duration.HasValue) {
        var timeText =
            CreateText(container.transform, $"Splits_Time_{split.Name}");
        var format = split.Duration.Value >= TimeSpan.FromHours(1)
            ? "h\\:mm\\:ss"
            : "m\\:ss\\.f";
        timeText.SetText(split.Duration.Value.ToString(format));
        timeText.alignment = TextAlignmentOptions.Right;
        timeText.overflowMode = TextOverflowModes.Truncate;
        timeText.rectTransform.localPosition = new Vector3(0f, 0f, 0f);
        timeText.rectTransform.anchorMin = timeText.rectTransform.anchorMax =
            new Vector2(0f, 0.5f);
        timeText.rectTransform.offsetMin = new Vector2(SPLITS_LEFT, top);
        timeText.rectTransform.offsetMax =
            new Vector2(SPLITS_WIDTH + SPLITS_LEFT, top + SPLITS_LINE_HEIGHT);
        timeText.fontSize = SPLITS_FONT_SIZE;
        timeText.ForceMeshUpdate();
        timeTextWidth = timeText.preferredWidth;
      }

      var nameText =
          CreateText(container.transform, $"Splits_Name_{split.Name}");
      var cleanedName = Regex.Replace(
          split.Name, @"^boneworks(?:_\d+)?\s+", "", RegexOptions.IgnoreCase
      );
      nameText.SetText(cleanedName);
      nameText.alignment = TextAlignmentOptions.Left;
      nameText.overflowMode = TextOverflowModes.Truncate;
      nameText.rectTransform.localPosition = new Vector3(0f, 0f, 0f);
      nameText.rectTransform.anchorMin = nameText.rectTransform.anchorMax =
          new Vector2(0f, 0.5f);
      nameText.rectTransform.offsetMin = new Vector2(SPLITS_LEFT, top);
      nameText.rectTransform.offsetMax = new Vector2(
          SPLITS_WIDTH + SPLITS_LEFT - timeTextWidth, top + SPLITS_LINE_HEIGHT
      );
      nameText.fontSize = SPLITS_FONT_SIZE;
    }
  }

  public static string DurationToString(TimeSpan duration
  ) => duration.ToString($"{(duration.Hours >= 1 ? "h\\:m" : "")}m\\:ss\\.ff");

  private static TextMeshPro CreateText(Transform parent, string name) {
    var go = new GameObject($"{BuildInfo.Name}_{name}");
    go.layer = LayerMask.NameToLayer("Background");
    var tmp = go.AddComponent<TextMeshPro>();
    tmp.transform.SetParent(parent);
    tmp.sortingOrder = 100;
    tmp.color = new Color(0.5f, 0.5f, 0.5f);
    return tmp;
  }
}
}
