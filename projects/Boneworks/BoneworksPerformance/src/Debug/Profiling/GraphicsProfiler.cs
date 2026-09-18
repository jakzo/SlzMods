#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using MelonLoader;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Valve.VR;

namespace Sst.BoneworksPerformance;

internal struct GraphicsPendingWorkSample {
  public int Id;
  public long Started;
  public long DurationTicks;
  public int ThreadId;
  public int CompletionTargetSerial;
  public int FirstCompletionWaitId;
  public int LastCompletionWaitId;
  public PlayerLoopPhase Phase;
}

internal struct GpuEngineSample {
  public long Timestamp;
  public int CounterIndex;
  public float UtilizationPercent;
}

internal sealed class GraphicsAdapterInfo {
  public int Index;
  public string Name;
  public uint VendorId;
  public uint DeviceId;
  public ulong DedicatedVideoMemory;
  public long Luid;
}

internal sealed class GpuEngineCounterInfo {
  public string InstanceName;
  public string EngineType;
  public string LuidText;
  public long Luid;
}

internal sealed class GraphicsProfileResult {
  public readonly GraphicsPendingWorkSample[] PendingWork;
  public readonly int PendingWorkCount;
  public readonly bool PendingWorkHookInstalled;
  public readonly long GraphicsDeviceAddress;
  public readonly long PendingWorkTargetAddress;
  public readonly long UnityPlayerBaseAddress;
  public readonly GraphicsAdapterInfo[] Adapters;
  public readonly GpuEngineCounterInfo[] EngineCounters;
  public readonly GpuEngineSample[] EngineSamples;
  public readonly int EngineSampleCount;
  public readonly int EngineSampleTicks;
  public readonly string EngineSamplerStatus;
  public readonly long SteamVrAdapterLuid;
  public readonly string UnityDeviceName;
  public readonly string UnityDeviceVendor;
  public readonly string UnityDeviceVersion;
  public readonly int UnityDeviceVendorId;
  public readonly int UnityDeviceId;
  public readonly int UnityGraphicsMemoryMb;
  public readonly bool UnityGraphicsMultiThreaded;

  public GraphicsProfileResult(
      GraphicsPendingWorkSample[] pendingWork, int pendingWorkCount,
      bool pendingWorkHookInstalled, long graphicsDeviceAddress,
      long pendingWorkTargetAddress, long unityPlayerBaseAddress,
      GraphicsAdapterInfo[] adapters,
      GpuEngineCounterInfo[] engineCounters,
      GpuEngineSample[] engineSamples, int engineSampleCount,
      int engineSampleTicks, string engineSamplerStatus,
      long steamVrAdapterLuid, string unityDeviceName,
      string unityDeviceVendor, string unityDeviceVersion,
      int unityDeviceVendorId, int unityDeviceId,
      int unityGraphicsMemoryMb, bool unityGraphicsMultiThreaded
  ) {
    PendingWork = pendingWork;
    PendingWorkCount = pendingWorkCount;
    PendingWorkHookInstalled = pendingWorkHookInstalled;
    GraphicsDeviceAddress = graphicsDeviceAddress;
    PendingWorkTargetAddress = pendingWorkTargetAddress;
    UnityPlayerBaseAddress = unityPlayerBaseAddress;
    Adapters = adapters;
    EngineCounters = engineCounters;
    EngineSamples = engineSamples;
    EngineSampleCount = engineSampleCount;
    EngineSampleTicks = engineSampleTicks;
    EngineSamplerStatus = engineSamplerStatus;
    SteamVrAdapterLuid = steamVrAdapterLuid;
    UnityDeviceName = unityDeviceName;
    UnityDeviceVendor = unityDeviceVendor;
    UnityDeviceVersion = unityDeviceVersion;
    UnityDeviceVendorId = unityDeviceVendorId;
    UnityDeviceId = unityDeviceId;
    UnityGraphicsMemoryMb = unityGraphicsMemoryMb;
    UnityGraphicsMultiThreaded = unityGraphicsMultiThreaded;
  }
}

internal static class GraphicsProfiler {
  private const long GetGraphicsDeviceOffset = 0x8D6E80;
  private const int PendingWorkVirtualSlotOffset = 0x560;
  private const int GpuSamplesPerSecond = 10;
  private const int MaximumGpuCounters = 64;
  private static readonly byte[] ExpectedGetterPrologue = {
    0x8B, 0x0D, 0x5A, 0xD1, 0xC8, 0x00, 0x48,
    0xFF, 0x25, 0x03, 0x99, 0x8D, 0x00,
  };

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate IntPtr GetGraphicsDeviceDelegate();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void PendingWorkDelegate(
      IntPtr device, int completionTargetSerial
  );

