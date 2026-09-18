#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using MelonLoader;
using UnityEngine;
using UnityEngine.Profiling;
using Valve.VR;

namespace Sst.BoneworksPerformance;

internal sealed class PerformanceCapture {
  private static PerformanceCapture Active;
  private static readonly string[] BasicMarkerNames = {
    "PlayerLoop",
    "Update.ScriptRunBehaviourUpdate",
    "PreLateUpdate.ScriptRunBehaviourLateUpdate",
    "FixedUpdate.ScriptRunBehaviourFixedUpdate",
    "Physics.Simulate",
    "Physics.Processing",
    "Camera.Render",
    "RenderLoop.Draw",
    "Gfx.WaitForPresentOnGfxThread",
    "WaitForTargetFPS",
    "GC.Alloc",
  };

  private static readonly string[] DetailedMarkerNames = {
    "EarlyUpdate",
    "FixedUpdate",
    "PreUpdate",
    "Update",
    "PreLateUpdate",
    "PostLateUpdate",
    "BehaviourUpdate",
    "DirectorUpdate",
    "Physics.FetchResults",
    "Physics.ProcessReports",
    "Physics.SyncColliderTransform",
    "Physics.UpdateBodies",
    "CullScriptable",
    "Camera.Render",
    "RenderLoop.Draw",
    "BatchRenderer.Flush",
    "Gfx.ProcessCommands",
    "Gfx.WaitForGfxCommandsFromMainThread",
    "Gfx.WaitForPresentOnGfxThread",
    "Semaphore.WaitForSignal",
    "WaitForTargetFPS",
    "GC.Collect",
    "GC.Alloc",
  };

  private readonly Process _process = Process.GetCurrentProcess();
  private readonly int _mainThreadId;
  private FrameSample[] _frames;
  private double[] _markerMilliseconds;
  private int[] _markerSampleCounts;
  private UnityMarker[] _markers;
  private string[] _markerNames;
  private CpuSampler _cpuSampler;
  private readonly long[] _playerLoopPhaseStarts =
      new long[(int)PlayerLoopPhase.Count];
  private readonly double[] _playerLoopPhaseMilliseconds =
      new double[(int)PlayerLoopPhase.Count];
  private double[] _playerLoopPhaseSamples;
  private volatile int _currentPlayerLoopPhase;
  private int _frameCount;
  private int _fixedUpdates;
  private long _startTimestamp;
  private long _lastFrameTimestamp;
  private long _lastProcessCpuTicks;
  private string _sessionDirectory;
  private string _unityProfilerPath;
  private PoseHistoryRecorder _poseHistory;

  public bool IsActive { get; private set; }
  public string Mode { get; private set; }
  public CaptureOptions Options { get; private set; }
  public bool IsDetailed => Mode == "Detailed";
  public static PlayerLoopPhase CurrentPlayerLoopPhase {
    get {
      var capture = Active;
      return capture == null
          ? PlayerLoopPhase.None
          : (PlayerLoopPhase)capture._currentPlayerLoopPhase;
    }
  }

  public PerformanceCapture(int mainThreadId) {
    _mainThreadId = mainThreadId;
  }

