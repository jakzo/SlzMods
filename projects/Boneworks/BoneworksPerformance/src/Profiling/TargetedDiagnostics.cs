#if DEBUG
using System;
using System.Diagnostics;
using System.Threading;
using HarmonyLib;
using MelonLoader;
using StressLevelZero.VRMK;
using UnityEngine;

namespace Sst.BoneworksPerformance;

internal struct TargetedDiagnosticFrame {
  public int PhysBodyCalls;
  public int PhysBodyUnchangedCalls;
  public long PhysBodyTicks;
  public int Exceptions;
}

internal struct PhysBodyDiagnosticRow {
  public long InstanceAddress;
  public int Calls;
  public int UnchangedCalls;
  public long DurationTicks;
}

internal struct ExceptionDiagnosticRow {
  public ulong Identity;
  public int Count;
  public string Condition;
  public string StackTrace;
}

internal sealed class TargetedDiagnosticResult {
  public readonly PhysBodyDiagnosticRow[] PhysBodies;
  public readonly ExceptionDiagnosticRow[] Exceptions;
  public readonly int PhysBodyCalls;
  public readonly int PhysBodyUnchangedCalls;
  public readonly long PhysBodyTicks;
  public readonly int ExceptionCount;

  public TargetedDiagnosticResult(
      PhysBodyDiagnosticRow[] physBodies,
      ExceptionDiagnosticRow[] exceptions,
      int physBodyCalls,
      int physBodyUnchangedCalls,
      long physBodyTicks,
      int exceptionCount
  ) {
    PhysBodies = physBodies;
    Exceptions = exceptions;
    PhysBodyCalls = physBodyCalls;
    PhysBodyUnchangedCalls = physBodyUnchangedCalls;
    PhysBodyTicks = physBodyTicks;
    ExceptionCount = exceptionCount;
  }
}

internal static class TargetedDiagnostics {
  private const int MaximumPhysBodies = 16;
  private const int MaximumExceptionIdentities = 128;

  private struct PhysBodyState {
    public long Address;
    public ulong LastOutputHash;
    public bool HasOutputHash;
    public int Calls;
    public int UnchangedCalls;
    public long DurationTicks;
  }

  private static readonly object ExceptionLock = new object();
  private static readonly PhysBodyState[] PhysBodies =
      new PhysBodyState[MaximumPhysBodies];
  private static readonly ExceptionDiagnosticRow[] Exceptions =
      new ExceptionDiagnosticRow[MaximumExceptionIdentities];
  private static volatile bool _capturing;
  private static int _physBodyCount;
  private static int _exceptionIdentityCount;
  private static int _framePhysBodyCalls;
  private static int _framePhysBodyUnchangedCalls;
  private static long _framePhysBodyTicks;
  private static int _frameExceptions;
  private static int _totalPhysBodyCalls;
  private static int _totalPhysBodyUnchangedCalls;
  private static long _totalPhysBodyTicks;
  private static int _totalExceptions;

  public static void Start() {
    _capturing = false;
    Array.Clear(PhysBodies, 0, PhysBodies.Length);
    Array.Clear(Exceptions, 0, Exceptions.Length);
    _physBodyCount = 0;
    _exceptionIdentityCount = 0;
    _framePhysBodyCalls = 0;
    _framePhysBodyUnchangedCalls = 0;
    _framePhysBodyTicks = 0;
    _frameExceptions = 0;
    _totalPhysBodyCalls = 0;
    _totalPhysBodyUnchangedCalls = 0;
    _totalPhysBodyTicks = 0;
    _totalExceptions = 0;
    MelonLogger.ErrorCallbackHandler += OnMelonError;
    _capturing = true;
  }

  public static TargetedDiagnosticFrame ConsumeFrame() =>
      new TargetedDiagnosticFrame {
        PhysBodyCalls = Interlocked.Exchange(ref _framePhysBodyCalls, 0),
        PhysBodyUnchangedCalls =
            Interlocked.Exchange(ref _framePhysBodyUnchangedCalls, 0),
        PhysBodyTicks = Interlocked.Exchange(ref _framePhysBodyTicks, 0),
        Exceptions = Interlocked.Exchange(ref _frameExceptions, 0),
      };

  public static TargetedDiagnosticResult Stop() {
    _capturing = false;
    MelonLogger.ErrorCallbackHandler -= OnMelonError;
    var physBodyRows = new PhysBodyDiagnosticRow[_physBodyCount];
    for (var i = 0; i < physBodyRows.Length; i++) {
      var state = PhysBodies[i];
      physBodyRows[i] = new PhysBodyDiagnosticRow {
        InstanceAddress = state.Address,
        Calls = state.Calls,
        UnchangedCalls = state.UnchangedCalls,
        DurationTicks = state.DurationTicks,
      };
    }
    ExceptionDiagnosticRow[] exceptionRows;
    lock (ExceptionLock) {
      exceptionRows = new ExceptionDiagnosticRow[_exceptionIdentityCount];
      Array.Copy(Exceptions, exceptionRows, exceptionRows.Length);
    }
    return new TargetedDiagnosticResult(
        physBodyRows, exceptionRows, _totalPhysBodyCalls,
        _totalPhysBodyUnchangedCalls, _totalPhysBodyTicks, _totalExceptions
    );
  }

  public static TargetedDiagnosticResult EmptyResult() =>
      new TargetedDiagnosticResult(
          new PhysBodyDiagnosticRow[0], new ExceptionDiagnosticRow[0],
          0, 0, 0, 0
      );