  private sealed class LiveGpuCounter {
    public readonly IntPtr Handle;
    public readonly GpuEngineCounterInfo Info;

    public LiveGpuCounter(
        IntPtr handle, GpuEngineCounterInfo info
    ) {
      Handle = handle;
      Info = info;
    }
  }

  private static GraphicsAdapterInfo[] _adapters =
      new GraphicsAdapterInfo[0];
  private static PendingWorkDelegate _hookDelegate;
  private static PendingWorkDelegate _original;
  private static NativeDetour _detour;
  private static GraphicsPendingWorkSample[] _pendingWork =
      new GraphicsPendingWorkSample[0];
  private static GpuEngineSample[] _gpuSamples = new GpuEngineSample[0];
  private static LiveGpuCounter[] _gpuCounters = new LiveGpuCounter[0];
  private static IntPtr _gpuQuery;
  private static int _pendingWorkCount;
  private static int _gpuSampleCount;
  private static int _gpuSampleTicks;
  private static int _mainThreadId;
  private static int _gpuSamplerThreadId;
  private static long _unityPlayerBase;
  private static long _graphicsDeviceAddress;
  private static long _pendingWorkTargetAddress;
  private static long _steamVrAdapterLuid;
  private static string _unityDeviceName = "";
  private static string _unityDeviceVendor = "";
  private static string _unityDeviceVersion = "";
  private static int _unityDeviceVendorId;
  private static int _unityDeviceId;
  private static int _unityGraphicsMemoryMb;
  private static bool _unityGraphicsMultiThreaded;
  private static string _gpuSamplerStatus = "not started";
  private static Thread _gpuSamplerThread;
  private static volatile bool _capturing;
  private static volatile bool _gpuSamplerRunning;
  private static bool _hookInstalled;

  public static void Initialize() {
    try {
      _adapters = EnumerateAdapters();
    } catch (Exception exception) {
      _adapters = new GraphicsAdapterInfo[0];
      MelonLogger.Warning(
          "Could not enumerate DXGI adapters: " + exception.Message
      );
    }
  }

  public static bool IsInternalThread(int threadId) =>
      threadId != 0 && threadId == _gpuSamplerThreadId;

  public static void Start(int mainThreadId, float maximumSeconds) {
    StopInternal(false);
    _mainThreadId = mainThreadId;
    _pendingWorkCount = 0;
    _gpuSampleCount = 0;
    _gpuSampleTicks = 0;
    _gpuSamplerStatus = "starting";
    _graphicsDeviceAddress = 0;
    _pendingWorkTargetAddress = 0;
    _unityPlayerBase = 0;
    _hookInstalled = false;
    CaptureDeviceIdentity();
    _steamVrAdapterLuid = ReadSteamVrAdapterLuid();

    var seconds = Math.Max(1.0, Math.Min(maximumSeconds, 1800.0));
    _pendingWork = new GraphicsPendingWorkSample[
        (int)Math.Min(262144.0, Math.Ceiling(seconds * 180.0))
    ];
    _gpuSamples = new GpuEngineSample[
        (int)Math.Min(
            262144.0,
            Math.Ceiling(seconds * GpuSamplesPerSecond * 8.0)
        )
    ];

    InstallPendingWorkHook();
    _capturing = true;
    StartGpuSampler();
  }

  public static GraphicsProfileResult Stop() => StopInternal(true);

  public static GraphicsProfileResult EmptyResult() =>
      new GraphicsProfileResult(
          new GraphicsPendingWorkSample[0], 0, false, 0, 0, 0,
          new GraphicsAdapterInfo[0], new GpuEngineCounterInfo[0],
          new GpuEngineSample[0], 0, 0, "disabled", 0,
          "", "", "", 0, 0, 0, false
      );

  public static void Shutdown() { StopInternal(false); }

