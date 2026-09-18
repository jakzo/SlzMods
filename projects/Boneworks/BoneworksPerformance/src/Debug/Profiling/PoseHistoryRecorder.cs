#if DEBUG
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnhollowerBaseLib;
using UnityEngine;
using Valve.VR;

namespace Sst.BoneworksPerformance;

internal sealed class PoseHistoryRecorder {
  private static int _samplerNativeThreadId;
  private readonly CVRSystem _system;
  private readonly int _sampleRate;
  private readonly long _startTimestamp;
  private readonly int _interpolationDelayTicks;
  private readonly PoseHistoryBuffer _backgroundSamples;
  private readonly FixedPoseObservation[] _fixedSamples;
  private readonly ResampledFixedPoseObservation[] _resampledFixedSamples;
  private readonly Il2CppStructArray<TrackedDevicePose_t> _fixedPoses;
  private readonly FixedTimeMapper _fixedTimeMapper = new FixedTimeMapper();
  private readonly AutoResetEvent _stop = new AutoResetEvent(false);
  private readonly Thread _thread;
  private readonly int _leftDeviceIndex;
  private readonly int _rightDeviceIndex;
  private int _fixedSampleCount;
  private int _fixedSamplesDropped;
  private long _queryTicks;
  private long _maximumQueryTicks;
  private int _queryCount;
  private int _queryErrors;
  private int _scheduleOverruns;
  private string _status;

  private PoseHistoryRecorder(
      float maximumSeconds, int requestedSampleRate,
      int interpolationDelayTicks, long startTimestamp
  ) {
    _sampleRate = Math.Max(30, Math.Min(1000, requestedSampleRate));
    _startTimestamp = startTimestamp;
    _interpolationDelayTicks = Math.Max(
        0, Math.Min(8, interpolationDelayTicks)
    );
    var backgroundCapacity = Math.Max(
        1, (int)Math.Ceiling(maximumSeconds * _sampleRate) + _sampleRate
    );
    var fixedCapacity = Math.Max(
        1, (int)Math.Ceiling(maximumSeconds * 240f) + 240
    );
    _backgroundSamples = new PoseHistoryBuffer(backgroundCapacity);
    _fixedSamples = new FixedPoseObservation[fixedCapacity];
    _resampledFixedSamples =
        new ResampledFixedPoseObservation[fixedCapacity];
    _fixedPoses = new Il2CppStructArray<TrackedDevicePose_t>(
        OpenVR.k_unMaxTrackedDeviceCount
    );
    _fixedTimeMapper.ObserveFrame(0, Time.time, Time.timeScale);

    try {
      _system = OpenVR.System;
      if (_system == null) {
        _status = "OpenVR system unavailable";
        return;
      }
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
    } catch (Exception exception) {
      _status = "OpenVR initialization failed: " + exception.Message;
      return;
    }

    _status = "running";
    _thread = new Thread(ThreadMain) {
      IsBackground = true,
      Name = "Boneworks pose history",
      Priority = System.Threading.ThreadPriority.BelowNormal,
    };
    _thread.Start();
  }

  public static PoseHistoryRecorder Start(
      float maximumSeconds, int requestedSampleRate,
      int interpolationDelayTicks, long startTimestamp
  ) => new PoseHistoryRecorder(
      maximumSeconds, requestedSampleRate, interpolationDelayTicks,
      startTimestamp
  );

  public static bool IsInternalThread(int threadId) =>
      threadId != 0 &&
      threadId == Volatile.Read(ref _samplerNativeThreadId);

  public void RecordFixedTick() {
    if (_system == null)
      return;
    var index = _fixedSampleCount;
    if (index >= _fixedSamples.Length) {
      _fixedSamplesDropped++;
      return;
    }
    if (!TryReadPoses(_fixedPoses, out var sample))
      return;
    _fixedSamples[index] = new FixedPoseObservation {
      UnityFrame = Time.frameCount,
      FixedTime = Time.fixedTime,
      RealtimeSinceStartup = Time.realtimeSinceStartup,
      Sample = sample,
    };
    _resampledFixedSamples[index] = ResampleFixedTick(sample);
    _fixedSampleCount = index + 1;
  }

  public void ObserveFrame(long timestamp) {
    _fixedTimeMapper.ObserveFrame(timestamp, Time.time, Time.timeScale);
  }

