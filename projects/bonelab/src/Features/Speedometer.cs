using UnityEngine;
using MelonLoader;

namespace SpeedrunTools.Features {
class SplitsTimer : Feature {
  private const string SPLITS_TEXT_NAME = "SpeedrunTools_Splits_Text";

  private TMPro.TextMeshPro _tmp;
  private Splits _splits;

  public override void OnLevelStart(int sceneIdx) {
    var windowDuration = PrefWindowDuration.Read();
    _speedLogger = new SpeedLogger() {
      WindowDuration = windowDuration,
      BufferSize = Mathf.CeilToInt(windowDuration * 240),
    };

    var speedometerText = GameObject.Find(SPLITS_TEXT_NAME);

    if (speedometerText == null) {
      speedometerText = new GameObject(SPLITS_TEXT_NAME);
      _tmp = speedometerText.AddComponent<TMPro.TextMeshPro>();
      _tmp.alignment = TMPro.TextAlignmentOptions.BottomRight;
      _tmp.fontSize = 0.5f;
      _tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
      speedometerText.transform.SetParent(
          Mod.GameState.rigManager.ControllerRig.leftController.transform);
      _tmp.rectTransform.localPosition = new Vector3(-0.36f, 0.24f, 0f);
      _tmp.rectTransform.localRotation = Quaternion.Euler(46f, 356f, 3f);
    } else {
      _tmp = speedometerText.GetComponent<TMPro.TextMeshPro>();
    }
  }

  public override void OnUpdate() {
    if (_speedLogger == null)
      return;
    var pos = Camera.main.transform.position;
    _speedLogger.OnFrame(Time.time, pos.x, pos.z);
    _tmp?.SetText($"{_speedLogger.GetSpeed():N2}m/s");
  }
}

class Splits {
  public List<Split> Splits;
  public float timeStart;
  public float timeEnd;
  public float timeStartRelative;
}

class Split {
  public Level Level;
  public string DisplayName;
  public float timeStart;
  public float timeEnd;
}
}
