#if DEBUG
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using MelonLoader;

namespace Sst.BoneworksPerformance;

internal struct MonoGcEventSample {
  public long Timestamp;
  public int Event;
  public uint Generation;
  public bool Serial;
  public int ThreadId;
  public PlayerLoopPhase Phase;
}

internal sealed class MonoGcProfileResult {
  public readonly MonoGcEventSample[] Events;
  public readonly int EventCount;
  public readonly bool CallbackInstalled;
  public readonly string Status;

  public MonoGcProfileResult(
      MonoGcEventSample[] events, int eventCount,
      bool callbackInstalled, string status
  ) {
    Events = events;
    EventCount = eventCount;
    CallbackInstalled = callbackInstalled;
    Status = status;
  }
}

internal static class MonoGcProfiler {
  private const string MonoLibrary = "mono-2.0-bdwgc";

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void GcEventCallback(
      IntPtr profiler, int gcEvent, uint generation, bool serial
  );

  private static readonly GcEventCallback Callback = OnGcEvent;
  private static IntPtr _handle;
  private static MonoGcEventSample[] _events = new MonoGcEventSample[0];
  private static int _eventCount;
  private static volatile bool _capturing;
  private static bool _callbackInstalled;
  private static string _status = "not started";

  public static void Start(float maximumSeconds) {
    _capturing = false;
    _eventCount = 0;
    var seconds = Math.Max(1.0, Math.Min(maximumSeconds, 1800.0));
    _events = new MonoGcEventSample[
        (int)Math.Min(65536.0, Math.Ceiling(seconds * 64.0))
    ];
    try {
      if (_handle == IntPtr.Zero)
        _handle = mono_profiler_create(IntPtr.Zero);
      if (_handle == IntPtr.Zero)
        throw new InvalidOperationException("mono_profiler_create returned null.");
      mono_profiler_set_gc_event_callback(_handle, Callback);
      _callbackInstalled = true;
      _status = "active";
      _capturing = true;
    } catch (Exception exception) {
      _callbackInstalled = false;
      _status = exception.GetType().Name + ": " + exception.Message;
      MelonLogger.Warning("Could not install Mono GC event callback: " + _status);
    }
  }

  public static MonoGcProfileResult Stop() {
    _capturing = false;
    return new MonoGcProfileResult(
        _events, Math.Min(Volatile.Read(ref _eventCount), _events.Length),
        _callbackInstalled, _status
    );
  }

  public static MonoGcProfileResult EmptyResult() =>
      new MonoGcProfileResult(
          new MonoGcEventSample[0], 0, false, "disabled"
      );

  public static long HeapSize {
    get {
      try {
        return unchecked((long)mono_gc_get_heap_size().ToUInt64());
      } catch {
        return 0;
      }
    }
  }

  public static long UsedSize {
    get {
      try {
        return unchecked((long)mono_gc_get_used_size().ToUInt64());
      } catch {
        return 0;
      }
    }
  }

  public static int CollectionCount(int generation) {
    try {
      return checked((int)mono_gc_collection_count(generation));
    } catch {
      return 0;
    }
  }

  public static string EventName(int gcEvent) {
    switch (gcEvent) {
      case 0: return "Start";
      case 1: return "MarkStart";
      case 2: return "MarkEnd";
      case 3: return "ReclaimStart";
      case 4: return "ReclaimEnd";
      case 5: return "End";
      case 6: return "PreStopWorld";
      case 7: return "PostStopWorld";
      case 8: return "PreStartWorld";
      case 9: return "PostStartWorld";
      case 10: return "PreStopWorldLocked";
      case 11: return "PostStartWorldUnlocked";
      default: return "Event" + gcEvent;
    }
  }

  private static void OnGcEvent(
      IntPtr profiler, int gcEvent, uint generation, bool serial
  ) {
    if (!_capturing)
      return;
    var index = Interlocked.Increment(ref _eventCount) - 1;
    if (index < 0 || index >= _events.Length)
      return;
    _events[index] = new MonoGcEventSample {
      Timestamp = Stopwatch.GetTimestamp(),
      Event = gcEvent,
      Generation = generation,
      Serial = serial,
      ThreadId = CpuSampler.GetCurrentNativeThreadId(),
      Phase = PerformanceCapture.CurrentPlayerLoopPhase,
    };
  }

  [DllImport(MonoLibrary, CallingConvention = CallingConvention.Cdecl)]
  private static extern IntPtr mono_profiler_create(IntPtr profiler);

  [DllImport(MonoLibrary, CallingConvention = CallingConvention.Cdecl)]
  private static extern void mono_profiler_set_gc_event_callback(
      IntPtr handle, GcEventCallback callback
  );

  [DllImport(MonoLibrary, CallingConvention = CallingConvention.Cdecl)]
  private static extern UIntPtr mono_gc_get_heap_size();

  [DllImport(MonoLibrary, CallingConvention = CallingConvention.Cdecl)]
  private static extern UIntPtr mono_gc_get_used_size();

  [DllImport(MonoLibrary, CallingConvention = CallingConvention.Cdecl)]
  private static extern uint mono_gc_collection_count(int generation);
}
#endif
