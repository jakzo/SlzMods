using UnityEngine;
using MelonLoader;

namespace SpeedrunTools.Features {
// TODO: Try this approach
// https://docs.unity3d.com/ScriptReference/Font.RequestCharactersInTexture.html
// TODO: I think the optimal solution is to use shader code to copy characters
//       directly from the font texture to a texture
class Timer : Feature {
  private const string TIMER_TEXT_NAME = "SpeedrunTools_Timer_Text";

  private TMPro.TextMeshPro _tmp;

  public Timer() {
    IsAllowedInRuns = true;
    IsDev = true;
  }

  public override void OnLevelStart(int sceneIdx) {
    var timerText = GameObject.Find(TIMER_TEXT_NAME);

    if (timerText == null) {
      timerText = new GameObject(TIMER_TEXT_NAME);
      _tmp = timerText.AddComponent<TMPro.TextMeshPro>();
      _tmp.alignment = TMPro.TextAlignmentOptions.BottomRight;
      _tmp.fontSize = 0.5f;
      _tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
      timerText.transform.SetParent(
          Mod.GameState.rigManager.ControllerRig.leftController.transform);
      _tmp.rectTransform.localPosition = new Vector3(-0.36f, 0.24f, 0f);
      _tmp.rectTransform.localRotation = Quaternion.Euler(46f, 356f, 3f);
    } else {
      _tmp = timerText.GetComponent<TMPro.TextMeshPro>();
    }
  }

  public override void OnUpdate() {
    var duration = Speedrun.Instance.RunTimer.CalculateDuration();
    if (!duration.HasValue)
      return;
    _tmp?.SetText(duration.Value.ToString(
        $"{(duration.Value.Seconds >= 60 * 60 ? "h\\:m" : "")}m\\:ss\\.ff"));
  }
}
}
