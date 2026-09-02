using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;
using MelonLoader;
using StressLevelZero.Rig;
using UnhollowerBaseLib;
using UnityEngine;
using Valve.VR;
using RigController = StressLevelZero.Rig.Controller;

namespace Sst.BoneworksPerformance;

#if DEBUG
internal struct FixedPoseInputDiagnostic {
  public int UnityFrame;
  public int TickInFrame;
  public float FixedTime;
  public float FixedDeltaTime;
  public float TimeScale;
  public long SimulationTimestamp;
  public long IndependentlyMappedTimestamp;
  public long TargetTimestamp;
  public long PreviousTargetTimestamp;
  public long TargetAgeTicks;
  public long MaximumHistoryAgeTicks;
  public long BeforeTimestamp;
  public long AfterTimestamp;
  public long BeforeSequence;
  public long AfterSequence;
  public float InterpolationAmount;
  public PoseBracketStatus Status;
  public bool TimelineReanchored;
  public long TimelineCorrectionTicks;
  public bool HeadVerticalVelocityAvailable;
  public float HeadVerticalVelocity;
  public bool JumpProtectionActive;
  public bool ForwardCorrectionSuppressed;
  public bool JumpCatchUpDebtActive;
  public bool HeadApplied;
  public bool LeftApplied;
  public bool RightApplied;
  public Vector3 HeadPosition;
  public Quaternion HeadRotation;
  public Vector3 LeftPosition;
  public Quaternion LeftRotation;
  public Vector3 RightPosition;
  public Quaternion RightRotation;
}
#endif

internal sealed class FixedUpdatePoseHistory {
  private const int HistoryCapacity = 4096;
#if DEBUG
  private const int DiagnosticCapacity = 16384;
#endif
  private static FixedUpdatePoseHistory _instance;
#if DEBUG
  private static int _samplerNativeThreadId;
#endif

  private readonly bool _enabled;
  private readonly int _sampleRate;
  private readonly int _delayTicks;
  private readonly bool _protectRisingHeadFromCatchUp;
  private readonly float _jumpRiseSpeedThreshold;
  private readonly long _jumpRiseWindowTicks;
  private readonly PoseHistoryBuffer _history =
      new PoseHistoryBuffer(HistoryCapacity);
  private readonly FixedTimeMapper _timeMapper = new FixedTimeMapper();
  private readonly FixedTickTimeline _tickTimeline;
  private readonly AutoResetEvent _stop = new AutoResetEvent(false);
#if DEBUG
  private readonly FixedPoseInputDiagnostic[] _diagnostics =
      new FixedPoseInputDiagnostic[DiagnosticCapacity];
  private FixedPoseSmoothnessTest _smoothnessTest;
  private SuperJumpDiagnostics _superJumpDiagnostics;
#endif
  private CVRSystem _system;
  private Thread _thread;
  private volatile bool _running;
  private int _leftDeviceIndex = -1;
  private int _rightDeviceIndex = -1;
  private int _queryErrors;
  private int _scheduleOverruns;
#if DEBUG
  private int _diagnosticCount;
  private int _diagnosticsDropped;
  private bool _recordDiagnostics;
#endif
  private int _lastUnityFrame = -1;
  private int _tickInFrame;
  private int _appliedTicks;
  private int _unavailableTicks;
  private long _lastTargetTimestamp;
  private long _lastSummaryTimestamp;
  private long _lastRenderedFrameTimestamp;
  private bool _jumpCatchUpDebt;
  private readonly List<TimedButtonEdge> _jumpButtonEdges =
      new List<TimedButtonEdge>(32);
  private bool _jumpButtonObserved;
  private bool _jumpButtonInitialState;
  private bool _jumpButtonLiveState;
  private long _pendingJumpReleaseTimestamp;
  private bool _deliveringQueuedJump;
  private SteamControllerRig _currentSteamRig;
  private CurrentFixedPose _current;

  private struct CurrentFixedPose {
    public bool Attempted;
    public bool Available;
    public int UnityFrame;
    public int TickInFrame;
    public float FixedTime;
    public float FixedDeltaTime;
    public float TimeScale;
    public long SimulationTimestamp;
    public long IndependentlyMappedTimestamp;
    public long TargetTimestamp;
    public long PreviousTargetTimestamp;
    public long TargetAgeTicks;
    public long MaximumHistoryAgeTicks;
    public PoseHistorySample Before;
    public PoseHistorySample After;
    public PoseBracketStatus Status;
    public bool TimelineReanchored;
    public long TimelineCorrectionTicks;
    public bool HeadVerticalVelocityAvailable;
    public float HeadVerticalVelocity;
    public bool JumpProtectionActive;
    public bool ForwardCorrectionSuppressed;
    public bool JumpCatchUpDebtActive;
    public float Amount;
    public TrackedPoseValue Head;
    public bool HeadApplied;
    public bool FixedJumpButtonHeld;
    public bool JumpReleaseDelivered;
    public long JumpReleaseTimestamp;
    public Vector3 HeadPosition;
    public Quaternion HeadRotation;
  }

  public FixedUpdatePoseHistory(
      bool enabled, int sampleRate, int delayTicks,
      float catchUpSpeed,
      bool protectRisingHeadFromCatchUp, float jumpRiseSpeedThreshold,
      float jumpRiseDetectionWindowSeconds
  ) {
    _enabled = enabled;
    _sampleRate = Math.Max(90, Math.Min(1000, sampleRate));
    _delayTicks = Math.Max(1, Math.Min(8, delayTicks));
    _protectRisingHeadFromCatchUp = protectRisingHeadFromCatchUp;
    _jumpRiseSpeedThreshold = Math.Max(
        0.05f, Math.Min(5f, jumpRiseSpeedThreshold)
    );
    _jumpRiseWindowTicks = (long)Math.Round(
        Math.Max(0.02f, Math.Min(0.5f, jumpRiseDetectionWindowSeconds)) *
        Stopwatch.Frequency
    );
    _tickTimeline = new FixedTickTimeline(_timeMapper, catchUpSpeed);
    _instance = this;
  }

#if DEBUG
  public static bool IsInternalThread(int threadId) =>
      threadId != 0 &&
      threadId == Volatile.Read(ref _samplerNativeThreadId);

