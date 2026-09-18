#if DEBUG
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using MelonLoader;
using MonoMod.RuntimeDetour;
using UnityEngine;

namespace Sst.BoneworksPerformance;

internal struct CompletionWaitSample {
  public int Id;
  public long Started;
  public long DurationTicks;
  public long StateAddress;
  public long EventHandle;
  public int TargetCount;
  public int InitialCount;
  public int FinalCount;
  public PlayerLoopPhase Phase;
}

internal sealed class CompletionWaitResult {
  public readonly CompletionWaitSample[] Waits;
  public readonly int WaitCount;
  public readonly bool HookInstalled;

  public CompletionWaitResult(
      CompletionWaitSample[] waits, int waitCount, bool hookInstalled
  ) {
    Waits = waits;
    WaitCount = waitCount;
    HookInstalled = hookInstalled;
  }
}

internal static class CompletionWaitProfiler {
  private const int CompletionEventOffset = 0x38;
  private const int CompletedCountOffset = 0x1E0;
  private const long WaitFunctionOffset = 0x5BB8D0;
  private static readonly byte[] ExpectedPrologue = {
    0x48, 0x89, 0x5C, 0x24, 0x08, 0x57, 0x48, 0x83,
    0xEC, 0x20, 0x8B, 0x81, 0xE0, 0x01, 0x00, 0x00,
  };

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void CompletionWaitDelegate(
      IntPtr completionState, int targetCount
  );

  private static CompletionWaitDelegate _hookDelegate;
  private static CompletionWaitDelegate _original;
  private static NativeDetour _detour;
  private static CompletionWaitSample[] _waits =
      new CompletionWaitSample[0];
  private static int _mainThreadId;
  private static int _waitCount;
  private static volatile int _currentWaitId;
  private static volatile bool _capturing;
  private static bool _hookInstalled;

  public static int CurrentWaitId => _currentWaitId;
  public static int RecordedWaitCount => Volatile.Read(ref _waitCount);

  public static void Start(int mainThreadId, float maximumSeconds) {
    StopDetour();
    _mainThreadId = mainThreadId;
    _waitCount = 0;
    _currentWaitId = 0;
    _hookInstalled = false;
    var seconds = Math.Max(1.0, Math.Min(maximumSeconds, 1800.0));
    var capacity = (int)Math.Min(
        262144.0, Math.Ceiling(seconds * 240.0)
    );
    _waits = new CompletionWaitSample[capacity];
    try {
      if (!string.Equals(
              Application.unityVersion, "2018.4.10f1",
              StringComparison.OrdinalIgnoreCase
          ))
        throw new NotSupportedException(
            "The completion-wait offset is verified only for Unity 2018.4.10f1."
        );
      var moduleBase = FindModuleBase("UnityPlayer.dll");
      if (moduleBase == IntPtr.Zero)
        throw new InvalidOperationException("UnityPlayer.dll is not loaded.");
      var target = new IntPtr(moduleBase.ToInt64() + WaitFunctionOffset);
      VerifyPrologue(target);

      _hookDelegate = HookCompletionWait;
      var config = new NativeDetourConfig {ManualApply = true};
      _detour = new NativeDetour(target, _hookDelegate, config);
      _original = _detour.GenerateTrampoline<CompletionWaitDelegate>();
      _detour.Apply();
      _hookInstalled = true;
      _capturing = true;
      MelonLogger.Msg(
          "Detailed completion-wait instrumentation enabled at " +
          "UnityPlayer.dll+0x" + WaitFunctionOffset.ToString("X") + "."
      );
    } catch (Exception exception) {
      _capturing = false;
      _hookInstalled = false;
      StopDetour();
      MelonLogger.Warning(
          "Could not instrument Unity's completion wait: " + exception
      );
    }
  }

  public static CompletionWaitResult Stop() {
    _capturing = false;
    _currentWaitId = 0;
    StopDetour();
    return new CompletionWaitResult(
        _waits, Math.Min(_waitCount, _waits.Length), _hookInstalled
    );
  }

  private static void HookCompletionWait(
      IntPtr completionState, int targetCount
  ) {
    if (!_capturing ||
        CpuSampler.GetCurrentNativeThreadId() != _mainThreadId) {
      _original(completionState, targetCount);
      return;
    }

    var index = Interlocked.Increment(ref _waitCount) - 1;
    if (index < 0 || index >= _waits.Length) {
      _original(completionState, targetCount);
      return;
    }

    var started = Stopwatch.GetTimestamp();
    var sample = new CompletionWaitSample {
      Id = index + 1,
      Started = started,
      StateAddress = completionState.ToInt64(),
      EventHandle = Marshal.ReadIntPtr(
          completionState, CompletionEventOffset
      ).ToInt64(),
      TargetCount = targetCount,
      InitialCount = Marshal.ReadInt32(
          completionState, CompletedCountOffset
      ),
      Phase = PerformanceCapture.CurrentPlayerLoopPhase,
    };
    _waits[index] = sample;
    _currentWaitId = sample.Id;
    try {
      _original(completionState, targetCount);
    } finally {
      sample.FinalCount = Marshal.ReadInt32(
          completionState, CompletedCountOffset
      );
      sample.DurationTicks = Stopwatch.GetTimestamp() - started;
      _waits[index] = sample;
      _currentWaitId = 0;
    }
  }

  private static IntPtr FindModuleBase(string name) {
    using (var process = Process.GetCurrentProcess()) {
      foreach (ProcessModule module in process.Modules) {
        try {
          if (string.Equals(
                  module.ModuleName, name,
                  StringComparison.OrdinalIgnoreCase
              ))
            return module.BaseAddress;
        } finally {
          module.Dispose();
        }
      }
    }
    return IntPtr.Zero;
  }

  private static void VerifyPrologue(IntPtr target) {
    for (var i = 0; i < ExpectedPrologue.Length; i++) {
      if (Marshal.ReadByte(target, i) != ExpectedPrologue[i])
        throw new InvalidOperationException(
            "Unity completion-wait machine code does not match the " +
            "verified BONEWORKS build."
        );
    }
  }

  private static void StopDetour() {
    if (_detour != null) {
      try {
        _detour.Dispose();
      } catch (Exception exception) {
        MelonLogger.Warning(
            "Could not remove completion-wait detour cleanly: " +
            exception.Message
        );
      }
      _detour = null;
    }
    _original = null;
    _hookDelegate = null;
  }

}
#endif
