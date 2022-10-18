using UnityEngine;

namespace Sst.Utilities {
class SplitsRenderer {
  public static void RenderLoadingWatermark(System.TimeSpan time) {
    var splitsText = new GameObject($"{BuildInfo.Name}_Watermark");
    splitsText.layer = LayerMask.NameToLayer("Background");
    var tmp = splitsText.AddComponent<TMPro.TextMeshPro>();
    tmp.alignment = TMPro.TextAlignmentOptions.TopRight;
    tmp.fontSize = 0.5f;
    tmp.transform.SetParent(GameObject.Find("Main Camera").transform);
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

  public static string
  DurationToString(System.TimeSpan duration) => duration.ToString(
      $"{(duration.Seconds >= 60 * 60 ? "h\\:m" : "")}m\\:ss\\.ff");
}
}