  private static void RecordPhysBody(
      PhysBody instance, long startedAt
  ) {
    if (!_capturing || !instance)
      return;
    var elapsed = Stopwatch.GetTimestamp() - startedAt;
    var address = instance.Pointer.ToInt64();
    ulong outputHash;
    try {
      outputHash = HashColliderGeometry(instance);
    } catch {
      outputHash = 0;
    }
    var index = FindPhysBody(address);
    var unchanged = outputHash != 0 && PhysBodies[index].HasOutputHash &&
                    PhysBodies[index].LastOutputHash == outputHash;
    var state = PhysBodies[index];
    state.Calls++;
    state.DurationTicks += elapsed;
    if (unchanged)
      state.UnchangedCalls++;
    if (outputHash != 0) {
      state.LastOutputHash = outputHash;
      state.HasOutputHash = true;
    }
    PhysBodies[index] = state;
    _totalPhysBodyCalls++;
    _totalPhysBodyTicks += elapsed;
    Interlocked.Increment(ref _framePhysBodyCalls);
    Interlocked.Add(ref _framePhysBodyTicks, elapsed);
    if (unchanged) {
      _totalPhysBodyUnchangedCalls++;
      Interlocked.Increment(ref _framePhysBodyUnchangedCalls);
    }
  }

  private static int FindPhysBody(long address) {
    for (var i = 0; i < _physBodyCount; i++) {
      if (PhysBodies[i].Address == address)
        return i;
    }
    if (_physBodyCount < MaximumPhysBodies) {
      var index = _physBodyCount++;
      PhysBodies[index].Address = address;
      return index;
    }
    return MaximumPhysBodies - 1;
  }

  private static ulong HashColliderGeometry(PhysBody body) {
    var hash = 1469598103934665603UL;
    HashCapsule(ref hash, body.kneePelvisCol);
    HashCapsule(ref hash, body.chestCol);
    HashCapsule(ref hash, body.pelvisCol);
    HashBox(ref hash, body.lfFingersCol);
    HashBox(ref hash, body.rtFingersCol);
    return hash;
  }

  private static void HashCapsule(ref ulong hash, CapsuleCollider collider) {
    if (!collider) {
      Mix(ref hash, 0);
      return;
    }
    HashVector(ref hash, collider.center);
    Mix(ref hash, FloatBits(collider.radius));
    Mix(ref hash, FloatBits(collider.height));
    Mix(ref hash, collider.direction);
  }

  private static void HashBox(ref ulong hash, BoxCollider collider) {
    if (!collider) {
      Mix(ref hash, 0);
      return;
    }
    HashVector(ref hash, collider.center);
    HashVector(ref hash, collider.size);
  }

  private static void HashVector(ref ulong hash, Vector3 value) {
    Mix(ref hash, FloatBits(value.x));
    Mix(ref hash, FloatBits(value.y));
    Mix(ref hash, FloatBits(value.z));
  }

  private static unsafe int FloatBits(float value) => *(int*)&value;

  private static void Mix(ref ulong hash, int value) {
    hash ^= unchecked((uint)value);
    hash *= 1099511628211UL;
  }

  private static void RecordException(
      string condition, string stackTrace
  ) {
    if (!_capturing)
      return;
    var identity = StableHash(string.IsNullOrEmpty(stackTrace)
        ? condition
        : stackTrace);
    lock (ExceptionLock) {
      var count = _exceptionIdentityCount;
      for (var i = 0; i < count; i++) {
        if (Exceptions[i].Identity != identity)
          continue;
        var row = Exceptions[i];
        row.Count++;
        Exceptions[i] = row;
        goto CountEvent;
      }
      if (count < Exceptions.Length) {
        Exceptions[count] = new ExceptionDiagnosticRow {
          Identity = identity,
          Count = 1,
          Condition = condition ?? "",
          StackTrace = stackTrace ?? "",
        };
        _exceptionIdentityCount = count + 1;
      }
    }
  CountEvent:
    Interlocked.Increment(ref _totalExceptions);
    Interlocked.Increment(ref _frameExceptions);
  }

  private static void OnMelonError(string melonName, string message) {
    RecordException(
        "MelonLoader:" + (melonName ?? "unknown") + ": " + message,
        message
    );
  }

  private static ulong StableHash(string value) {
    var hash = 1469598103934665603UL;
    if (value == null)
      return hash;
    for (var i = 0; i < value.Length; i++) {
      hash ^= value[i];
      hash *= 1099511628211UL;
    }
    return hash;
  }

  [HarmonyPatch(typeof(PhysBody), nameof(PhysBody.UpdateColliders))]
  private static class PhysBody_UpdateColliders_Patch {
    [HarmonyPrefix]
    private static void Prefix(ref long __state) {
      __state = _capturing ? Stopwatch.GetTimestamp() : 0;
    }

    [HarmonyPostfix]
    private static void Postfix(PhysBody __instance, long __state) {
      if (__state != 0)
        RecordPhysBody(__instance, __state);
    }
  }

  [HarmonyPatch(
      typeof(UnityEngine.Debug), nameof(UnityEngine.Debug.LogException),
      new[] { typeof(Il2CppSystem.Exception) }
  )]
  private static class Debug_LogException_Patch {
    [HarmonyPrefix]
    private static void Prefix(Il2CppSystem.Exception exception) {
      if (!_capturing || exception == null)
        return;
      RecordException(exception.Message, exception.StackTrace);
    }
  }

  [HarmonyPatch(
      typeof(UnityEngine.Debug), nameof(UnityEngine.Debug.LogException),
      new[] { typeof(Il2CppSystem.Exception), typeof(UnityEngine.Object) }
  )]
  private static class Debug_LogExceptionWithContext_Patch {
    [HarmonyPrefix]
    private static void Prefix(Il2CppSystem.Exception exception) {
      if (!_capturing || exception == null)
        return;
      RecordException(exception.Message, exception.StackTrace);
    }
  }
}
#endif