  private static GraphicsProfileResult StopInternal(bool keepResult) {
    _capturing = false;
    StopGpuSampler();
    StopDetour();

    var counterInfo = new GpuEngineCounterInfo[_gpuCounters.Length];
    for (var i = 0; i < _gpuCounters.Length; i++)
      counterInfo[i] = _gpuCounters[i].Info;
    DisposeGpuCounters();

    return new GraphicsProfileResult(
        keepResult ? _pendingWork : new GraphicsPendingWorkSample[0],
        keepResult ? Math.Min(_pendingWorkCount, _pendingWork.Length) : 0,
        keepResult && _hookInstalled,
        keepResult ? _graphicsDeviceAddress : 0,
        keepResult ? _pendingWorkTargetAddress : 0,
        keepResult ? _unityPlayerBase : 0,
        keepResult ? _adapters : new GraphicsAdapterInfo[0],
        keepResult ? counterInfo : new GpuEngineCounterInfo[0],
        keepResult ? _gpuSamples : new GpuEngineSample[0],
        keepResult ? Math.Min(_gpuSampleCount, _gpuSamples.Length) : 0,
        keepResult ? _gpuSampleTicks : 0,
        keepResult ? _gpuSamplerStatus : "disabled",
        keepResult ? _steamVrAdapterLuid : 0,
        keepResult ? _unityDeviceName : "",
        keepResult ? _unityDeviceVendor : "",
        keepResult ? _unityDeviceVersion : "",
        keepResult ? _unityDeviceVendorId : 0,
        keepResult ? _unityDeviceId : 0,
        keepResult ? _unityGraphicsMemoryMb : 0,
        keepResult && _unityGraphicsMultiThreaded
    );
  }

  private static void InstallPendingWorkHook() {
    try {
      if (!string.Equals(
              Application.unityVersion, "2018.4.10f1",
              StringComparison.OrdinalIgnoreCase
          ))
        throw new NotSupportedException(
            "The graphics-device offsets are verified only for Unity 2018.4.10f1."
        );

      using (var process = Process.GetCurrentProcess()) {
        ProcessModule unityPlayer = null;
        foreach (ProcessModule module in process.Modules) {
          if (string.Equals(
                  module.ModuleName, "UnityPlayer.dll",
                  StringComparison.OrdinalIgnoreCase
              )) {
            unityPlayer = module;
            break;
          }
          module.Dispose();
        }
        if (unityPlayer == null)
          throw new InvalidOperationException("UnityPlayer.dll is not loaded.");
        using (unityPlayer) {
          _unityPlayerBase = unityPlayer.BaseAddress.ToInt64();
          var getterAddress = new IntPtr(
              _unityPlayerBase + GetGraphicsDeviceOffset
          );
          VerifyBytes(getterAddress, ExpectedGetterPrologue);
          var getter = Marshal.GetDelegateForFunctionPointer<
              GetGraphicsDeviceDelegate>(getterAddress);
          var device = getter();
          if (device == IntPtr.Zero)
            throw new InvalidOperationException(
                "Unity returned a null graphics-device pointer."
            );
          var vtable = Marshal.ReadIntPtr(device);
          if (vtable == IntPtr.Zero)
            throw new InvalidOperationException(
                "Unity's graphics-device vtable is null."
            );
          var target = Marshal.ReadIntPtr(
              vtable, PendingWorkVirtualSlotOffset
          );
          var targetAddress = target.ToInt64();
          var moduleEnd = _unityPlayerBase + unityPlayer.ModuleMemorySize;
          if (targetAddress < _unityPlayerBase || targetAddress >= moduleEnd)
            throw new InvalidOperationException(
                "The pending-work method is outside UnityPlayer.dll."
            );

          _graphicsDeviceAddress = device.ToInt64();
          _pendingWorkTargetAddress = targetAddress;
          _hookDelegate = HookPendingWork;
          var config = new NativeDetourConfig {ManualApply = true};
          _detour = new NativeDetour(target, _hookDelegate, config);
          _original = _detour.GenerateTrampoline<PendingWorkDelegate>();
          _detour.Apply();
          _hookInstalled = true;
        }
      }
      MelonLogger.Msg(
          "Detailed graphics pending-work instrumentation enabled at " +
          "UnityPlayer.dll+0x" +
          (_pendingWorkTargetAddress - _unityPlayerBase).ToString("X") + "."
      );
    } catch (Exception exception) {
      _hookInstalled = false;
      StopDetour();
      MelonLogger.Warning(
          "Could not instrument Unity's graphics pending-work call: " +
          exception
      );
    }
  }

