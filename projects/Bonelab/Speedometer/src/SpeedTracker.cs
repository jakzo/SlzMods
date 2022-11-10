using UnityEngine;

namespace Sst.Speedometer {
class SpeedTracker {
  public int BufferSize = 1000;
  public float WindowDuration = 1;

  private (float, float, float)[] _frames;
  private int _idxStart = 0;
  private int _idxEnd = 0;

  public SpeedTracker() { _frames = new(float, float, float)[BufferSize]; }

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