  public void SetSmoothnessTest(FixedPoseSmoothnessTest test) {
    if (_thread != null)
      throw new InvalidOperationException(
          "The smoothness test must be configured before pose sampling starts."
      );
    _smoothnessTest = test;
  }

  public void SetSuperJumpDiagnostics(SuperJumpDiagnostics diagnostics) {
    _superJumpDiagnostics = diagnostics;
  }
#endif

  public void ObserveRenderedFrame() {
    if (!_enabled)
      return;
    EnsureStarted();
    var now = Stopwatch.GetTimestamp();
    _lastRenderedFrameTimestamp = now;
    _timeMapper.ObserveFrame(now, Time.time, Time.timeScale);
#if DEBUG
    if (_running && now - _lastSummaryTimestamp >=
        Stopwatch.Frequency * 5L) {
      _lastSummaryTimestamp = now;
      MelonLogger.Msg(
          "Fixed-pose input: applied=" + _appliedTicks +
          ", unavailable=" + _unavailableTicks +
          ", historySamples=" + _history.TotalWritten +
          ", queryErrors=" + _queryErrors +
          ", samplerOverruns=" + _scheduleOverruns + "."
      );
    }
#endif
  }

  public void ResetScene() {
    _current = new CurrentFixedPose();
    _lastUnityFrame = -1;
    _tickInFrame = 0;
    _lastTargetTimestamp = 0;
    _lastRenderedFrameTimestamp = 0;
    _jumpCatchUpDebt = false;
    _jumpButtonEdges.Clear();
    _jumpButtonObserved = false;
    _jumpButtonInitialState = false;
    _jumpButtonLiveState = false;
    _pendingJumpReleaseTimestamp = 0;
    _deliveringQueuedJump = false;
    _currentSteamRig = null;
    _tickTimeline.Reset();
  }

  public void Shutdown() {
    _running = false;
    _stop.Set();
    if (_thread != null && !_thread.Join(2000))
      MelonLogger.Warning("Fixed-pose sampler did not stop within two seconds.");
    _thread = null;
#if DEBUG
    _recordDiagnostics = false;
    WriteDiagnostics();
#endif
    if (ReferenceEquals(_instance, this))
      _instance = null;
  }

#if DEBUG
  public void BeginDiagnosticRun() {
    _diagnosticCount = 0;
    _diagnosticsDropped = 0;
    _lastTargetTimestamp = 0;
    _recordDiagnostics = true;
  }

  public void EndDiagnosticRun() {
    _recordDiagnostics = false;
    WriteDiagnostics();
    _diagnosticCount = 0;
    _diagnosticsDropped = 0;
  }
#endif

  private void EnsureStarted() {
    if (_running || _thread != null)
      return;
    try {
#if DEBUG
      if (_smoothnessTest == null) {
#endif
      _system = OpenVR.System;
      if (_system == null)
        return;
      RefreshDeviceIndices();
#if DEBUG
      }
#endif
      _running = true;
      _lastSummaryTimestamp = Stopwatch.GetTimestamp();
      _thread = new Thread(SampleLoop) {
        IsBackground = true,
        Name = "Boneworks fixed-pose history",
        Priority = System.Threading.ThreadPriority.AboveNormal,
      };
      _thread.Start();
      var samplerName = "Fixed-update pose history";
#if DEBUG
      if (_smoothnessTest != null)
        samplerName = "Mock fixed-update pose history";
#endif
      MelonLogger.Msg(
          samplerName + " active at " + _sampleRate +
          " Hz with " + _delayTicks + " tick interpolation delay."
      );
    } catch (Exception exception) {
      _system = null;
      _running = false;
      _thread = null;
      MelonLogger.Warning(
          "Could not start fixed-update pose history: " + exception.Message
      );
    }
  }

  private void SampleLoop() {
#if DEBUG
    Volatile.Write(
        ref _samplerNativeThreadId,
        unchecked((int)GetCurrentThreadId())
    );
    if (_smoothnessTest != null) {
      try {
        SampleSyntheticLoop();
      } finally {
        _running = false;
        Volatile.Write(ref _samplerNativeThreadId, 0);
      }
      return;
    }
#endif
    try {
      using (Il2CppThreadRegistration.Attach()) {
        var poses = new Il2CppStructArray<TrackedDevicePose_t>(
            OpenVR.k_unMaxTrackedDeviceCount
        );
        var interval = Math.Max(1L, Stopwatch.Frequency / _sampleRate);
        var next = Stopwatch.GetTimestamp();
        var refreshAt = next;
        while (_running) {
          var now = Stopwatch.GetTimestamp();
          var remaining = next - now;
          if (remaining > 0) {
            var waitMs = (int)(remaining * 1000L / Stopwatch.Frequency);
            if (waitMs > 0 && _stop.WaitOne(waitMs))
              break;
            if (waitMs == 0) {
              Thread.SpinWait(32);
              if (_stop.WaitOne(0))
                break;
            }
            continue;
          }
          if (now - next > interval)
            Interlocked.Increment(ref _scheduleOverruns);
          if (now >= refreshAt) {
            RefreshDeviceIndices();
            refreshAt = now + Stopwatch.Frequency;
          }
          SamplePoses(poses);
          next += interval;
          if (now - next > interval * 4)
            next = now + interval;
        }
      }
    } catch (Exception exception) {
      MelonLogger.Warning(
          "Fixed-pose sampler stopped unexpectedly: " + exception.Message
      );
    } finally {
      _running = false;
#if DEBUG
      Volatile.Write(ref _samplerNativeThreadId, 0);
#endif
    }
  }

#if DEBUG
  private void SampleSyntheticLoop() {
    var interval = Math.Max(1L, Stopwatch.Frequency / _sampleRate);
    var next = Stopwatch.GetTimestamp();
    while (_running) {
      var now = Stopwatch.GetTimestamp();
      var remaining = next - now;
      if (remaining > 0) {
        var waitMs = (int)(remaining * 1000L / Stopwatch.Frequency);
        if (waitMs > 0 && _stop.WaitOne(waitMs))
          break;
        if (waitMs == 0) {
          Thread.SpinWait(32);
          if (_stop.WaitOne(0))
            break;
        }
        continue;
      }
      if (now - next > interval)
        Interlocked.Increment(ref _scheduleOverruns);
      var pose = _smoothnessTest.PoseAt(now);
      _history.Add(new PoseHistorySample {
        Timestamp = now,
        HeadDeviceIndex = 0,
        Head = pose,
      });
      next += interval;
      if (now - next > interval * 4)
        next = now + interval;
    }
  }
#endif