  public void Start(
      CaptureMode mode, float maximumSeconds, CaptureOptions options
  ) {
    if (IsActive)
      return;

    var duration = Math.Max(1f, Math.Min(maximumSeconds, 1800f));
    var capacity = (int)Math.Ceiling(duration * 180f);
    _frames = new FrameSample[capacity];
    _playerLoopPhaseSamples = new double[
        capacity * (int)PlayerLoopPhase.Count
    ];
    _frameCount = 0;
    _fixedUpdates = 0;
    Mode = mode == CaptureMode.Detailed
        ? "Detailed"
        : mode == CaptureMode.Basic ? "Basic" : "MetricsOnly";
    Options = options.Clone();

    var markerNames = mode == CaptureMode.Detailed
        ? JoinMarkerNames(BasicMarkerNames, DetailedMarkerNames)
        : BasicMarkerNames;
    _markers = new UnityMarker[markerNames.Length];
    for (var i = 0; i < markerNames.Length; i++)
      _markers[i] = new UnityMarker(markerNames[i]);
    var availableMarkerCount = 0;
    for (var i = 0; i < _markers.Length; i++) {
      if (_markers[i].IsAvailable)
        availableMarkerCount++;
    }
    if (availableMarkerCount == 0) {
      MelonLogger.Warning(
          "Unity Recorder.Get is stripped in this player; using native CPU sampling."
      );
    }
    _markerNames = new string[_markers.Length];
    for (var i = 0; i < _markers.Length; i++)
      _markerNames[i] = _markers[i].Name;
    _markerMilliseconds = new double[capacity * _markerNames.Length];
    _markerSampleCounts = new int[capacity * _markerNames.Length];

    _sessionDirectory = Path.Combine(
        MelonUtils.UserDataDirectory, BuildInfo.NAME, "profiles",
        DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")
    );
    Directory.CreateDirectory(_sessionDirectory);

    if (Options.EnableUnityBinaryProfiler) {
      _unityProfilerPath = Path.Combine(
          _sessionDirectory, "unity-profiler.data"
      );
      try {
        Profiler.logFile = _unityProfilerPath;
        Profiler.enableBinaryLog = true;
        Profiler.enabled = true;
      } catch (Exception exception) {
        MelonLogger.Warning(
            "Unity binary profiler could not start: " + exception.Message
        );
        _unityProfilerPath = null;
      }
    } else {
      _unityProfilerPath = null;
    }

    _startTimestamp = Stopwatch.GetTimestamp();
    _lastFrameTimestamp = _startTimestamp;
    _lastProcessCpuTicks = _process.TotalProcessorTime.Ticks;
    _cpuSampler = Options.EnableCpuSampling && mode != CaptureMode.MetricsOnly
        ? new CpuSampler(_mainThreadId, mode, duration)
        : null;
    _poseHistory = Options.EnablePoseHistory && mode != CaptureMode.MetricsOnly
        ? PoseHistoryRecorder.Start(
            duration, Options.PoseHistorySampleRate,
            Options.PoseHistoryInterpolationDelayTicks, _startTimestamp
        )
        : null;
    IsActive = true;
    Active = this;
    if (mode == CaptureMode.Detailed && Options.EnableCompletionWaits)
      CompletionWaitProfiler.Start(_mainThreadId, duration);
    if (mode == CaptureMode.Detailed && Options.EnableGraphics)
      GraphicsProfiler.Start(_mainThreadId, duration);
    if (Options.EnableMonoGcEvents)
      MonoGcProfiler.Start(duration);
    if (mode == CaptureMode.Detailed && Options.EnableTargetedDiagnostics)
      TargetedDiagnostics.Start();
    if (_cpuSampler != null)
      _cpuSampler.Start();
    MelonLogger.Msg(
        $"Profiler started in {Mode.ToLowerInvariant()} mode. " +
        $"Preset: {Options.Preset}; capacity: {capacity} frames; " +
        "CPU sampling: " +
        (_cpuSampler == null ? "disabled." :
            _cpuSampler.SamplesPerSecond + " Hz.")
    );
  }

  public void NoteFixedUpdate() {
    _fixedUpdates++;
    _poseHistory?.RecordFixedTick();
  }

