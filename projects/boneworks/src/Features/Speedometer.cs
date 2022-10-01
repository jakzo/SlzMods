using UnityEngine;
using MelonLoader;

namespace SpeedrunTools.Features {
class Speedometer : Feature {
  private const string SPEEDOMETER_TEXT_NAME = "SpeedrunTools_Speedometer_Text";

  private TMPro.TextMeshPro _tmp;
  private SpeedLogger _speedLogger;

  public readonly Pref<float> PrefWindowDuration = new Pref<float>() {
    Id = "speedometerWindowDuration",
    Name =
        "The speedometer shows the average speed over the specified duration",
    DefaultValue = 2.0f
  };

  public Speedometer() { IsDev = true; }

  public override void OnLevelStart(int sceneIdx) {
    var windowDuration = PrefWindowDuration.Read();
    _speedLogger = new SpeedLogger() { WindowDuration = windowDuration,
                                       BufferSize = Mathf.CeilToInt(
                                           windowDuration * 240) };

    var speedometerText = GameObject.Find(SPEEDOMETER_TEXT_NAME);

    if (speedometerText == null) {
      speedometerText = new GameObject(SPEEDOMETER_TEXT_NAME);
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

class SpeedLogger {
  public int BufferSize = 1000;
  public float WindowDuration = 1;

  private (float, float, float)[] _frames;
  private int _idxStart = 0;
  private int _idxEnd = 0;

  public SpeedLogger() { _frames = new(float, float, float)[BufferSize]; }

  public void OnFrame(float time, float posX, float posZ) {
    _frames[_idxEnd] = (time, posX, posZ);
    _idxEnd++;
    if (_idxEnd >= _frames.Length)
      _idxEnd = 0;
    if (_idxEnd == _idxStart) {
      _idxStart++;
      if (_idxStart >= _frames.Length)
        _idxStart = 0;
    }
  }

  public float GetSpeed() {
    if (_idxEnd == _idxStart)
      return 0;
    var frameEnd = _frames[(_idxEnd <= 0 ? _frames.Length : _idxEnd) - 1];
    var windowStart = frameEnd.Item1 - WindowDuration;
    while (_idxStart != _idxEnd && _frames[_idxStart].Item1 < windowStart) {
      _idxStart++;
      if (_idxStart >= _frames.Length)
        _idxStart = 0;
    }
    var frameStart = _frames[_idxStart];
    var dx = frameEnd.Item2 - frameStart.Item2;
    var dz = frameEnd.Item3 - frameStart.Item3;
    var duration = frameEnd.Item1 - frameStart.Item1;
    return Mathf.Sqrt(dx * dx + dz * dz) / duration;
  }
}
}