  private void RefreshDeviceIndices() {
    _leftDeviceIndex = ToDeviceIndex(
        _system.GetTrackedDeviceIndexForControllerRole(
            ETrackedControllerRole.LeftHand
        )
    );
    _rightDeviceIndex = ToDeviceIndex(
        _system.GetTrackedDeviceIndexForControllerRole(
            ETrackedControllerRole.RightHand
        )
    );
  }

  private void SamplePoses(Il2CppStructArray<TrackedDevicePose_t> poses) {
    var started = Stopwatch.GetTimestamp();
    try {
      _system.GetDeviceToAbsoluteTrackingPose(
          ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, poses
      );
      var ended = Stopwatch.GetTimestamp();
      _history.Add(new PoseHistorySample {
        Timestamp = started + (ended - started) / 2,
        QueryTicks = ended - started,
        HeadDeviceIndex = 0,
        LeftDeviceIndex = _leftDeviceIndex,
        RightDeviceIndex = _rightDeviceIndex,
        Head = ReadPose(poses, 0),
        Left = ReadPose(poses, _leftDeviceIndex),
        Right = ReadPose(poses, _rightDeviceIndex),
      });
    } catch {
      Interlocked.Increment(ref _queryErrors);
    }
  }

  private void PrepareFixedTick(SteamControllerRig rig) {
    _current = new CurrentFixedPose();
    _currentSteamRig = rig;
    if (!_enabled || !_running)
      return;
    var frame = Time.frameCount;
    if (frame != _lastUnityFrame) {
      _lastUnityFrame = frame;
      _tickInFrame = 0;
    } else {
      _tickInFrame++;
    }
    var fixedTime = Time.fixedTime;
    var fixedDelta = Time.fixedDeltaTime;
    var timeScale = Time.timeScale;
    _current = new CurrentFixedPose {
      Attempted = true,
      UnityFrame = frame,
      TickInFrame = _tickInFrame,
      FixedTime = fixedTime,
      FixedDeltaTime = fixedDelta,
      TimeScale = timeScale,
      PreviousTargetTimestamp = _lastTargetTimestamp,
      Status = PoseBracketStatus.Unavailable,
    };
    var headVelocityAvailable = _history.TryGetHeadVerticalVelocity(
        _jumpRiseWindowTicks, Stopwatch.Frequency,
        out var headVerticalVelocity
    );
    var jumpProtectionActive = _protectRisingHeadFromCatchUp &&
                               headVelocityAvailable &&
                               headVerticalVelocity >=
                               _jumpRiseSpeedThreshold;
    if (!_tickTimeline.TryAdvance(
            fixedTime, fixedDelta, timeScale, Stopwatch.Frequency,
            _tickInFrame == 0, jumpProtectionActive,
            out var simulationTimestamp,
            out var independentlyMappedTimestamp, out var reanchored
        )) {
      _unavailableTicks++;
      return;
    }
    _current.TimelineCorrectionTicks =
        _tickTimeline.LastCorrectionTicks;
    _current.HeadVerticalVelocityAvailable = headVelocityAvailable;
    _current.HeadVerticalVelocity = headVerticalVelocity;
    _current.JumpProtectionActive = jumpProtectionActive;
    _current.ForwardCorrectionSuppressed =
        _tickTimeline.LastForwardCorrectionSuppressed;
    if (_current.ForwardCorrectionSuppressed)
      _jumpCatchUpDebt = true;
    var targetTimestamp = simulationTimestamp - (long)Math.Round(
        _delayTicks * fixedDelta / Math.Max(0.000001f, timeScale) *
        Stopwatch.Frequency
    );
    var now = Stopwatch.GetTimestamp();
    var fixedIntervalTicks = (long)Math.Round(
        fixedDelta / Math.Max(0.000001f, timeScale) * Stopwatch.Frequency
    );
    var normalMaximumHistoryAgeTicks = fixedIntervalTicks * _delayTicks +
                                       Stopwatch.Frequency / 4;
    var extendedHistoryAge = jumpProtectionActive || _jumpCatchUpDebt;
    var maximumHistoryAgeTicks = extendedHistoryAge
        ? Math.Max(
            normalMaximumHistoryAgeTicks,
            fixedIntervalTicks * _delayTicks + Stopwatch.Frequency * 2
        )
        : normalMaximumHistoryAgeTicks;
    var targetAgeTicks = Math.Max(0, now - targetTimestamp);
    if (_jumpCatchUpDebt && !jumpProtectionActive &&
        targetAgeTicks <= normalMaximumHistoryAgeTicks)
      _jumpCatchUpDebt = false;
    _current.JumpCatchUpDebtActive = _jumpCatchUpDebt;
    var previousTargetTimestamp = _lastTargetTimestamp;
    _current.SimulationTimestamp = simulationTimestamp;
    _current.IndependentlyMappedTimestamp = independentlyMappedTimestamp;
    _current.TargetTimestamp = targetTimestamp;
    _current.FixedJumpButtonHeld = JumpButtonStateAt(targetTimestamp);
    _current.PreviousTargetTimestamp = previousTargetTimestamp;
    _current.TargetAgeTicks = targetAgeTicks;
    _current.MaximumHistoryAgeTicks = maximumHistoryAgeTicks;
    _current.TimelineReanchored = reanchored;
    _lastTargetTimestamp = targetTimestamp;
    if (targetAgeTicks > maximumHistoryAgeTicks) {
      _current.Status = PoseBracketStatus.Stale;
      _unavailableTicks++;
#if DEBUG
      ApplySyntheticFallback(now);
#endif
      return;
    }
    if (!_history.TryGetBracket(
            targetTimestamp, out var before, out var after, out var status
        )) {
      _current.Before = before;
      _current.After = after;
      _current.Status = status;
      _unavailableTicks++;
#if DEBUG
      ApplySyntheticFallback(now);
#endif
      return;
    }
    if (status == PoseBracketStatus.AfterHistory &&
        _history.TryGetLatestPair(out before, out after)) {
      var predictionTicks = targetTimestamp - after.Timestamp;
      var maximumPredictionTicks = Stopwatch.Frequency / 10;
      if (predictionTicks > 0 && predictionTicks <= maximumPredictionTicks) {
        status = PoseBracketStatus.Extrapolated;
      }
    }
    if (status != PoseBracketStatus.Exact &&
        status != PoseBracketStatus.Interpolated &&
        status != PoseBracketStatus.Extrapolated) {
      _current.Before = before;
      _current.After = after;
      _current.Status = status;
      _unavailableTicks++;
#if DEBUG
      ApplySyntheticFallback(now);
#endif
      return;
    }
    var span = after.Timestamp - before.Timestamp;
    var amount = span <= 0 ? 0f : (float)(
        (targetTimestamp - before.Timestamp) / (double)span
    );
    if (status != PoseBracketStatus.Extrapolated)
      amount = Math.Max(0f, Math.Min(1f, amount));
    var head = status == PoseBracketStatus.Extrapolated
        ? PoseHistoryMath.Extrapolate(before.Head, after.Head, amount)
        : PoseHistoryMath.Interpolate(before.Head, after.Head, amount);
    _current = new CurrentFixedPose {
      Attempted = true,
      Available = true,
      UnityFrame = frame,
      TickInFrame = _tickInFrame,
      FixedTime = fixedTime,
      FixedDeltaTime = fixedDelta,
      TimeScale = timeScale,
      SimulationTimestamp = simulationTimestamp,
      IndependentlyMappedTimestamp = independentlyMappedTimestamp,
      TargetTimestamp = targetTimestamp,
      PreviousTargetTimestamp = previousTargetTimestamp,
      TargetAgeTicks = targetAgeTicks,
      MaximumHistoryAgeTicks = maximumHistoryAgeTicks,
      Before = before,
      After = after,
      Status = status,
      TimelineReanchored = reanchored,
      TimelineCorrectionTicks = _tickTimeline.LastCorrectionTicks,
      HeadVerticalVelocityAvailable = headVelocityAvailable,
      HeadVerticalVelocity = headVerticalVelocity,
      JumpProtectionActive = jumpProtectionActive,
      ForwardCorrectionSuppressed =
          _tickTimeline.LastForwardCorrectionSuppressed,
      JumpCatchUpDebtActive = _jumpCatchUpDebt,
      Amount = amount,
      Head = head,
      FixedJumpButtonHeld = JumpButtonStateAt(targetTimestamp),
    };
  }