  public bool CaptureFrame() {
    if (!IsActive || _frameCount >= _frames.Length)
      return false;

    var captureStarted = Stopwatch.GetTimestamp();
    var timestamp = Stopwatch.GetTimestamp();
    var processCpuTicks = _process.TotalProcessorTime.Ticks;
    var self = ProfilerSelfMetrics.Consume();
    var targeted = IsDetailed && Options.EnableTargetedDiagnostics
        ? TargetedDiagnostics.ConsumeFrame()
        : new TargetedDiagnosticFrame();
    var frame = new FrameSample {
      Frame = Time.frameCount,
      ElapsedMs = TicksToMilliseconds(timestamp - _startTimestamp),
      FrameMs = TicksToMilliseconds(timestamp - _lastFrameTimestamp),
      ProcessCpuMs = TimeSpan.FromTicks(
          processCpuTicks - _lastProcessCpuTicks
      ).TotalMilliseconds,
      FixedUpdates = _fixedUpdates,
      FixedUpdateMs =
          _playerLoopPhaseMilliseconds[(int)PlayerLoopPhase.FixedUpdate],
      PhysicsFixedUpdateMs =
          _playerLoopPhaseMilliseconds[
              (int)PlayerLoopPhase.PhysicsFixedUpdate
          ],
      ScriptFixedUpdateMs =
          _playerLoopPhaseMilliseconds[(int)PlayerLoopPhase.ScriptFixedUpdate],
      SimulationDebtMs = Math.Max(
          0.0, (Time.time - Time.fixedTime) * 1000.0
      ),
      FixedDeltaMs = Time.fixedDeltaTime * 1000.0,
      ManagedHeapCapacityBytes = MonoGcProfiler.HeapSize,
      ManagedHeapBytes = MonoGcProfiler.UsedSize,
      TotalAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
      GcGen0Collections = MonoGcProfiler.CollectionCount(0),
      GcGen1Collections = MonoGcProfiler.CollectionCount(1),
      GcGen2Collections = MonoGcProfiler.CollectionCount(2),
      PhysBodyUpdateCollidersCalls = targeted.PhysBodyCalls,
      PhysBodyUnchangedColliderCalls = targeted.PhysBodyUnchangedCalls,
      PhysBodyUpdateCollidersMs = TicksToMilliseconds(targeted.PhysBodyTicks),
      ExceptionCount = targeted.Exceptions,
      ProfilerUpdateMs = ProfilerSelfMetrics.Milliseconds(self.UpdateTicks),
      ProfilerFixedUpdateMs =
          ProfilerSelfMetrics.Milliseconds(self.FixedUpdateTicks),
      ProfilerGuiMs = ProfilerSelfMetrics.Milliseconds(self.GuiTicks),
      ProfilerGuiHeapDeltaBytes = self.GuiManagedHeapDelta,
      ProfilerGuiCalls = self.GuiCalls,
      ProfilerGuiLayoutCalls = self.GuiLayoutCalls,
      ProfilerGuiRepaintCalls = self.GuiRepaintCalls,
      ProfilerGuiOtherCalls = self.GuiOtherCalls,
    };
    Array.Copy(
        _playerLoopPhaseMilliseconds, 0, _playerLoopPhaseSamples,
        _frameCount * (int)PlayerLoopPhase.Count,
        (int)PlayerLoopPhase.Count
    );
    _fixedUpdates = 0;
    Array.Clear(
        _playerLoopPhaseMilliseconds, 0,
        _playerLoopPhaseMilliseconds.Length
    );
    _lastFrameTimestamp = timestamp;
    _lastProcessCpuTicks = processCpuTicks;

    CaptureCompositorTiming(ref frame);
    var markerOffset = _frameCount * _markerNames.Length;
    for (var i = 0; i < _markers.Length; i++) {
      _markerMilliseconds[markerOffset + i] =
          _markers[i].ElapsedMilliseconds;
      _markerSampleCounts[markerOffset + i] = _markers[i].SampleCount;
    }
    frame.ProfilerCaptureMs = TicksToMilliseconds(
        Stopwatch.GetTimestamp() - captureStarted
    );
    _frames[_frameCount] = frame;
    _frameCount++;
    _poseHistory?.ObserveFrame(timestamp - _startTimestamp);
    return _frameCount < _frames.Length;
  }

