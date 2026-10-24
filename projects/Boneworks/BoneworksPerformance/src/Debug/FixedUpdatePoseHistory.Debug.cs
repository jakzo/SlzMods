using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using MelonLoader;
using StressLevelZero.Rig;
using UnhollowerBaseLib;
using UnityEngine;
using Valve.VR;

namespace Sst.BoneworksPerformance;

internal struct FixedPoseInputDiagnostic
{
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

internal sealed partial class FixedUpdatePoseHistory
{
  private static void DebugReadRotation(ref TrackedPoseValue value, HmdMatrix34_t matrix,
      bool connected, int trackingResult)
  {
    value = PoseHistoryMath.FromMatrix(new PoseMatrix3x4
    {
      M0 = matrix.m0, M1 = matrix.m1, M2 = matrix.m2, M3 = matrix.m3,
      M4 = matrix.m4, M5 = matrix.m5, M6 = matrix.m6, M7 = matrix.m7,
      M8 = matrix.m8, M9 = matrix.m9, M10 = matrix.m10, M11 = matrix.m11,
    }, value.IsValid, connected, trackingResult);
  }

  internal static void ToUnityPose(TrackedPoseValue pose, out Vector3 position, out Quaternion rotation)
  {
    position = new Vector3(pose.X, pose.Y, -pose.Z);
    rotation = new Quaternion(-pose.Qx, -pose.Qy, pose.Qz, pose.Qw);
  }

  private static void DebugButtonHistoryLost() =>
      MelonLogger.Warning("Input history overflowed; discarded queued button edges to avoid phantom jumps.");

  private void DebugObserveButtons(SampledButtons buttons, bool jumpChanged, bool slowChanged)
  {
    if (jumpChanged)
      _superJumpDiagnostics?.ObserveCapturedButton(buttons.Timestamp, buttons.Generation, true, buttons.Jump);
    if (slowChanged)
      _superJumpDiagnostics?.ObserveCapturedButton(buttons.Timestamp, buttons.Generation, false, buttons.SlowMotion);
  }

  private void DebugButtonConfiguration(ControllerRig rig) => MelonLogger.Msg(
      "Background buttons: generation=" + _buttonSampler.Generation +
      ", jumpHand=" + (rig.isRightHanded ? "right" : "left") +
      ", jumpController=" + JumpButtonTiming.Controller(rig)?.controllerType +
      ", slowMotionController=" + SlowMotionButtonTiming.Controller(rig)?.controllerType +
      ". Using shipped physical bindings, not custom SteamVR action remaps.");

  private void DebugAllowSyntheticUpdates(ref bool allowed)
  {
    if (_smoothnessTest != null) allowed = true;
  }

  private const int DiagnosticCapacity = 16384;
  private static int _samplerNativeThreadId;
  private readonly FixedPoseInputDiagnostic[] _diagnostics =
      new FixedPoseInputDiagnostic[DiagnosticCapacity];
  private FixedPoseSmoothnessTest _smoothnessTest;
  private SuperJumpDiagnostics _superJumpDiagnostics;
  private int _diagnosticCount;
  private int _diagnosticsDropped;
  private bool _recordDiagnostics;
  private int _leftDeviceIndex = -1;
  private int _rightDeviceIndex = -1;
  private int _queryErrors, _scheduleOverruns, _appliedTicks, _unavailableTicks;
  private long _lastTargetTimestamp, _lastSummaryTimestamp, _deviceRefreshAt;
  private int _tickInFrame;
  private bool _skipSyntheticPostfix;

  [StructLayout(LayoutKind.Auto)]
  private partial struct CurrentFixedPose
  {
    public int UnityFrame;
    public int TickInFrame;
    public float FixedTime;
    public long SimulationTimestamp;
    public long IndependentlyMappedTimestamp;
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
    public bool ForwardCorrectionSuppressed;
    public bool JumpCatchUpDebtActive;
    public float Amount;
    public bool JumpReleaseDelivered;
    public long JumpReleaseTimestamp;
    public int SlowMotionPressesDelivered;
    public int SlowMotionReleasesDelivered;
    public bool SlowMotionToggleDelivered;
    public Vector3 HeadPosition;
    public Quaternion HeadRotation;
  }

  public static bool IsInternalThread(int threadId) =>
      threadId != 0 &&
      threadId == Volatile.Read(ref _samplerNativeThreadId);

  public void SetSmoothnessTest(FixedPoseSmoothnessTest test)
  {
    if (_thread != null)
      throw new InvalidOperationException(
          "The smoothness test must be configured before pose sampling starts."
      );
    _smoothnessTest = test;
  }

  public void SetSuperJumpDiagnostics(SuperJumpDiagnostics diagnostics)
  {
    _superJumpDiagnostics = diagnostics;
  }

  public void BeginDiagnosticRun()
  {
    _diagnosticCount = 0;
    _diagnosticsDropped = 0;
    _lastTargetTimestamp = 0;
    _recordDiagnostics = true;
  }