  public PoseHistoryResult Stop() {
    if (_thread != null) {
      _stop.Set();
      if (!_thread.Join(2000))
        _status = "sampler thread did not stop within two seconds";
      else if (_status == "running")
        _status = "complete";
    }
    var fixedSamples = new FixedPoseObservation[_fixedSampleCount];
    Array.Copy(_fixedSamples, fixedSamples, fixedSamples.Length);
    var resampledFixedSamples =
        new ResampledFixedPoseObservation[_fixedSampleCount];
    Array.Copy(
        _resampledFixedSamples, resampledFixedSamples,
        resampledFixedSamples.Length
    );
    var background = _backgroundSamples.CopyChronological();
    var elapsedTicks = background.Length < 2
        ? 0
        : background[background.Length - 1].Timestamp -
          background[0].Timestamp;
    var actualRate = elapsedTicks <= 0
        ? 0.0
        : (background.Length - 1) * (double)Stopwatch.Frequency /
          elapsedTicks;
    return new PoseHistoryResult(
        background, fixedSamples, resampledFixedSamples,
        _sampleRate, actualRate, _interpolationDelayTicks,
        _leftDeviceIndex, _rightDeviceIndex,
        _queryCount, _queryTicks, _maximumQueryTicks, _queryErrors,
        _scheduleOverruns, _fixedSamplesDropped, _status
    );
  }

  public static PoseHistoryResult EmptyResult(string status) =>
      new PoseHistoryResult(
          new PoseHistorySample[0], new FixedPoseObservation[0],
          new ResampledFixedPoseObservation[0], 0, 0.0,
          0, -1, -1, 0, 0, 0, 0, 0, 0, status
      );

  private ResampledFixedPoseObservation ResampleFixedTick(
      PoseHistorySample querySample
  ) {
    var observation = new ResampledFixedPoseObservation {
      UnityFrame = Time.frameCount,
      FixedTime = Time.fixedTime,
      QueryTimestamp = querySample.Timestamp,
      Status = PoseBracketStatus.Unavailable,
      BeforeSequence = -1,
      AfterSequence = -1,
    };
    if (!_fixedTimeMapper.TryMap(
            Time.fixedTime, Stopwatch.Frequency,
            out observation.SimulationTimestamp,
            out observation.AnchorTimestamp,
            out observation.AnchorGameTime,
            out observation.AnchorTimeScale
        ))
      return observation;
    observation.TargetTimestamp = observation.SimulationTimestamp -
        (long)Math.Round(
            _interpolationDelayTicks * Time.fixedDeltaTime /
            Math.Max(0.000001f, observation.AnchorTimeScale) *
            Stopwatch.Frequency
        );
    if (!_backgroundSamples.TryGetBracket(
            observation.TargetTimestamp, out var before, out var after,
            out observation.Status
        ))
      return observation;

    observation.BeforeSequence = before.Sequence;
    observation.AfterSequence = after.Sequence;
    observation.BeforeTimestamp = before.Timestamp;
    observation.AfterTimestamp = after.Timestamp;
    var span = after.Timestamp - before.Timestamp;
    observation.InterpolationAmount = span <= 0
        ? 0f
        : (float)Math.Max(0.0, Math.Min(
            1.0, (observation.TargetTimestamp - before.Timestamp) /
                 (double)span
        ));
    observation.Head = PoseHistoryMath.Interpolate(
        before.Head, after.Head, observation.InterpolationAmount
    );
    observation.Left = PoseHistoryMath.Interpolate(
        before.Left, after.Left, observation.InterpolationAmount
    );
    observation.Right = PoseHistoryMath.Interpolate(
        before.Right, after.Right, observation.InterpolationAmount
    );
    return observation;
  }

  private void ThreadMain() {
    Volatile.Write(
        ref _samplerNativeThreadId,
        CpuSampler.GetCurrentNativeThreadId()
    );
    try {
      // Valve.VR is an IL2CPP-generated game assembly. A foreign managed
      // thread must be registered with IL2CPP's garbage collector before it
      // enters that assembly.
      using (Il2CppThreadRegistration.Attach()) {
        var poses = new Il2CppStructArray<TrackedDevicePose_t>(
            OpenVR.k_unMaxTrackedDeviceCount
        );
        var interval = Math.Max(1L, Stopwatch.Frequency / _sampleRate);
        var next = Stopwatch.GetTimestamp();
        while (true) {
          var now = Stopwatch.GetTimestamp();
          var remaining = next - now;
          if (remaining > 0) {
            var waitMilliseconds = (int)(
                remaining * 1000L / Stopwatch.Frequency
            );
            if (waitMilliseconds > 0 && _stop.WaitOne(waitMilliseconds))
              break;
            if (waitMilliseconds == 0) {
              Thread.SpinWait(32);
              if (_stop.WaitOne(0))
                break;
            }
            continue;
          }
          if (now - next > interval)
            Interlocked.Increment(ref _scheduleOverruns);
          if (TryReadPoses(poses, out var sample))
            _backgroundSamples.Add(sample);
          next += interval;
          if (now - next > interval * 4)
            next = now + interval;
        }
      }
    } catch (Exception exception) {
      _status = "sampler thread failed: " + exception.Message;
    } finally {
      Volatile.Write(ref _samplerNativeThreadId, 0);
    }
  }