  private void ObserveJumpButton(SteamControllerRig rig) {
    if (!_enabled || !rig)
      return;
    var controller = JumpButtonTiming.Controller(rig);
    if (!JumpButtonTiming.UsesAButton(controller))
      return;
    var held = controller.GetAButton();
    if (_jumpButtonObserved && held == _jumpButtonLiveState)
      return;
    var now = Stopwatch.GetTimestamp();
    var timestamp = JumpButtonTiming.ReadEdgeTimestamp(
        rig, now, Time.realtimeSinceStartup
    );
    if (!_jumpButtonObserved) {
      _jumpButtonObserved = true;
      _jumpButtonInitialState = false;
      _jumpButtonLiveState = held;
      if (held)
        AddJumpButtonEdge(timestamp, true);
      return;
    }
    _jumpButtonLiveState = held;
    AddJumpButtonEdge(timestamp, held);
  }

  private void AddJumpButtonEdge(long timestamp, bool pressed) {
    if (_jumpButtonEdges.Count > 0) {
      var last = _jumpButtonEdges[_jumpButtonEdges.Count - 1];
      if (timestamp < last.Timestamp)
        timestamp = last.Timestamp;
      if (last.Timestamp == timestamp && last.Pressed == pressed)
        return;
    }
    _jumpButtonEdges.Add(new TimedButtonEdge(timestamp, pressed));
    if (_jumpButtonEdges.Count <= 96)
      return;
    _jumpButtonInitialState = _jumpButtonEdges[63].Pressed;
    _jumpButtonEdges.RemoveRange(0, 64);
  }

