using System;
using UnityEngine;
using TMPro;

namespace Sst.SpeedrunTimer {
class SplitsRenderer {
  public static void RenderLoadingWatermark(TimeSpan time) {
    var camera = GameObject.Find("Main Camera");
    if (!camera)
      return;
    var splitsText = new GameObject($"{BuildInfo.Name}_Watermark");
    splitsText.layer = LayerMask.NameToLayer("Background");
    var tmp = splitsText.AddComponent<TextMeshPro>();
    tmp.alignment = TextAlignmentOptions.TopRight;
    tmp.fontSize = 0.5f;
    tmp.transform.SetParent(camera.transform);
    tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.8f);
    tmp.rectTransform.localPosition = new Vector3(0, 0, 1);
    tmp.rectTransform.localRotation = Quaternion.Euler(0, 0, 0);
    var debugText = "";
#if DEBUG
    debugText = " DEBUG";
#endif
    tmp.SetText(
        $"{BuildInfo.Name} v{AppVersion.Value}{debugText}\n{DurationToString(time)}");
  }

  public static void RenderSplits(Splits splits) {
    // TODO
  }

  public static string DurationToString(TimeSpan duration) =>
      duration.ToString($"{(duration.Hours >= 1 ? "h\\:m" : "")}m\\:ss\\.ff");
}
}
