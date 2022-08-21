using MelonLoader;
using UnityEngine;
using HarmonyLib;

namespace SpeedrunTools.Features {
// TODO: Try this approach
// https://docs.unity3d.com/ScriptReference/Font.RequestCharactersInTexture.html
// TODO: I think the optimal solution is to use shader code to copy characters
//       directly from the font texture to a texture
class Timer : Feature {
  private const string TIMER_TEXT_NAME = "SpeedrunTools_Timer_Text";

  private GameObject _timerText;
  private TMPro.TextMeshPro _tmp;
  private Speedruns.RunTimer _runTimer = new Speedruns.RunTimer();

  public override void OnLevelStart(int sceneIdx) {
    var timerText = GameObject.Find(TIMER_TEXT_NAME);

    if (timerText == null) {
      timerText = new GameObject(TIMER_TEXT_NAME);
      var tmp = timerText.AddComponent<TMPro.TextMeshPro>();
      tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
      tmp.fontSize = 1.5f;
      tmp.rectTransform.sizeDelta = new Vector2(2, 2);
      tmp.rectTransform.position = new Vector3(2.65f, 1.8f, 9.6f);
    }

    _timerText = timerText;
    _tmp = _timerText.GetComponent<TMPro.TextMeshPro>();
  }

  public override void OnUpdate() { _tmp.SetText(_runTimer.Duration); }
}
}