  private bool JumpButtonStateAt(long timestamp) {
    var state = _jumpButtonInitialState;
    for (var i = 0; i < _jumpButtonEdges.Count; i++) {
      if (_jumpButtonEdges[i].Timestamp > timestamp)
        break;
      state = _jumpButtonEdges[i].Pressed;
    }
    return state;
  }

  private bool InterceptJump(ControllerRig rig) {
    if (_deliveringQueuedJump || !_enabled || !rig)
      return true;
    var controller = JumpButtonTiming.Controller(rig);
    if (!JumpButtonTiming.UsesAButton(controller) ||
        !controller.GetAButtonUp())
      return true;
    var now = Stopwatch.GetTimestamp();
    var timestamp = JumpButtonTiming.ReadEdgeTimestamp(
        rig, now, Time.realtimeSinceStartup
    );
    _jumpButtonObserved = true;
    _jumpButtonLiveState = false;
    AddJumpButtonEdge(timestamp, false);
    _pendingJumpReleaseTimestamp = timestamp;
#if DEBUG
    _superJumpDiagnostics?.ObserveJumpRelease(rig, now, timestamp);
#endif
    return false;
  }

  private void DeliverQueuedJump(ControllerRig rig) {
    if (_pendingJumpReleaseTimestamp == 0 || !rig ||
        _current.TargetTimestamp < _pendingJumpReleaseTimestamp)
      return;
    var releaseTimestamp = _pendingJumpReleaseTimestamp;
    _pendingJumpReleaseTimestamp = 0;
    _current.JumpReleaseDelivered = true;
    _current.JumpReleaseTimestamp = releaseTimestamp;
    _deliveringQueuedJump = true;
    try {
      rig.Jump();
    } finally {
      _deliveringQueuedJump = false;
    }
  }

  private void OverrideJumpCharge(
      ControllerRig rig, ref bool chargeInput
  ) {
    if (!_enabled || !_current.Attempted ||
        _current.TargetTimestamp == 0 || !rig)
      return;
    chargeInput = _current.FixedJumpButtonHeld;
  }

#if DEBUG
  private void ApplySyntheticFallback(long timestamp) {
    if (_smoothnessTest == null)
      return;
    _current.Available = true;
    _current.Head = _smoothnessTest.PoseAt(timestamp);
  }
#endif

  private struct ControllerPatchState { }

  private void BeforeController(
      RigController controller, out ControllerPatchState state
  ) {
    state = new ControllerPatchState();
    if (!_current.Available || !controller)
      return;
    if (!_current.HeadApplied)
      ApplyHead(_currentSteamRig);
  }

  private void AfterController(
      RigController controller, ControllerPatchState state
  ) {
    // Controller poses stay live. Only HMD position is replayed.
  }

  private void ApplyHead(SteamControllerRig rig) {
    if (!_current.Available || !rig)
      return;
    var poses = rig.poses;
    var hmd = rig.hmdTransform;
    var target = hmd ? hmd.parent : null;
#if DEBUG
    if (_smoothnessTest != null && target && _current.Head.IsValid) {
      ToUnityPose(_current.Head, out var position, out _);
      target.localPosition = position;
      _current.HeadApplied = true;
      _current.HeadPosition = position;
      _current.HeadRotation = target.localRotation;
      return;
    }
#endif
    if (poses != null && poses.Length > 0 && target &&
        _current.Head.IsValid) {
      var live = ReadPose(poses, 0);
      if (live.IsValid) {
        ApplyCalibratedPosition(
            live, _current.Head, target.localPosition, out var position
        );
        target.localPosition = position;
        _current.HeadApplied = true;
        _current.HeadPosition = position;
        _current.HeadRotation = target.localRotation;
      }
    }
  }

  private void FinishFixedTick() {
    if (!_current.Attempted)
      return;
    if (_current.HeadApplied)
      _appliedTicks++;
#if DEBUG
    if (_superJumpDiagnostics != null) {
      var latestHead = new TrackedPoseValue();
      if (_history.TryGetLatestPair(out _, out var latest))
        latestHead = latest.Head;
      _superJumpDiagnostics.RecordFixedTick(
          _currentSteamRig, _current.UnityFrame, _current.TickInFrame,
          _current.FixedTime, _current.SimulationTimestamp,
          _current.TargetTimestamp, _current.Available, _current.Head,
          _current.HeadPosition, latestHead, _current.TimeScale,
          _current.FixedJumpButtonHeld, _current.JumpReleaseDelivered,
          _current.JumpReleaseTimestamp
      );
    }
    if (_smoothnessTest != null && _smoothnessTest.IsActive) {
      var hmd = _currentSteamRig ? _currentSteamRig.hmdTransform : null;
      var target = hmd ? hmd.parent : null;
      _smoothnessTest.Record(
          _current.UnityFrame, _current.TickInFrame, _current.FixedTime,
          _current.TargetTimestamp, _current.Status, _current.Head,
          _current.HeadApplied, target ? target.localPosition : Vector3.zero,
          _current.HeadVerticalVelocity, _current.JumpProtectionActive,
          _current.ForwardCorrectionSuppressed,
          _current.TimelineCorrectionTicks
      );
    }
#endif
#if DEBUG
    if (_recordDiagnostics)
      RecordDiagnostic();
#endif
    _current.Available = false;
    _currentSteamRig = null;
  }

#if DEBUG
  private void RecordDiagnostic() {
    var index = _diagnosticCount++;
    if (index >= _diagnostics.Length) {
      _diagnosticsDropped++;
      return;
    }
    _diagnostics[index] = new FixedPoseInputDiagnostic {
      UnityFrame = _current.UnityFrame,
      TickInFrame = _current.TickInFrame,
      FixedTime = _current.FixedTime,
      FixedDeltaTime = _current.FixedDeltaTime,
      TimeScale = _current.TimeScale,
      SimulationTimestamp = _current.SimulationTimestamp,
      IndependentlyMappedTimestamp = _current.IndependentlyMappedTimestamp,
      TargetTimestamp = _current.TargetTimestamp,
      PreviousTargetTimestamp = _current.PreviousTargetTimestamp,
      TargetAgeTicks = _current.TargetAgeTicks,
      MaximumHistoryAgeTicks = _current.MaximumHistoryAgeTicks,
      BeforeTimestamp = _current.Before.Timestamp,
      AfterTimestamp = _current.After.Timestamp,
      BeforeSequence = _current.Before.Sequence,
      AfterSequence = _current.After.Sequence,
      InterpolationAmount = _current.Amount,
      Status = _current.Status,
      TimelineReanchored = _current.TimelineReanchored,
      TimelineCorrectionTicks = _current.TimelineCorrectionTicks,
      HeadVerticalVelocityAvailable =
          _current.HeadVerticalVelocityAvailable,
      HeadVerticalVelocity = _current.HeadVerticalVelocity,
      JumpProtectionActive = _current.JumpProtectionActive,
      ForwardCorrectionSuppressed =
          _current.ForwardCorrectionSuppressed,
      JumpCatchUpDebtActive = _current.JumpCatchUpDebtActive,
      HeadApplied = _current.HeadApplied,
      LeftApplied = false,
      RightApplied = false,
      HeadPosition = _current.HeadPosition,
      HeadRotation = _current.HeadRotation,
      LeftPosition = Vector3.zero,
      LeftRotation = Quaternion.identity,
      RightPosition = Vector3.zero,
      RightRotation = Quaternion.identity,
    };
  }

