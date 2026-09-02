#if DEBUG
using System.Diagnostics;
using UnityEngine;

namespace Sst.BoneworksPerformance;

internal struct ProfilerSelfSample {
  public long UpdateTicks;
  public long FixedUpdateTicks;
  public long GuiTicks;
  public long GuiManagedHeapDelta;
  public int GuiCalls;
  public int GuiLayoutCalls;
  public int GuiRepaintCalls;
  public int GuiOtherCalls;
}

internal static class ProfilerSelfMetrics {
  private static long _updateTicks;
  private static long _fixedUpdateTicks;
  private static long _guiTicks;
  private static long _guiManagedHeapDelta;
  private static int _guiCalls;
  private static int _guiLayoutCalls;
  private static int _guiRepaintCalls;
  private static int _guiOtherCalls;

  public static void AddUpdate(long started) {
    _updateTicks += Stopwatch.GetTimestamp() - started;
  }

  public static void AddFixedUpdate(long started) {
    _fixedUpdateTicks += Stopwatch.GetTimestamp() - started;
  }

  public static void AddGui(
      long started, long heapBefore, long heapAfter, EventType eventType
  ) {
    _guiTicks += Stopwatch.GetTimestamp() - started;
    _guiManagedHeapDelta += heapAfter - heapBefore;
    _guiCalls++;
    if (eventType == EventType.Layout)
      _guiLayoutCalls++;
    else if (eventType == EventType.Repaint)
      _guiRepaintCalls++;
    else
      _guiOtherCalls++;
  }

  public static ProfilerSelfSample Consume() {
    var result = new ProfilerSelfSample {
      UpdateTicks = _updateTicks,
      FixedUpdateTicks = _fixedUpdateTicks,
      GuiTicks = _guiTicks,
      GuiManagedHeapDelta = _guiManagedHeapDelta,
      GuiCalls = _guiCalls,
      GuiLayoutCalls = _guiLayoutCalls,
      GuiRepaintCalls = _guiRepaintCalls,
      GuiOtherCalls = _guiOtherCalls,
    };
    _updateTicks = 0;
    _fixedUpdateTicks = 0;
    _guiTicks = 0;
    _guiManagedHeapDelta = 0;
    _guiCalls = 0;
    _guiLayoutCalls = 0;
    _guiRepaintCalls = 0;
    _guiOtherCalls = 0;
    return result;
  }

  public static double Milliseconds(long ticks) =>
      ticks * 1000.0 / Stopwatch.Frequency;
}
#endif