  public void Stop() {
    if (!IsActive)
      return;
    IsActive = false;
    Active = null;
    var graphics = IsDetailed && Options.EnableGraphics
        ? GraphicsProfiler.Stop()
        : GraphicsProfiler.EmptyResult();
    if (_cpuSampler != null)
      _cpuSampler.Stop();
    var completionWaits = IsDetailed && Options.EnableCompletionWaits
        ? CompletionWaitProfiler.Stop()
        : new CompletionWaitResult(
            new CompletionWaitSample[0], 0, false
        );
    var monoGc = Options.EnableMonoGcEvents
        ? MonoGcProfiler.Stop()
        : MonoGcProfiler.EmptyResult();
    var targetedDiagnostics = IsDetailed && Options.EnableTargetedDiagnostics
        ? TargetedDiagnostics.Stop()
        : TargetedDiagnostics.EmptyResult();
    var poseHistory = _poseHistory == null
        ? PoseHistoryRecorder.EmptyResult("disabled")
        : _poseHistory.Stop();
    var threadNames = _cpuSampler == null
        ? new Dictionary<int, string>()
        : _cpuSampler.CopyThreadNames();

    if (_unityProfilerPath != null) {
      try {
        Profiler.enabled = false;
        Profiler.enableBinaryLog = false;
        Profiler.logFile = "";
      } catch (Exception exception) {
        MelonLogger.Warning(
            "Unity binary profiler could not stop cleanly: " + exception.Message
        );
      }
    }
    foreach (var marker in _markers)
      marker.Disable();

    var result = new CaptureResult(
        Mode, Options, _sessionDirectory, _unityProfilerPath, _startTimestamp,
        _frames, _frameCount,
        _markerNames, _markerMilliseconds, _markerSampleCounts,
        _cpuSampler == null ? new CpuSample[0] : _cpuSampler.Samples,
        _cpuSampler == null ? 0 : _cpuSampler.SampleCount,
        _cpuSampler == null ? 0 : _cpuSampler.SamplesPerSecond,
        _cpuSampler == null ? new long[0] : _cpuSampler.StackAddresses,
        _cpuSampler == null ? 0 : _cpuSampler.StackAddressCount,
        _cpuSampler == null ? 0.0 : _cpuSampler.AverageSamplingMicroseconds,
        _cpuSampler == null ? 0.0 : _cpuSampler.AverageSuspensionMicroseconds,
        _cpuSampler == null ? 0.0 : _cpuSampler.MaximumSuspensionMicroseconds,
        completionWaits, graphics, monoGc, targetedDiagnostics, threadNames,
        _playerLoopPhaseSamples, poseHistory
    );
    MelonLogger.Msg(
        $"Profiler stopped after {_frameCount} frames. Writing report in the background."
    );
    ThreadPool.QueueUserWorkItem(_ => ReportWriter.Write(result));

    _frames = null;
    _markerMilliseconds = null;
    _markerSampleCounts = null;
    _markers = null;
    _markerNames = null;
    _cpuSampler = null;
    _poseHistory = null;
    Options = null;
    _playerLoopPhaseSamples = null;
  }

  private static string[] JoinMarkerNames(string[] first, string[] second) {
    var result = new string[first.Length + second.Length];
    Array.Copy(first, result, first.Length);
    var count = first.Length;
    for (var i = 0; i < second.Length; i++) {
      var duplicate = false;
      for (var j = 0; j < first.Length; j++) {
        if (first[j] == second[i]) {
          duplicate = true;
          break;
        }
      }
      if (!duplicate)
        result[count++] = second[i];
    }
    if (count == result.Length)
      return result;
    Array.Resize(ref result, count);
    return result;
  }

  private static void CaptureCompositorTiming(ref FrameSample frame) {
    try {
      var compositor = OpenVR.Compositor;
      if (compositor == null)
        return;
      var timing = new Compositor_FrameTiming {
        m_nSize = (uint)Marshal.SizeOf(typeof(Compositor_FrameTiming)),
      };
      if (!compositor.GetFrameTiming(ref timing, 0))
        return;

      frame.HasCompositorTiming = true;
      frame.ClientFrameIntervalMs = timing.m_flClientFrameIntervalMs;
      frame.CompositorCpuMs = timing.m_flCompositorRenderCpuMs;
      frame.CompositorGpuMs = timing.m_flCompositorRenderGpuMs;
      frame.CompositorIdleCpuMs = timing.m_flCompositorIdleCpuMs;
      frame.PresentWaitCpuMs = timing.m_flWaitForPresentCpuMs;
      frame.TotalRenderGpuMs = timing.m_flTotalRenderGpuMs;
      frame.PreSubmitGpuMs = timing.m_flPreSubmitGpuMs;
      frame.PostSubmitGpuMs = timing.m_flPostSubmitGpuMs;
      frame.DroppedFrames = (int)timing.m_nNumDroppedFrames;
    } catch {
      // SteamVR is optional for the headless smoke test.
    }
  }

  private static double TicksToMilliseconds(long ticks) =>
      ticks * 1000.0 / Stopwatch.Frequency;

  public static void BeginPlayerLoopPhase(PlayerLoopPhase phase) {
    var capture = Active;
    if (capture != null) {
      capture._currentPlayerLoopPhase = (int)phase;
      capture._playerLoopPhaseStarts[(int)phase] = Stopwatch.GetTimestamp();
    }
  }