  private void WriteDiagnostics() {
    if (_diagnosticCount <= 0)
      return;
    try {
      var directory = Path.Combine(
          MelonUtils.UserDataDirectory, BuildInfo.NAME
      );
      Directory.CreateDirectory(directory);
      var path = Path.Combine(
          directory, "fixed-update-pose-input-" +
          DateTime.Now.ToString("yyyyMMdd-HHmmss-fff",
                                CultureInfo.InvariantCulture) + ".csv"
      );
      var count = Math.Min(_diagnosticCount, _diagnostics.Length);
      using (var writer = new StreamWriter(path, false)) {
        writer.WriteLine(
            "sample,unity_frame,tick_in_render_frame,fixed_time_s," +
            "fixed_delta_s,time_scale,simulation_timestamp_us," +
            "mapped_timestamp_us,timeline_mapping_error_us," +
            "target_timestamp_us,target_spacing_us,target_age_us," +
            "maximum_history_age_us,timeline_reanchored," +
            "timeline_correction_us,head_vertical_velocity_available," +
            "head_vertical_velocity_mps,jump_protection_active," +
            "forward_correction_suppressed," +
            "jump_catch_up_debt_active," +
            "before_sequence," +
            "after_sequence,before_timestamp_us,after_timestamp_us," +
            "before_offset_us,after_offset_us,interpolation_amount,status," +
            "head_applied,left_applied,right_applied," +
            "head_x,head_y,head_z,head_qx,head_qy,head_qz,head_qw," +
            "left_x,left_y,left_z,left_qx,left_qy,left_qz,left_qw," +
            "right_x,right_y,right_z,right_qx,right_qy,right_qz,right_qw"
        );
        for (var i = 0; i < count; i++)
          WriteDiagnostic(writer, i, _diagnostics[i]);
      }
      var summary = BuildDiagnosticSummary(count);
      File.WriteAllText(Path.ChangeExtension(path, ".summary.txt"), summary);
      MelonLogger.Msg(
          "Fixed-update pose diagnostics written: " + path +
          (_diagnosticsDropped == 0 ? "" :
              " (dropped " + _diagnosticsDropped + " ticks)")
      );
      MelonLogger.Msg(summary.Replace(Environment.NewLine, "; ").Trim());
    } catch (Exception exception) {
      MelonLogger.Warning(
          "Could not write fixed-update pose diagnostics: " +
          exception.Message
      );
    }
  }

