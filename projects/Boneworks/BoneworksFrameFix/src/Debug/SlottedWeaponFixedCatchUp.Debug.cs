using System.Diagnostics;
using MelonLoader;
using UnityEngine;
namespace Sst.BoneworksFrameFix;

internal sealed partial class SlottedWeaponFixedCatchUp
{
  private long _catchUpTicks;
  private long _slotMoves;
  private long _totalStopwatchTicks;
  private long _maximumStopwatchTicks;
  private float _nextDiagnosticAt;

  private static long DebugCatchUpStarted() => Stopwatch.GetTimestamp();
  private void DebugResetDiagnostics()
  {
    _catchUpTicks = 0;
    _slotMoves = 0;
    _totalStopwatchTicks = 0;
    _maximumStopwatchTicks = 0;
    _nextDiagnosticAt = Time.realtimeSinceStartup + 10f;

  }
  private void DebugRecordCatchUp(long started, int moved)
  {
    var elapsed = Stopwatch.GetTimestamp() - started;
    _catchUpTicks++;
    _slotMoves += moved;
    _totalStopwatchTicks += elapsed;
    if (elapsed > _maximumStopwatchTicks)
      _maximumStopwatchTicks = elapsed;
    LogDiagnosticsIfDue();

  }
  private void LogDiagnosticsIfDue()
  {
    var now = Time.realtimeSinceStartup;
    if (now < _nextDiagnosticAt || _catchUpTicks == 0)
      return;
    _nextDiagnosticAt = now + 10f;
    var microsecondsPerTick = _totalStopwatchTicks * 1000000.0 /
                              Stopwatch.Frequency / _catchUpTicks;
    var maximumMicroseconds = _maximumStopwatchTicks * 1000000.0 /
                              Stopwatch.Frequency;
    MelonLogger.Msg(
        "Fixed-tick weapon-slot catch-up: ticks=" + _catchUpTicks +
        ", slot moves=" + _slotMoves + ", mean=" +
        microsecondsPerTick.ToString("0.###") + " us, max=" +
        maximumMicroseconds.ToString("0.###") + " us."
    );
  }

}
