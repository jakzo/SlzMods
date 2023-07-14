using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using SLZ.Marrow.Warehouse;

namespace Sst.SpeedrunTimer {
public class Splits {
  private static Regex _levelPrefixPattern = new Regex(@"^\s*\d+\s*-\s*");
  private static string LevelSplitName(LevelCrate level) {
    return _levelPrefixPattern.Replace(level.Title, "");
  }

  public List<Split> Items = new List<Split>();
  public DateTime? TimeStart;
  public DateTime? TimeEnd;
  public DateTime? TimeStartRelative;
  public DateTime? TimePause;
  public DateTime TimeLastSplitStartRelative = DateTime.Now;

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
    var now = DateTime.Now;
    TimeEnd = TimePause = null;
    TimeStart = TimeStartRelative = TimeLastSplitStartRelative = now;
    Items = new List<Split>() {
      new Split() {
        Level = firstLevel,
        Name = LevelSplitName(firstLevel),
        TimeStart = now,
      },
    };
  }

  public void Pause() { TimePause = DateTime.Now; }

  public void ResumeIfStarted() {
    if (TimePause == null)
      return;
    var delta = DateTime.Now - TimePause.Value;
    TimeStartRelative += delta;
    TimeLastSplitStartRelative += delta;
    TimePause = null;
  }

  public System.TimeSpan? GetTime() =>
      (TimeEnd ?? TimePause ?? DateTime.Now) - TimeStartRelative;

  public System.TimeSpan? GetCurrentSplitTime() =>
      (TimeEnd ?? TimePause ?? DateTime.Now) - TimeLastSplitStartRelative;

  public void Split(LevelCrate nextLevel) {
    var lastItem = Items[Items.Count - 1];
    var now = DateTime.Now;
    lastItem.TimeEnd = now;
    var splitTimeRelative = TimePause ?? now;
    lastItem.Duration = splitTimeRelative - TimeLastSplitStartRelative;
    TimeLastSplitStartRelative = splitTimeRelative;
    Items.Add(new Split() {
      Level = nextLevel,
      Name = LevelSplitName(nextLevel),
      TimeStart = now,
    });
  }
}

public class Split {
  public LevelCrate Level;
  public string Name;
  public DateTime? TimeStart;
  public DateTime? TimeEnd;
  public System.TimeSpan? Duration;
}
}