  private string BuildDiagnosticSummary(int count) {
    var available = 0;
    var stale = 0;
    var allApplied = 0;
    var catchUpFrames = 0;
    var maximumTicksInFrame = 0;
    var monotonicViolations = 0;
    var bracketViolations = 0;
    var maximumSpacingErrorMicroseconds = 0.0;
    var maximumMappingErrorMicroseconds = 0.0;
    var jumpProtectedTicks = 0;
    var suppressedCorrectionTicks = 0;
    var catchUpDebtTicks = 0;
    var frame = -1;
    var ticksInFrame = 0;
    var frequencyPerMicrosecond = Stopwatch.Frequency / 1000000.0;
    for (var i = 0; i < count; i++) {
      var row = _diagnostics[i];
      if (row.UnityFrame != frame) {
        if (ticksInFrame > 1)
          catchUpFrames++;
        maximumTicksInFrame = Math.Max(maximumTicksInFrame, ticksInFrame);
        frame = row.UnityFrame;
        ticksInFrame = 0;
      }
      ticksInFrame++;
      if (row.Status == PoseBracketStatus.Exact ||
          row.Status == PoseBracketStatus.Interpolated ||
          row.Status == PoseBracketStatus.Extrapolated) {
        available++;
        if (row.BeforeTimestamp > row.TargetTimestamp ||
            row.AfterTimestamp < row.TargetTimestamp)
          bracketViolations++;
      }
      if (row.Status == PoseBracketStatus.Stale)
        stale++;
      if (row.HeadApplied)
        allApplied++;
      if (row.JumpProtectionActive)
        jumpProtectedTicks++;
      if (row.ForwardCorrectionSuppressed)
        suppressedCorrectionTicks++;
      if (row.JumpCatchUpDebtActive)
        catchUpDebtTicks++;
      maximumMappingErrorMicroseconds = Math.Max(
          maximumMappingErrorMicroseconds,
          Math.Abs(row.SimulationTimestamp -
                   row.IndependentlyMappedTimestamp) /
          frequencyPerMicrosecond
      );
      if (i == 0 || row.PreviousTargetTimestamp == 0 ||
          row.TimelineReanchored)
        continue;
      var spacing = row.TargetTimestamp - row.PreviousTargetTimestamp;
      if (spacing <= 0)
        monotonicViolations++;
      var expected = row.FixedDeltaTime /
                     Math.Max(0.000001f, row.TimeScale) *
                     Stopwatch.Frequency;
      maximumSpacingErrorMicroseconds = Math.Max(
          maximumSpacingErrorMicroseconds,
          Math.Abs(spacing - expected) / frequencyPerMicrosecond
      );
    }
    if (ticksInFrame > 1)
      catchUpFrames++;
    maximumTicksInFrame = Math.Max(maximumTicksInFrame, ticksInFrame);
    return
        "rows=" + count + Environment.NewLine +
        "available_ticks=" + available + Environment.NewLine +
        "stale_fallback_ticks=" + stale + Environment.NewLine +
        "all_head_and_hands_applied_ticks=" + allApplied +
        Environment.NewLine +
        "catch_up_render_frames=" + catchUpFrames + Environment.NewLine +
        "maximum_ticks_in_one_render_frame=" + maximumTicksInFrame +
        Environment.NewLine +
        "target_timestamp_monotonic_violations=" + monotonicViolations +
        Environment.NewLine +
        "history_bracket_violations=" + bracketViolations +
        Environment.NewLine +
        "jump_protected_ticks=" + jumpProtectedTicks +
        Environment.NewLine +
        "forward_correction_suppressed_ticks=" +
        suppressedCorrectionTicks + Environment.NewLine +
        "jump_catch_up_debt_ticks=" + catchUpDebtTicks +
        Environment.NewLine +
        "maximum_target_spacing_error_us=" +
        maximumSpacingErrorMicroseconds.ToString(
            "0.######", CultureInfo.InvariantCulture
        ) + Environment.NewLine +
        "maximum_render_anchor_mapping_error_us=" +
        maximumMappingErrorMicroseconds.ToString(
            "0.######", CultureInfo.InvariantCulture
        ) + Environment.NewLine;
  }

  private static void WriteDiagnostic(
      TextWriter writer, int index, FixedPoseInputDiagnostic row
  ) {
    var f = Stopwatch.Frequency / 1000000.0;
    writer.Write(index);
    writer.Write(','); writer.Write(row.UnityFrame);
    writer.Write(','); writer.Write(row.TickInFrame);
    Write(writer, row.FixedTime);
    Write(writer, row.FixedDeltaTime);
    Write(writer, row.TimeScale);
    Write(writer, row.SimulationTimestamp / f);
    Write(writer, row.IndependentlyMappedTimestamp / f);
    Write(writer, (row.SimulationTimestamp -
                   row.IndependentlyMappedTimestamp) / f);
    Write(writer, row.TargetTimestamp / f);
    Write(writer, row.PreviousTargetTimestamp == 0 ? 0.0 :
        (row.TargetTimestamp - row.PreviousTargetTimestamp) / f);
    Write(writer, row.TargetAgeTicks / f);
    Write(writer, row.MaximumHistoryAgeTicks / f);
    writer.Write(','); writer.Write(row.TimelineReanchored ? 1 : 0);
    Write(writer, row.TimelineCorrectionTicks / f);
    writer.Write(','); writer.Write(
        row.HeadVerticalVelocityAvailable ? 1 : 0
    );
    Write(writer, row.HeadVerticalVelocity);
    writer.Write(','); writer.Write(row.JumpProtectionActive ? 1 : 0);
    writer.Write(','); writer.Write(
        row.ForwardCorrectionSuppressed ? 1 : 0
    );
    writer.Write(','); writer.Write(row.JumpCatchUpDebtActive ? 1 : 0);
    writer.Write(','); writer.Write(row.BeforeSequence);
    writer.Write(','); writer.Write(row.AfterSequence);
    Write(writer, row.BeforeTimestamp / f);
    Write(writer, row.AfterTimestamp / f);
    Write(writer, (row.BeforeTimestamp - row.TargetTimestamp) / f);
    Write(writer, (row.AfterTimestamp - row.TargetTimestamp) / f);
    Write(writer, row.InterpolationAmount);
    writer.Write(','); writer.Write(row.Status);
    writer.Write(','); writer.Write(row.HeadApplied ? 1 : 0);
    writer.Write(','); writer.Write(row.LeftApplied ? 1 : 0);
    writer.Write(','); writer.Write(row.RightApplied ? 1 : 0);
    WritePose(writer, row.HeadPosition, row.HeadRotation);
    WritePose(writer, row.LeftPosition, row.LeftRotation);
    WritePose(writer, row.RightPosition, row.RightRotation);
    writer.WriteLine();
  }

  private static void WritePose(
      TextWriter writer, Vector3 position, Quaternion rotation
  ) {
    Write(writer, position.x); Write(writer, position.y);
    Write(writer, position.z); Write(writer, rotation.x);
    Write(writer, rotation.y); Write(writer, rotation.z);
    Write(writer, rotation.w);
  }

  private static void Write(TextWriter writer, double value) {
    writer.Write(',');
    writer.Write(value.ToString("0.######", CultureInfo.InvariantCulture));
  }
#endif

