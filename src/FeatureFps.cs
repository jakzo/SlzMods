using MelonLoader;
using UnityEngine;

namespace SpeedrunTools {
class FeatureFps : Feature {
  public float UpdateFrequency = 1;

  private FpsTimer _fixedUpdateTimer;
  private FpsTimer _updateTimer;
  private float _lastUpdate = 0;

  public override void OnFixedUpdate() { _fixedUpdateTimer.OnFrame(Time.time); }

  public override void OnUpdate() {
    _updateTimer.OnFrame(Time.time);

    if (Time.time - _lastUpdate >= UpdateFrequency) {
      _lastUpdate = Time.time;

      var updateFps = _updateTimer.GetFps(Time.time).ToString("n1");
      var fixedUpdateFps = _updateTimer.GetFps(Time.time).ToString("n1");
      MelonLogger.Msg(
          $"FPS Update: {updateFps}, FixedUpdate: {fixedUpdateFps}");
    }
  }
}

class FpsTimer {
  public int BufferSize = 1000;
  public float WindowDuration = 1;

  private float[] _times;
  private int _idxStart = 0;
  private int _idxEnd = 0;

  public FpsTimer() { _times = new float[BufferSize]; }

  public void OnFrame(float time) {
    _times[_idxEnd] = time;
    _idxEnd++;
    if (_idxEnd >= _times.Length)
      _idxEnd = 0;
    if (_idxEnd == _idxStart) {
      _idxStart++;
      if (_idxStart >= _times.Length)
        _idxStart = 0;
    }
  }

  public float GetFps(float time) {
    var windowStart = time - WindowDuration;
    while (_idxStart != _idxEnd && _times[_idxStart] < windowStart) {
      _idxStart++;
      if (_idxStart >= _times.Length)
        _idxStart = 0;
    }
    var numFramesInWindow = _idxEnd >= _idxStart
                                ? _idxEnd - _idxStart
                                : _times.Length - _idxStart + _idxEnd;
    return numFramesInWindow / WindowDuration;
  }
}
}