  private bool TryReadPoses(
      Il2CppStructArray<TrackedDevicePose_t> poses,
      out PoseHistorySample sample
  ) {
    var started = Stopwatch.GetTimestamp();
    try {
      _system.GetDeviceToAbsoluteTrackingPose(
          ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, poses
      );
      var ended = Stopwatch.GetTimestamp();
      var queryTicks = ended - started;
      Interlocked.Add(ref _queryTicks, queryTicks);
      Interlocked.Increment(ref _queryCount);
      UpdateMaximum(ref _maximumQueryTicks, queryTicks);
      sample = new PoseHistorySample {
        Timestamp = started + queryTicks / 2 - _startTimestamp,
        QueryTicks = queryTicks,
        HeadDeviceIndex = 0,
        LeftDeviceIndex = _leftDeviceIndex,
        RightDeviceIndex = _rightDeviceIndex,
        Head = ReadPose(poses, 0),
        Left = ReadPose(poses, _leftDeviceIndex),
        Right = ReadPose(poses, _rightDeviceIndex),
      };
      return true;
    } catch {
      Interlocked.Increment(ref _queryErrors);
      sample = new PoseHistorySample();
      return false;
    }
  }

  private static TrackedPoseValue ReadPose(
      Il2CppStructArray<TrackedDevicePose_t> poses, int deviceIndex
  ) {
    if (deviceIndex < 0 || deviceIndex >= poses.Length)
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

  private static void UpdateMaximum(ref long target, long candidate) {
    var current = Interlocked.Read(ref target);
    while (candidate > current) {
      var previous = Interlocked.CompareExchange(
          ref target, candidate, current
      );
      if (previous == current)
        return;
      current = previous;
    }
  }
}

internal sealed class PoseHistoryResult {
  public readonly PoseHistorySample[] BackgroundSamples;
  public readonly FixedPoseObservation[] FixedSamples;
  public readonly ResampledFixedPoseObservation[] ResampledFixedSamples;
  public readonly int RequestedSampleRate;
  public readonly double ActualSampleRate;
  public readonly int InterpolationDelayTicks;
  public readonly int LeftDeviceIndex;
  public readonly int RightDeviceIndex;
  public readonly int QueryCount;
  public readonly long QueryTicks;
  public readonly long MaximumQueryTicks;
  public readonly int QueryErrors;
  public readonly int ScheduleOverruns;
  public readonly int FixedSamplesDropped;
  public readonly string Status;

  public PoseHistoryResult(
      PoseHistorySample[] backgroundSamples,
      FixedPoseObservation[] fixedSamples,
      ResampledFixedPoseObservation[] resampledFixedSamples,
      int requestedSampleRate, double actualSampleRate,
      int interpolationDelayTicks,
      int leftDeviceIndex, int rightDeviceIndex,
      int queryCount, long queryTicks, long maximumQueryTicks,
      int queryErrors, int scheduleOverruns, int fixedSamplesDropped,
      string status
  ) {
    BackgroundSamples = backgroundSamples;
    FixedSamples = fixedSamples;
    ResampledFixedSamples = resampledFixedSamples;
    RequestedSampleRate = requestedSampleRate;
    ActualSampleRate = actualSampleRate;
    InterpolationDelayTicks = interpolationDelayTicks;
    LeftDeviceIndex = leftDeviceIndex;
    RightDeviceIndex = rightDeviceIndex;
    QueryCount = queryCount;
    QueryTicks = queryTicks;
    MaximumQueryTicks = maximumQueryTicks;
    QueryErrors = queryErrors;
    ScheduleOverruns = scheduleOverruns;
    FixedSamplesDropped = fixedSamplesDropped;
    Status = status;
  }
}
#endif
