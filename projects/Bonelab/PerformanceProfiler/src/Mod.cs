using System;
using System.Diagnostics;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Sst.PerformanceProfiler;

public class Mod : MelonMod {
  private const int MB = 1024 * 1024;

  public static Mod Instance;

  private long _lastMemoryUsage = 0;
  private Stopwatch _gcStopwatch = new Stopwatch();

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);
    Instance = this;

    _lastMemoryUsage = Il2CppSystem.GC.GetTotalMemory(false);
    _gcStopwatch.Start();
  }

  public override void OnUpdate() {
    var currentMemoryUsage = Il2CppSystem.GC.GetTotalMemory(false);
    var memoryDifference = currentMemoryUsage - _lastMemoryUsage;

    if (Math.Abs(memoryDifference) > MB) {
      MelonLogger.Msg($"Memory Usage Change: {memoryDifference / 1024} KB");
      _lastMemoryUsage = currentMemoryUsage;
    }

    if (Il2CppSystem.GC.CollectionCount(0) > 0 ||
        Il2CppSystem.GC.CollectionCount(1) > 0 ||
        Il2CppSystem.GC.CollectionCount(2) > 0) {
      // Happens every frame
      MelonLogger.Msg(
          $"GC Collection occurred at {_gcStopwatch.ElapsedMilliseconds}ms");
      _gcStopwatch.Restart();
    }
  }
}