  internal static void ApplyCalibratedPose(
      TrackedPoseValue live, TrackedPoseValue historical,
      Vector3 liveOutputPosition, Quaternion liveOutputRotation,
      out Vector3 position, out Quaternion rotation
  ) {
    ToUnityPose(live, out var livePosition, out var liveRotation);
    ToUnityPose(
        historical, out var historicalPosition,
        out var historicalRotation
    );
    var inverseLiveRotation = Quaternion.Inverse(liveRotation);
    var localPositionOffset = inverseLiveRotation *
                              (liveOutputPosition - livePosition);
    var localRotationOffset = inverseLiveRotation * liveOutputRotation;
    position = historicalPosition +
               historicalRotation * localPositionOffset;
    rotation = historicalRotation * localRotationOffset;
  }

  internal static void ApplyCalibratedPosition(
      TrackedPoseValue live, TrackedPoseValue historical,
      Vector3 liveOutputPosition, out Vector3 position
  ) {
    ToUnityPose(live, out var livePosition, out _);
    ToUnityPose(historical, out var historicalPosition, out _);
    position = liveOutputPosition + historicalPosition - livePosition;
  }

  internal static void ToUnityPose(
      TrackedPoseValue pose, out Vector3 position, out Quaternion rotation
  ) {
    position = new Vector3(pose.X, pose.Y, -pose.Z);
    rotation = new Quaternion(-pose.Qx, -pose.Qy, pose.Qz, pose.Qw);
  }

  private static HmdMatrix34_t ToOpenVrMatrix(TrackedPoseValue pose) {
    var matrix = PoseHistoryMath.ToMatrix(pose);
    return new HmdMatrix34_t {
      m0 = matrix.M0, m1 = matrix.M1, m2 = matrix.M2, m3 = matrix.M3,
      m4 = matrix.M4, m5 = matrix.M5, m6 = matrix.M6, m7 = matrix.M7,
      m8 = matrix.M8, m9 = matrix.M9, m10 = matrix.M10,
      m11 = matrix.M11,
    };
  }

  private static TrackedPoseValue ReadPose(
      Il2CppStructArray<TrackedDevicePose_t> poses, int deviceIndex
  ) {
    if (poses == null || deviceIndex < 0 || deviceIndex >= poses.Length)
      return new TrackedPoseValue();
    var pose = poses[deviceIndex];
    var matrix = pose.mDeviceToAbsoluteTracking;
    return PoseHistoryMath.FromMatrix(
        new PoseMatrix3x4 {
          M0 = matrix.m0, M1 = matrix.m1, M2 = matrix.m2, M3 = matrix.m3,
          M4 = matrix.m4, M5 = matrix.m5, M6 = matrix.m6, M7 = matrix.m7,
          M8 = matrix.m8, M9 = matrix.m9, M10 = matrix.m10,
          M11 = matrix.m11,
        },
        pose.bPoseIsValid, pose.bDeviceIsConnected,
        (int)pose.eTrackingResult
    );
  }

  private static int ToDeviceIndex(uint index) =>
      index == OpenVR.k_unTrackedDeviceIndexInvalid ? -1 : (int)index;

#if DEBUG
  [DllImport("kernel32.dll")]
  private static extern uint GetCurrentThreadId();
#endif

  [HarmonyPatch(typeof(SteamControllerRig), nameof(SteamControllerRig.OnFixedUpdate))]
  private static class SteamControllerRig_OnFixedUpdate_Patch {
    [HarmonyPrefix]
    private static bool Prefix(
        SteamControllerRig __instance, out bool __state
    ) {
      __state = false;
      var instance = _instance;
      if (instance == null)
        return true;
      instance.PrepareFixedTick(__instance);
      instance.DeliverQueuedJump(__instance);
#if DEBUG
      if (instance._smoothnessTest != null) {
        __state = true;
        instance.ApplyHead(__instance);
        instance.FinishFixedTick();
        return false;
      }
#endif
      return true;
    }

    [HarmonyPostfix]
    private static void Postfix(bool __state) {
      if (!__state)
        _instance?.FinishFixedTick();
    }
  }

  [HarmonyPatch(typeof(SteamControllerRig), nameof(SteamControllerRig.OnEarlyUpdate))]
  private static class SteamControllerRig_OnEarlyUpdate_Patch {
    [HarmonyPostfix]
    private static void Postfix(SteamControllerRig __instance) =>
        _instance?.ObserveJumpButton(__instance);
  }

  [HarmonyPatch(typeof(ControllerRig), nameof(ControllerRig.JumpCharge))]
  private static class ControllerRig_JumpCharge_Patch {
    [HarmonyPrefix]
    private static void Prefix(
        ControllerRig __instance, ref bool chargeInput
    ) => _instance?.OverrideJumpCharge(__instance, ref chargeInput);
  }

  [HarmonyPatch(typeof(ControllerRig), nameof(ControllerRig.Jump))]
  private static class ControllerRig_Jump_Patch {
    [HarmonyPrefix]
    private static bool Prefix(ControllerRig __instance) =>
        _instance == null || _instance.InterceptJump(__instance);
  }

  [HarmonyPatch(typeof(RigController), nameof(RigController.OnVrFixedUpdate))]
  private static class Controller_OnVrFixedUpdate_Patch {
    [HarmonyPrefix]
    private static void Prefix(
        RigController __instance, out ControllerPatchState __state
    ) {
      if (_instance == null) {
        __state = new ControllerPatchState();
        return;
      }
      _instance.BeforeController(__instance, out __state);
    }

    [HarmonyPostfix]
    private static void Postfix(
        RigController __instance, ControllerPatchState __state
    ) => _instance?.AfterController(__instance, __state);
  }
}
