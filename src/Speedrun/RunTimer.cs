using System;

namespace SpeedrunTools.Speedrun {
class RunTimer {
  public TimeSpan? Duration;

  private DateTime? _levelStart;

  public void Stop() {
    Duration = null;
    _levelStart = null;
  }

  public void Reset() {
    Duration = new System.TimeSpan();
    _levelStart = null;
  }

  public void OnLevelStart() { _levelStart = System.DateTime.Now; }

  public void OnLevelEnd() {
    if (_levelStart.HasValue) {
      if (Duration.HasValue)
        Duration += System.DateTime.Now - _levelStart;
      _levelStart = null;
    }
  }
}
}