  public static void EndPlayerLoopPhase(PlayerLoopPhase phase) {
    var capture = Active;
    if (capture == null)
      return;
    var index = (int)phase;
    var start = capture._playerLoopPhaseStarts[index];
    if (start == 0)
      return;
    capture._playerLoopPhaseMilliseconds[index] +=
        TicksToMilliseconds(Stopwatch.GetTimestamp() - start);
    capture._playerLoopPhaseStarts[index] = 0;
    capture._currentPlayerLoopPhase = IsFixedSubphase(phase)
        ? (int)PlayerLoopPhase.FixedUpdate
        : (int)PlayerLoopPhase.None;
    if (phase == PlayerLoopPhase.PostLateUpdate)
      Mod.OnProfilerFrameBoundary();
  }

  private static bool IsFixedSubphase(PlayerLoopPhase phase) =>
      phase >= PlayerLoopPhase.FixedUpdateClearLines &&
      phase <= PlayerLoopPhase.XRFixedUpdate;
}

internal sealed class CaptureResult {
  public readonly string Mode;
  public readonly CaptureOptions Options;
  public readonly string Directory;
  public readonly string UnityProfilerPath;
  public readonly long StartTimestamp;
  public readonly FrameSample[] Frames;
  public readonly int FrameCount;
  public readonly string[] MarkerNames;
  public readonly double[] MarkerMilliseconds;
  public readonly int[] MarkerSampleCounts;
  public readonly CpuSample[] CpuSamples;
  public readonly int CpuSampleCount;
  public readonly int CpuSamplesPerSecond;
  public readonly long[] StackAddresses;
  public readonly int StackAddressCount;
  public readonly double AverageSamplingMicroseconds;
  public readonly double AverageSuspensionMicroseconds;
  public readonly double MaximumSuspensionMicroseconds;
  public readonly CompletionWaitResult CompletionWaits;
  public readonly GraphicsProfileResult Graphics;
  public readonly MonoGcProfileResult MonoGc;
  public readonly TargetedDiagnosticResult TargetedDiagnostics;
  public readonly Dictionary<int, string> ThreadNames;
  public readonly double[] PlayerLoopPhaseMilliseconds;
  public readonly PoseHistoryResult PoseHistory;

  public CaptureResult(
      string mode, CaptureOptions options, string directory,
      string unityProfilerPath,
      long startTimestamp,
      FrameSample[] frames, int frameCount, string[] markerNames,
      double[] markerMilliseconds, int[] markerSampleCounts,
      CpuSample[] cpuSamples, int cpuSampleCount, int cpuSamplesPerSecond,
      long[] stackAddresses, int stackAddressCount,
      double averageSamplingMicroseconds,
      double averageSuspensionMicroseconds,
      double maximumSuspensionMicroseconds,
      CompletionWaitResult completionWaits,
      GraphicsProfileResult graphics,
      MonoGcProfileResult monoGc,
      TargetedDiagnosticResult targetedDiagnostics,
      Dictionary<int, string> threadNames,
      double[] playerLoopPhaseMilliseconds,
      PoseHistoryResult poseHistory
  ) {
    Mode = mode;
    Options = options;
    Directory = directory;
    UnityProfilerPath = unityProfilerPath;
    StartTimestamp = startTimestamp;
    Frames = frames;
    FrameCount = frameCount;
    MarkerNames = markerNames;
    MarkerMilliseconds = markerMilliseconds;
    MarkerSampleCounts = markerSampleCounts;
    CpuSamples = cpuSamples;
    CpuSampleCount = cpuSampleCount;
    CpuSamplesPerSecond = cpuSamplesPerSecond;
    StackAddresses = stackAddresses;
    StackAddressCount = stackAddressCount;
    AverageSamplingMicroseconds = averageSamplingMicroseconds;
    AverageSuspensionMicroseconds = averageSuspensionMicroseconds;
    MaximumSuspensionMicroseconds = maximumSuspensionMicroseconds;
    CompletionWaits = completionWaits;
    Graphics = graphics;
    MonoGc = monoGc;
    TargetedDiagnostics = targetedDiagnostics;
    ThreadNames = threadNames;
    PlayerLoopPhaseMilliseconds = playerLoopPhaseMilliseconds;
    PoseHistory = poseHistory;
  }
}
#endif
