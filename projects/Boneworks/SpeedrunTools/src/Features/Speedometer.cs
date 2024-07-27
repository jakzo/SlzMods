using UnityEngine;
using System;
using System.Collections.Generic;

namespace Sst.Features {
class Speedometer : Feature {
  private static readonly Func<Vector3, Vector3, float>[] _calculations = {
    (pos1, pos2) =>
        (new Vector3(pos2.x, 0f, pos2.z) - new Vector3(pos1.x, 0f, pos1.z))
            .magnitude,

#if DEBUG
    (pos1, pos2) => pos1.y - pos2.y,
    (pos1, pos2) => (pos2 - pos1).magnitude,
#endif
  };

  public readonly Pref<float> PrefWindowDuration = new Pref<float>() {
    Id = "speedometerWindowDuration",
    Name =
        "The speedometer shows the average speed over the specified duration",
    DefaultValue = 2.0f,
  };

  public readonly Pref<bool> PrefIgnoreHeight = new Pref<bool>() {
    Id = "speedometerIgnoreHeight",
    Name = "Ignore speed in up/down directions",
    DefaultValue = true,
  };

  private readonly List<SpeedDisplay> _speedDisplays = new List<SpeedDisplay>();

  public Speedometer() { IsDev = true; }

  public override void OnLevelStart(int sceneIdx) {
    _speedDisplays.Clear();

    for (var i = 0; i < _calculations.Length; i++) {
      _speedDisplays.Add(new SpeedDisplay(
          PrefWindowDuration.Read(),
          new Vector3(-0.36f, 0.24f + 0.03f * i, 0f + 0.03f * i),
          _calculations[i]
      ));
    }
  }

  public override void OnUpdate() {
    foreach (var speedDisplay in _speedDisplays)
      speedDisplay.OnUpdate();
  }
}

class SpeedDisplay {
  private const string SPEEDOMETER_TEXT_NAME = "SpeedrunTools_Speedometer_Text";

  private TMPro.TextMeshPro _tmp;
  private SpeedLogger _speedLogger;
  private Func<Vector3> _getPosition;

  public SpeedDisplay(
      float windowDuration, Vector3 localPosition,
      Func<Vector3, Vector3, float> calculateDistance,
      Func<Vector3> getPosition = null
  ) {
    _getPosition = getPosition ?? (() => Camera.main.transform.position);

    _speedLogger = new SpeedLogger() {
      WindowDuration = windowDuration,
      BufferSize = Mathf.CeilToInt(windowDuration * 240),
      CalculateDistance = calculateDistance,
    };

    var speedometerText = new GameObject(SPEEDOMETER_TEXT_NAME);
    _tmp = speedometerText.AddComponent<TMPro.TextMeshPro>();
    _tmp.alignment = TMPro.TextAlignmentOptions.BottomRight;
    _tmp.fontSize = 0.5f;
    _tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
    speedometerText.transform.SetParent(
        Mod.GameState.rigManager.ControllerRig.leftController.transform
    );
    _tmp.rectTransform.localPosition = localPosition;
    _tmp.rectTransform.localRotation = Quaternion.Euler(46f, 356f, 3f);
  }

  public void OnUpdate() {
    _speedLogger.OnFrame(Time.time, _getPosition());
    _tmp?.SetText($"{_speedLogger.GetSpeed():N2}m/s");
  }
}

class SpeedLogger {
  public int BufferSize = 1000;
  public float WindowDuration = 1;
  public Func<Vector3, Vector3, float> CalculateDistance;

  private (float, Vector3)[] _frames;
  private int _idxStart = 0;
  private int _idxEnd = 0;

  public SpeedLogger() { _frames = new(float, Vector3)[BufferSize]; }

  public void OnFrame(float time, Vector3 pos) {
    _frames[_idxEnd] = (time, pos);
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
    var duration = frameEnd.Item1 - frameStart.Item1;
    var distance = CalculateDistance(frameEnd.Item2, frameStart.Item2);
    return distance / duration;
  }
}
}