  private static void HookPendingWork(IntPtr device, int completionTargetSerial) {
    if (!_capturing ||
        CpuSampler.GetCurrentNativeThreadId() != _mainThreadId) {
      _original(device, completionTargetSerial);
      return;
    }

    var index = Interlocked.Increment(ref _pendingWorkCount) - 1;
    if (index < 0 || index >= _pendingWork.Length) {
      _original(device, completionTargetSerial);
      return;
    }

    var started = Stopwatch.GetTimestamp();
    var beforeWait = CompletionWaitProfiler.RecordedWaitCount;
    var sample = new GraphicsPendingWorkSample {
      Id = index + 1,
      Started = started,
      ThreadId = CpuSampler.GetCurrentNativeThreadId(),
      CompletionTargetSerial = completionTargetSerial,
      Phase = PerformanceCapture.CurrentPlayerLoopPhase,
    };
    try {
      _original(device, completionTargetSerial);
    } finally {
      var afterWait = CompletionWaitProfiler.RecordedWaitCount;
      if (afterWait > beforeWait) {
        sample.FirstCompletionWaitId = beforeWait + 1;
        sample.LastCompletionWaitId = afterWait;
      }
      sample.DurationTicks = Stopwatch.GetTimestamp() - started;
      _pendingWork[index] = sample;
    }
  }

  private static void CaptureDeviceIdentity() {
    try {
      _unityDeviceName = SystemInfo.graphicsDeviceName ?? "";
      _unityDeviceVendor = SystemInfo.graphicsDeviceVendor ?? "";
      _unityDeviceVersion = SystemInfo.graphicsDeviceVersion ?? "";
      _unityDeviceVendorId = SystemInfo.graphicsDeviceVendorID;
      _unityDeviceId = SystemInfo.graphicsDeviceID;
      _unityGraphicsMemoryMb = SystemInfo.graphicsMemorySize;
      _unityGraphicsMultiThreaded = SystemInfo.graphicsMultiThreaded;
    } catch (Exception exception) {
      MelonLogger.Warning(
          "Could not read Unity graphics-device identity: " +
          exception.Message
      );
    }
  }

  private static long ReadSteamVrAdapterLuid() {
    try {
      var system = OpenVR.System;
      if (system == null)
        return 0;
      var error = ETrackedPropertyError.TrackedProp_Success;
      return unchecked((long)system.GetUint64TrackedDeviceProperty(
          OpenVR.k_unTrackedDeviceIndex_Hmd,
          ETrackedDeviceProperty.Prop_GraphicsAdapterLuid_Uint64,
          ref error
      ));
    } catch {
      return 0;
    }
  }

  private static void StartGpuSampler() {
    _gpuSamplerRunning = true;
    _gpuSamplerThread = new Thread(GpuSamplerLoop) {
      IsBackground = true,
      Name = BuildInfo.NAME + " GPU sampler",
      Priority = System.Threading.ThreadPriority.BelowNormal,
    };
    _gpuSamplerThread.Start();
  }

  private static void StopGpuSampler() {
    _gpuSamplerRunning = false;
    if (_gpuSamplerThread != null && !_gpuSamplerThread.Join(2000))
      MelonLogger.Warning("GPU sampler did not stop within two seconds.");
    _gpuSamplerThread = null;
    _gpuSamplerThreadId = 0;
  }

