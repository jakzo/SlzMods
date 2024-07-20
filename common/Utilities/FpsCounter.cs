using System;
using System.Collections.Generic;

namespace Sst.Utilities;

public class FpsCounter {
  public TimeSpan WindowSize;

  private Queue<DateTime> _times = new();

  public FpsCounter(TimeSpan? windowSize = null) {
    WindowSize = windowSize ?? TimeSpan.FromSeconds(0.2f);
  }

  public void OnFrame() {
    RemoveOutOfWindowTimes();
    _times.Enqueue(DateTime.Now);
  }

  public double Read() {
    RemoveOutOfWindowTimes();
    return (double)_times.Count / WindowSize.TotalSeconds;
  }

  private void RemoveOutOfWindowTimes() {
    var windowStart = DateTime.Now - WindowSize;
    while (_times.Count > 0 && _times.Peek() < windowStart) {
      _times.Dequeue();
    }
  }
}
