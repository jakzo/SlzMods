using MelonLoader;
using UnityEngine;

namespace Sst.Features {
class Fps : Feature {
  public float UpdateFrequency = 1;

  private FpsTimer _fixedUpdateTimer = new FpsTimer();
  private FpsTimer _updateTimer = new FpsTimer();
  private float _lastUpdate = 0;

  public Fps() { IsDev = true; }

  public override void OnFixedUpdate() { _fixedUpdateTimer.OnFrame(Time.time); }

  public override void OnUpdate() {
    _updateTimer.OnFrame(Time.time);

    if (Time.time - _lastUpdate >= UpdateFrequency) {
      _lastUpdate = Time.time;

      var timing = new Valve.VR.Compositor_FrameTiming();
      timing.m_nSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(
          typeof(Valve.VR.Compositor_FrameTiming)
      );
      Valve.VR.OpenVR.Compositor.GetFrameTiming(ref timing, 0);

      var updateFps = _updateTimer.GetFps(Time.time).ToString("N1");
      var fixedUpdateFps = _fixedUpdateTimer.GetFps(Time.time).ToString("N1");
      var refreshRate =
          (1f / (timing.m_flClientFrameIntervalMs / 1000f)).ToString("N1");
      MelonLogger.Msg(
          $"FPS Refresh: {refreshRate}, Update: {updateFps}, FixedUpdate: {fixedUpdateFps}"
      );
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
    return (float)numFramesInWindow / WindowDuration;
  }
}
}
