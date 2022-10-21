using System;

namespace Sst.Speedruns {
class RunTimer {
  private DateTime? _relativeStartTime;
  private DateTime? _pauseTime;

  public bool IsActive { get => _relativeStartTime.HasValue; }
  public bool IsPaused { get => _pauseTime.HasValue; }

  public void Stop() {
    _relativeStartTime = null;
    _pauseTime = null;
  }

  public void Reset(bool pauseAfterReset = false) {
    _relativeStartTime = DateTime.Now;
    _pauseTime = pauseAfterReset ? _relativeStartTime : null;
  }

  public void Pause() {
    if (_pauseTime.HasValue)
      return;
    _pauseTime = DateTime.Now;
  }

  public void Unpause() {
    if (!_pauseTime.HasValue)
      return;
    _relativeStartTime += DateTime.Now - _pauseTime;
    _pauseTime = null;
  }

  public TimeSpan? CalculateDuration() {
    return (_pauseTime ?? DateTime.Now) - _relativeStartTime;
  }
}
}
