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

  private static bool IsDisabled = false;

  public Timer() {
    IsAllowedInRuns = true;
    IsEnabledByDefault = false;
  }

  public readonly Hotkey HotkeyTeleport =
      new Hotkey() { Predicate = (cl, cr) => cr.GetThumbStick(),
                     Handler = () => { IsDisabled = !IsDisabled; } };

  public override void OnLevelStart(int sceneIdx) {
    var timerText = GameObject.Find(TIMER_TEXT_NAME);

    if (timerText == null) {
      timerText = new GameObject(TIMER_TEXT_NAME);
      _tmp = timerText.AddComponent<TMPro.TextMeshPro>();
      _tmp.alignment = TMPro.TextAlignmentOptions.BottomRight;
      _tmp.fontSize = 0.5f;
      _tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
      var rigManager =
          GameObject.FindObjectOfType<StressLevelZero.Rig.RigManager>();
      timerText.transform.SetParent(
          rigManager.ControllerRig.leftController.transform);
      _tmp.rectTransform.localPosition = new Vector3(-0.36f, 0.24f, 0f);
      _tmp.rectTransform.localRotation = Quaternion.Euler(46f, 356f, 3f);
    } else {
      _tmp = timerText.GetComponent<TMPro.TextMeshPro>();
    }
  }

  public override void OnUpdate() {
    if (IsDisabled)
      return;
    var duration = Speedrun.Instance.RunTimer.CalculateDuration();
    if (!duration.HasValue)
      return;
    _tmp?.SetText(duration.Value.ToString(
        $"{(duration.Value.Seconds >= 60 * 60 ? "h\\:m" : "")}m\\:ss\\.ff"));
  }
}
}