  private static void GpuSamplerLoop() {
    _gpuSamplerThreadId = CpuSampler.GetCurrentNativeThreadId();
    try {
      RefreshGpuCounters();
      _gpuSamplerStatus = _gpuCounters.Length == 0
          ? "no process GPU-engine counters found"
          : "active";
      var period = Math.Max(1L, Stopwatch.Frequency / GpuSamplesPerSecond);
      var next = Stopwatch.GetTimestamp();
      while (_gpuSamplerRunning && _gpuSampleCount < _gpuSamples.Length) {
        var timestamp = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _gpuSampleTicks);
        if (_gpuQuery == IntPtr.Zero ||
            PdhCollectQueryData(_gpuQuery) != 0)
          break;
        for (var i = 0; i < _gpuCounters.Length; i++) {
          if (PdhGetFormattedCounterValue(
                  _gpuCounters[i].Handle, PdhFormatDouble,
                  out _, out var counterValue
              ) != 0 || counterValue.Status != 0)
            continue;
          var value = unchecked((float)counterValue.DoubleValue);
          if (value < 0.01f)
            continue;
          var index = Interlocked.Increment(ref _gpuSampleCount) - 1;
          if (index < 0 || index >= _gpuSamples.Length)
            break;
          _gpuSamples[index] = new GpuEngineSample {
            Timestamp = timestamp,
            CounterIndex = i,
            UtilizationPercent = value,
          };
        }
        next += period;
        while (_gpuSamplerRunning) {
          var remaining = next - Stopwatch.GetTimestamp();
          if (remaining <= 0)
            break;
          Thread.Sleep(remaining > Stopwatch.Frequency / 100 ? 5 : 1);
        }
      }
    } catch (Exception exception) {
      _gpuSamplerStatus = exception.GetType().Name + ": " + exception.Message;
      MelonLogger.Warning(
          "Windows GPU-engine sampling is unavailable: " +
          exception.Message
      );
    }
  }

  private static void RefreshGpuCounters() {
    DisposeGpuCounters();
    var wildcard = "\\GPU Engine(*pid_" +
                   Process.GetCurrentProcess().Id +
                   "*)\\Utilization Percentage";
    var paths = ExpandPdhWildcard(wildcard);
    Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
    var status = PdhOpenQuery(null, IntPtr.Zero, out _gpuQuery);
    if (status != 0)
      throw new InvalidOperationException(
          "PdhOpenQuery failed with 0x" + status.ToString("X8") + "."
      );
    var counters = new List<LiveGpuCounter>();
    for (var i = 0; i < paths.Length && counters.Count < MaximumGpuCounters;
         i++) {
      var path = paths[i];
      status = PdhAddEnglishCounter(
          _gpuQuery, path, IntPtr.Zero, out var handle
      );
      if (status != 0 || handle == IntPtr.Zero)
        continue;
      var name = ExtractPdhInstance(path);
      counters.Add(new LiveGpuCounter(
          handle, new GpuEngineCounterInfo {
            InstanceName = name,
            EngineType = ParseEngineType(name),
            LuidText = ParseLuidText(name),
            Luid = ParseLuid(name),
          }
      ));
    }
    _gpuCounters = counters.ToArray();
    if (_gpuCounters.Length > 0)
      PdhCollectQueryData(_gpuQuery);
  }

  private static string[] ExpandPdhWildcard(string wildcard) {
    uint length = 0;
    var status = PdhExpandWildCardPath(
        null, wildcard, IntPtr.Zero, ref length, 0
    );
    if (status != PdhMoreData && status != 0)
      return new string[0];
    if (length == 0)
      return new string[0];
    var buffer = Marshal.AllocHGlobal(unchecked((int)length * 2));
    try {
      status = PdhExpandWildCardPath(
          null, wildcard, buffer, ref length, 0
      );
      if (status != 0)
        return new string[0];
      var paths = new List<string>();
      var offset = 0;
      while (offset < length) {
        var path = Marshal.PtrToStringUni(
            new IntPtr(buffer.ToInt64() + offset * 2)
        );
        if (string.IsNullOrEmpty(path))
          break;
        paths.Add(path);
        offset += path.Length + 1;
      }
      return paths.ToArray();
    } finally {
      Marshal.FreeHGlobal(buffer);
    }
  }

  private static string ExtractPdhInstance(string path) {
    var start = path.IndexOf('(');
    var end = path.LastIndexOf(')');
    return start < 0 || end <= start
        ? path
        : path.Substring(start + 1, end - start - 1);
  }

  private static string ParseEngineType(string instance) {
    const string marker = "engtype_";
    var offset = instance.IndexOf(
        marker, StringComparison.OrdinalIgnoreCase
    );
    return offset < 0 ? "" : instance.Substring(offset + marker.Length);
  }

  private static string ParseLuidText(string instance) {
    const string marker = "luid_";
    var offset = instance.IndexOf(
        marker, StringComparison.OrdinalIgnoreCase
    );
    if (offset < 0)
      return "";
    offset += marker.Length;
    var end = instance.IndexOf("_phys_", offset, StringComparison.OrdinalIgnoreCase);
    return end < 0
        ? instance.Substring(offset)
        : instance.Substring(offset, end - offset);
  }

  private static long ParseLuid(string instance) {
    var text = ParseLuidText(instance);
    var parts = text.Split('_');
    if (parts.Length != 2)
      return 0;
    if (!uint.TryParse(
            parts[0].Replace("0x", ""), NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out var high
        ) || !uint.TryParse(
            parts[1].Replace("0x", ""), NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out var low
        ))
      return 0;
    return ((long)high << 32) | low;
  }

  private static void DisposeGpuCounters() {
    _gpuCounters = new LiveGpuCounter[0];
    if (_gpuQuery != IntPtr.Zero) {
      PdhCloseQuery(_gpuQuery);
      _gpuQuery = IntPtr.Zero;
    }
  }

  private static GraphicsAdapterInfo[] EnumerateAdapters() {
    var factoryId = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
    var result = CreateDXGIFactory1(ref factoryId, out var factory);
    if (result < 0 || factory == IntPtr.Zero)
      Marshal.ThrowExceptionForHR(result);
    var adapters = new List<GraphicsAdapterInfo>();
    try {
      var enumAdapters = GetVtableDelegate<EnumAdapters1Delegate>(factory, 12);
      for (uint index = 0; index < 32; index++) {
        var hr = enumAdapters(factory, index, out var adapter);
        if (hr == DxgiErrorNotFound)
          break;
        if (hr < 0)
          Marshal.ThrowExceptionForHR(hr);
        try {
          var getDescription = GetVtableDelegate<GetDesc1Delegate>(adapter, 10);
          hr = getDescription(adapter, out var description);
          if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);
          adapters.Add(new GraphicsAdapterInfo {
            Index = unchecked((int)index),
            Name = description.Description ?? "",
            VendorId = description.VendorId,
            DeviceId = description.DeviceId,
            DedicatedVideoMemory = description.DedicatedVideoMemory.ToUInt64(),
            Luid = ((long)description.AdapterLuid.HighPart << 32) |
                   description.AdapterLuid.LowPart,
          });
        } finally {
          Marshal.Release(adapter);
        }
      }
    } finally {
      Marshal.Release(factory);
    }
    return adapters.ToArray();
  }

  private static T GetVtableDelegate<T>(IntPtr instance, int slot)
      where T : class {
    var vtable = Marshal.ReadIntPtr(instance);
    var address = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
    return Marshal.GetDelegateForFunctionPointer(address, typeof(T)) as T;
  }

  private static void VerifyBytes(IntPtr target, byte[] expected) {
    for (var i = 0; i < expected.Length; i++) {
      if (Marshal.ReadByte(target, i) != expected[i])
        throw new InvalidOperationException(
            "Unity graphics-device machine code does not match the " +
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
            "Could not remove the graphics pending-work detour cleanly: " +
            exception.Message
        );
      }
      _detour = null;
    }
    _original = null;
    _hookDelegate = null;
  }

  private const int DxgiErrorNotFound = unchecked((int)0x887A0002);
  private const int PdhMoreData = unchecked((int)0x800007D2);
  private const uint PdhFormatDouble = 0x00000200;

  [StructLayout(LayoutKind.Explicit)]
  private struct PdhFormattedCounterValue {
    [FieldOffset(0)] public uint Status;
    [FieldOffset(8)] public double DoubleValue;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct Luid {
    public uint LowPart;
    public int HighPart;
  }

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  private struct DxgiAdapterDescription1 {
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Description;
    public uint VendorId;
    public uint DeviceId;
    public uint SubSystemId;
    public uint Revision;
    public UIntPtr DedicatedVideoMemory;
    public UIntPtr DedicatedSystemMemory;
    public UIntPtr SharedSystemMemory;
    public Luid AdapterLuid;
    public uint Flags;
  }

  [UnmanagedFunctionPointer(CallingConvention.StdCall)]
  private delegate int EnumAdapters1Delegate(
      IntPtr factory, uint adapterIndex, out IntPtr adapter
  );

  [UnmanagedFunctionPointer(CallingConvention.StdCall)]
  private delegate int GetDesc1Delegate(
      IntPtr adapter, out DxgiAdapterDescription1 description
  );

  [DllImport("dxgi.dll", CallingConvention = CallingConvention.StdCall)]
  private static extern int CreateDXGIFactory1(
      ref Guid interfaceId, out IntPtr factory
  );

  [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
  private static extern int PdhExpandWildCardPath(
      string dataSource, string wildcardPath, IntPtr expandedPathList,
      ref uint pathListLength, uint flags
  );

  [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
  private static extern int PdhOpenQuery(
      string dataSource, IntPtr userData, out IntPtr query
  );

  [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
  private static extern int PdhAddEnglishCounter(
      IntPtr query, string fullCounterPath, IntPtr userData,
      out IntPtr counter
  );

  [DllImport("pdh.dll")]
  private static extern int PdhCollectQueryData(IntPtr query);

  [DllImport("pdh.dll")]
  private static extern int PdhGetFormattedCounterValue(
      IntPtr counter, uint format, out uint counterType,
      out PdhFormattedCounterValue value
  );

  [DllImport("pdh.dll")]
  private static extern int PdhCloseQuery(IntPtr query);
}
#endif