  public void EndDiagnosticRun()
  {
    _recordDiagnostics = false;
    WriteDiagnostics();
    _diagnosticCount = 0;
    _diagnosticsDropped = 0;
  }

  private void RefreshDeviceIndices()
  {
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

  private static int ToDeviceIndex(uint index) =>
      index == OpenVR.k_unTrackedDeviceIndexInvalid ? -1 : (int)index;

  private void DebugObserveFrame(long now)
  {
    if (_running && now - _lastSummaryTimestamp >=
        Stopwatch.Frequency * 5L)
    {
      _lastSummaryTimestamp = now;
      MelonLogger.Msg(
          "Fixed-pose input: applied=" + _appliedTicks +
          ", unavailable=" + _unavailableTicks +
          ", historySamples=" + _history.TotalWritten +
          ", queryErrors=" + _queryErrors +
          ", samplerOverruns=" + _scheduleOverruns + "."
      );
    }

  }

  private void DebugResetScene() { _tickInFrame = 0; _lastTargetTimestamp = 0; }

  private void DebugPrepareSampler(ref bool synthetic)
  {
    synthetic = _smoothnessTest != null;
    _lastSummaryTimestamp = Stopwatch.GetTimestamp();
  }

  private bool DebugSampleLoop()
  {
    Volatile.Write(
        ref _samplerNativeThreadId,
        unchecked((int)GetCurrentThreadId())
    );
    if (_smoothnessTest != null)
    {
      try
      {
        SampleSyntheticLoop();
      } finally
      {
        _running = false;
        Volatile.Write(ref _samplerNativeThreadId, 0);
      }
      return true;
    }

    return false;
  }

  private void DebugSamplerStopped() => Volatile.Write(ref _samplerNativeThreadId, 0);
  private void DebugQueryFailed() => Interlocked.Increment(ref _queryErrors);
  private void DebugUnavailable() => _unavailableTicks++;
  private void DebugUnavailablePose(long now, PoseHistorySample before,
      PoseHistorySample after, PoseBracketStatus status)
  {
    DebugSetBracket(before, after, status);
    DebugUnavailable();
    ApplySyntheticFallback(now);
  }

  private void DebugSamplerTick(long now, long lateness, long interval)
  {
    if (lateness > interval) Interlocked.Increment(ref _scheduleOverruns);
    if (now < _deviceRefreshAt) return;
    RefreshDeviceIndices();
    _deviceRefreshAt = now + Stopwatch.Frequency;
  }

  private void DebugPopulateSample(ref PoseHistorySample sample,
      Il2CppStructArray<TrackedDevicePose_t> poses, long queryTicks)
  {
    sample.QueryTicks = queryTicks;
    sample.HeadDeviceIndex = 0;
    sample.LeftDeviceIndex = _leftDeviceIndex;
    sample.RightDeviceIndex = _rightDeviceIndex;
    sample.Left = ReadPose(poses, _leftDeviceIndex);
    sample.Right = ReadPose(poses, _rightDeviceIndex);
  }

  private void DebugBeginFixedTick(int frame, bool first, float fixedTime)
  {
    _tickInFrame = first ? 0 : _tickInFrame + 1;
    _current.UnityFrame = frame;
    _current.TickInFrame = _tickInFrame;
    _current.FixedTime = fixedTime;
    _current.PreviousTargetTimestamp = _lastTargetTimestamp;
    _current.Status = PoseBracketStatus.Unavailable;
  }

  private void DebugClockMapped(long simulation, long mapped, bool reanchored,
      bool velocityAvailable, float velocity, long age, long maxAge)
  {
    _current.SimulationTimestamp = simulation;
    _current.IndependentlyMappedTimestamp = mapped;
    _current.TimelineReanchored = reanchored;
    _current.TimelineCorrectionTicks = _tickTimeline.LastCorrectionTicks;
    _current.HeadVerticalVelocityAvailable = velocityAvailable;
    _current.HeadVerticalVelocity = velocity;
    _current.ForwardCorrectionSuppressed = _tickTimeline.LastForwardCorrectionSuppressed;
    _current.JumpCatchUpDebtActive = _jumpCatchUpDebt;
    _current.TargetAgeTicks = age;
    _current.MaximumHistoryAgeTicks = maxAge;
    _lastTargetTimestamp = _current.TargetTimestamp;
  }

  private void DebugSetBracket(PoseHistorySample before, PoseHistorySample after,
      PoseBracketStatus status, float amount = 0)
  {
    _current.Before = before; _current.After = after;
    _current.Status = status; _current.Amount = amount;
  }

  private void DebugSlowMotionToggled() => _current.SlowMotionToggleDelivered = true;
  private void DebugSlowMotionEdge(SlowMotionReplayCommand command)
  {
    if (command == SlowMotionReplayCommand.DecreaseTimeScale) _current.SlowMotionPressesDelivered++;
    else _current.SlowMotionReleasesDelivered++;
  }
  private void DebugJumpReleased(long timestamp)
  {
    _current.JumpReleaseDelivered = true; _current.JumpReleaseTimestamp = timestamp;
  }
  private void DebugHeadApplied(Vector3 position, Quaternion rotation)
  {
    _current.HeadPosition = position; _current.HeadRotation = rotation;
  }

  private bool DebugApplySyntheticHead(Transform target)
  {
    if (_smoothnessTest == null || !target || !_current.Head.IsValid) return false;
    ToUnityPose(_current.Head, out var position, out _);
    target.localPosition = position;
    _current.HeadApplied = true;
    DebugHeadApplied(position, target.localRotation);
    return true;
  }

  private void DebugFinishFixedTick()
  {
    if (_current.HeadApplied) _appliedTicks++;
    if (_superJumpDiagnostics != null)
    {
      var latestHead = new TrackedPoseValue();
      if (_history.TryGetLatestPair(out _, out var latest))
        latestHead = latest.Head;
      _superJumpDiagnostics.RecordFixedTick(
          _currentSteamRig, _current.UnityFrame, _current.TickInFrame,
          _current.FixedTime, _current.SimulationTimestamp,
          _current.TargetTimestamp, _current.Available, _current.Head,
          _current.HeadPosition, latestHead, _current.TimeScale,
          _current.FixedJumpButtonHeld, _current.JumpReleaseDelivered,
          _current.JumpReleaseTimestamp,
          _current.SlowMotionPressesDelivered,
          _current.SlowMotionReleasesDelivered,
          _current.SlowMotionToggleDelivered,
          _slowMotionReplay.PendingToggleTimestamp
      );
    }
    if (_smoothnessTest != null && _smoothnessTest.IsActive)
    {
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
    if (_recordDiagnostics)
      RecordDiagnostic();

  }

  private bool DebugRunSyntheticFixedTick()
  {
    if (_smoothnessTest == null) return false;
    _skipSyntheticPostfix = true;
    ApplyHead(_currentSteamRig);
    FinishFixedTick();
    return true;
  }
  private bool DebugSkipFixedPostfix()
  {
    var skip = _skipSyntheticPostfix; _skipSyntheticPostfix = false; return skip;
  }

  private void SampleSyntheticLoop()
  {
    using var wait = new SamplerWait(_stop);
    var interval = Math.Max(1L, Stopwatch.Frequency / _sampleRate);
    var next = Stopwatch.GetTimestamp();
    while (_running)
    {
      var now = Stopwatch.GetTimestamp();
      var remaining = next - now;
      if (remaining > 0)
      {
        if (!wait.Until(next))
          break;
        continue;
      }
      if (now - next > interval)
        Interlocked.Increment(ref _scheduleOverruns);
      var pose = _smoothnessTest.PoseAt(now);
      _history.Add(new PoseHistorySample
      {
        Timestamp = now,
        HeadDeviceIndex = 0,
        Head = pose,
      });
      next = SamplerSchedule.Next(next, Stopwatch.GetTimestamp(), interval);
    }
  }

  private void ApplySyntheticFallback(long timestamp)
  {
    if (_smoothnessTest == null)
      return;
    _current.Available = true;
    _current.Head = _smoothnessTest.PoseAt(timestamp);
  }

  private void RecordDiagnostic()
  {
    var index = _diagnosticCount++;
    if (index >= _diagnostics.Length)
    {
      _diagnosticsDropped++;
      return;
    }
    _diagnostics[index] = new FixedPoseInputDiagnostic
    {
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

  private void WriteDiagnostics()
  {
    if (_diagnosticCount <= 0)
      return;
    try
    {
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
      using (var writer = new StreamWriter(path, false))
      {
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
    } catch (Exception exception)
    {
      MelonLogger.Warning(
          "Could not write fixed-update pose diagnostics: " +
          exception.Message
      );
    }
  }

  private string BuildDiagnosticSummary(int count)
  {
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
    for (var i = 0; i < count; i++)
    {
      var row = _diagnostics[i];
      if (row.UnityFrame != frame)
      {
        if (ticksInFrame > 1)
          catchUpFrames++;
        maximumTicksInFrame = Math.Max(maximumTicksInFrame, ticksInFrame);
        frame = row.UnityFrame;
        ticksInFrame = 0;
      }
      ticksInFrame++;
      if (row.Status == PoseBracketStatus.Exact ||
          row.Status == PoseBracketStatus.Interpolated ||
          row.Status == PoseBracketStatus.Extrapolated)
      {
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
  )
  {
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
  )
  {
    Write(writer, position.x); Write(writer, position.y);
    Write(writer, position.z); Write(writer, rotation.x);
    Write(writer, rotation.y); Write(writer, rotation.z);
    Write(writer, rotation.w);
  }

  private static void Write(TextWriter writer, double value)
  {
    writer.Write(',');
    writer.Write(value.ToString("0.######", CultureInfo.InvariantCulture));
  }

  [DllImport("kernel32.dll")]
  private static extern uint GetCurrentThreadId();

}
