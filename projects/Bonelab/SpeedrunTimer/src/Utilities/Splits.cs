using System.Collections.Generic;
using SLZ.Marrow.Warehouse;

namespace Sst.SpeedrunTimer {
public class Splits {
  public List<Split> Items = new List<Split>();
  public System.DateTime? TimeStart;
  public System.DateTime? TimeEnd;
  public System.DateTime? TimeStartRelative;
  public System.DateTime? TimePause;
  public System.DateTime TimeLastSplitStartRelative = System.DateTime.Now;

  public void Reset() {
    TimeStart = null;
    TimeEnd = null;
    TimeStartRelative = null;
    TimePause = null;
  }

  public void ResetAndPause(LevelCrate firstLevel) {
    ResetAndStart(firstLevel);
    TimePause = TimeStart;
  }

  public void ResetAndStart(LevelCrate firstLevel) {
    var now = System.DateTime.Now;
    TimeEnd = TimePause = null;
    TimeStart = TimeStartRelative = TimeLastSplitStartRelative = now;
    Items = new List<Split>() {
      new Split() {
        Level = firstLevel,
        Name = firstLevel.name,
        TimeStart = now,
      },
    };
  }

  public void Pause() { TimePause = System.DateTime.Now; }

  public void ResumeIfStarted() {
    if (TimePause == null)
      return;
    var delta = System.DateTime.Now - TimePause.Value;
    TimeStartRelative += delta;
    TimeLastSplitStartRelative += delta;
    TimePause = null;
  }

  public System.TimeSpan? GetTime() =>
      (TimeEnd ?? TimePause ?? System.DateTime.Now) - TimeStartRelative;

  public void Split(LevelCrate nextLevel) {
    var lastItem = Items[Items.Count - 1];
    var now = System.DateTime.Now;
    lastItem.TimeEnd = now;
    var splitTimeRelative = TimePause ?? now;
    lastItem.Duration = splitTimeRelative - TimeLastSplitStartRelative;
    TimeLastSplitStartRelative = splitTimeRelative;
    Items.Add(new Split() {
      Level = nextLevel,
      Name = nextLevel.name,
      TimeStart = now,
    });
  }
}

public class Split {
  public LevelCrate Level;
  public string Name;
  public System.DateTime? TimeStart;
  public System.DateTime? TimeEnd;
  public System.TimeSpan? Duration;
}
}
