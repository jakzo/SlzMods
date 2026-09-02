using System;
using System.Collections.Generic;
using System.Threading;

namespace Sst.BoneworksPerformance;

internal struct PoseMatrix3x4 {
  public float M0, M1, M2, M3;
  public float M4, M5, M6, M7;
  public float M8, M9, M10, M11;
}

internal struct TrackedPoseValue {
  public bool IsValid;
  public bool IsConnected;
  public int TrackingResult;
  public float X, Y, Z;
  public float Qx, Qy, Qz, Qw;
}

internal struct PoseHistorySample {
  public long Sequence;
  public long Timestamp;
  public long QueryTicks;
  public int HeadDeviceIndex;
  public int LeftDeviceIndex;
  public int RightDeviceIndex;
  public TrackedPoseValue Head;
  public TrackedPoseValue Left;
  public TrackedPoseValue Right;
}

#if DEBUG
internal struct FixedPoseObservation {
  public int UnityFrame;
  public float FixedTime;
  public float RealtimeSinceStartup;
  public PoseHistorySample Sample;
}
#endif

internal enum PoseBracketStatus {
  Unavailable,
  Exact,
  Interpolated,
  Extrapolated,
  BeforeHistory,
  AfterHistory,
  Stale,
}

internal struct TimedButtonEdge {
  public long Timestamp;
  public bool Pressed;

  public TimedButtonEdge(long timestamp, bool pressed) {
    Timestamp = timestamp;
    Pressed = pressed;
  }
}

internal static class TimedButtonMath {
  public static int FirstTickAtOrAfter(
      long edgeTimestamp, long[] fixedTickTimestamps
  ) {
    if (fixedTickTimestamps == null || fixedTickTimestamps.Length == 0)
      return -1;
    var low = 0;
    var high = fixedTickTimestamps.Length - 1;
    var answer = -1;
    while (low <= high) {
      var middle = low + (high - low) / 2;
      if (fixedTickTimestamps[middle] >= edgeTimestamp) {
        answer = middle;
        high = middle - 1;
      } else {
        low = middle + 1;
      }
    }
    return answer;
  }

  public static bool StateAt(
      bool initialState, TimedButtonEdge[] edges, long timestamp
  ) {
    var state = initialState;
    if (edges == null)
      return state;
    for (var i = 0; i < edges.Length; i++) {
      if (edges[i].Timestamp > timestamp)
        break;
      state = edges[i].Pressed;
    }
    return state;
  }
}

#if DEBUG
internal struct ResampledFixedPoseObservation {
  public int UnityFrame;
  public float FixedTime;
  public long TargetTimestamp;
  public long SimulationTimestamp;
  public long QueryTimestamp;
  public long AnchorTimestamp;
  public float AnchorGameTime;
  public float AnchorTimeScale;
  public long BeforeSequence;
  public long AfterSequence;
  public long BeforeTimestamp;
  public long AfterTimestamp;
  public float InterpolationAmount;
  public PoseBracketStatus Status;
  public TrackedPoseValue Head;
  public TrackedPoseValue Left;
  public TrackedPoseValue Right;
}
#endif

internal sealed class FixedTimeMapper {
  private long _anchorTimestamp;
  private float _anchorGameTime;
  private float _anchorTimeScale;
  private bool _hasAnchor;

  public void ObserveFrame(
      long timestamp, float gameTime, float timeScale
  ) {
    if (timeScale <= 0.000001f)
      return;
    _anchorTimestamp = timestamp;
    _anchorGameTime = gameTime;
    _anchorTimeScale = timeScale;
    _hasAnchor = true;
  }

  public bool TryMap(
      float fixedTime, long stopwatchFrequency, out long targetTimestamp,
      out long anchorTimestamp, out float anchorGameTime,
      out float anchorTimeScale
  ) {
    anchorTimestamp = _anchorTimestamp;
    anchorGameTime = _anchorGameTime;
    anchorTimeScale = _anchorTimeScale;
    if (!_hasAnchor || stopwatchFrequency <= 0) {
      targetTimestamp = 0;
      return false;
    }
    targetTimestamp = _anchorTimestamp + (long)Math.Round(
        (fixedTime - _anchorGameTime) / _anchorTimeScale *
        stopwatchFrequency
    );
    return true;
  }
}

internal sealed class FixedTickTimeline {
  private readonly FixedTimeMapper _mapper;
  private readonly double _catchUpSpeed;
  private bool _hasTick;
  private float _lastFixedTime;
  private double _timestamp;

  public long LastCorrectionTicks { get; private set; }
  public bool LastForwardCorrectionSuppressed { get; private set; }

  public FixedTickTimeline(
      FixedTimeMapper mapper, double catchUpSpeed = 2.0
  ) {
    _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    _catchUpSpeed = Math.Max(1.0, Math.Min(8.0, catchUpSpeed));
  }

  public void Reset() {
    _hasTick = false;
    _lastFixedTime = 0f;
    _timestamp = 0.0;
    LastCorrectionTicks = 0;
    LastForwardCorrectionSuppressed = false;
  }

  public bool TryAdvance(
      float fixedTime, float fixedDeltaTime, float timeScale,
      long stopwatchFrequency, bool beginRenderFrame,
      bool suppressForwardCorrection, out long timestamp,
      out long independentlyMappedTimestamp, out bool reanchored
  ) {
    timestamp = 0;
    independentlyMappedTimestamp = 0;
    reanchored = false;
    LastCorrectionTicks = 0;
    LastForwardCorrectionSuppressed = false;
    if (fixedDeltaTime <= 0f || timeScale <= 0.000001f ||
        stopwatchFrequency <= 0 || !_mapper.TryMap(
            fixedTime, stopwatchFrequency,
            out independentlyMappedTimestamp, out _, out _, out _
        ))
      return false;

    var fixedAdvance = fixedTime - _lastFixedTime;
    var nextTimestamp = _timestamp +
                        fixedDeltaTime / timeScale * stopwatchFrequency;
    var renderFrameDrift = Math.Abs(
        independentlyMappedTimestamp - nextTimestamp
    );
    var isNextTick = _hasTick &&
                     fixedAdvance > fixedDeltaTime * 0.5f &&
                     fixedAdvance < fixedDeltaTime * 1.5f &&
                     (!beginRenderFrame ||
                      renderFrameDrift <= stopwatchFrequency);
    if (!isNextTick) {
      _timestamp = independentlyMappedTimestamp;
      reanchored = _hasTick;
    } else {
      _timestamp = nextTimestamp;
      if (beginRenderFrame) {
        var phaseError = independentlyMappedTimestamp - _timestamp;
        var fixedInterval = fixedDeltaTime / timeScale *
                            stopwatchFrequency;
        var maximumForwardCorrection = fixedInterval *
                                       (_catchUpSpeed - 1.0);
        var maximumBackwardCorrection = fixedInterval * 0.25;
        var wantsForwardCorrection = phaseError > 0.0;
        var correction = wantsForwardCorrection && suppressForwardCorrection
            ? 0.0
            : wantsForwardCorrection
                ? Math.Min(maximumForwardCorrection, phaseError)
                : Math.Max(-maximumBackwardCorrection, phaseError * 0.125);
        LastForwardCorrectionSuppressed =
            wantsForwardCorrection && suppressForwardCorrection;
        LastCorrectionTicks = (long)Math.Round(correction);
        _timestamp += correction;
      }
    }
    _hasTick = true;
    _lastFixedTime = fixedTime;
    timestamp = (long)Math.Round(_timestamp);
    return true;
  }
}

internal sealed class PoseHistoryBuffer {
  private readonly PoseHistorySample[] _items;
  private readonly long[] _publishedSequences;
  private long _nextSequence;

  public int Capacity => _items.Length;
  public long TotalWritten => Volatile.Read(ref _nextSequence);

  public PoseHistoryBuffer(int capacity) {
    if (capacity < 1)
      throw new ArgumentOutOfRangeException(nameof(capacity));
    _items = new PoseHistorySample[capacity];
    _publishedSequences = new long[capacity];
    for (var i = 0; i < capacity; i++)
      _publishedSequences[i] = -1;
  }

  public void Add(PoseHistorySample sample) {
    var sequence = Interlocked.Increment(ref _nextSequence) - 1;
    sample.Sequence = sequence;
    var index = (int)(sequence % _items.Length);
    _items[index] = sample;
    Thread.MemoryBarrier();
    Volatile.Write(ref _publishedSequences[index], sequence);
  }

  public PoseHistorySample[] CopyChronological() {
    var end = Volatile.Read(ref _nextSequence);
    var start = Math.Max(0, end - _items.Length);
    var result = new List<PoseHistorySample>((int)(end - start));
    for (var sequence = start; sequence < end; sequence++) {
      var index = (int)(sequence % _items.Length);
      if (Volatile.Read(ref _publishedSequences[index]) != sequence)
        continue;
      var sample = _items[index];
      Thread.MemoryBarrier();
      if (Volatile.Read(ref _publishedSequences[index]) == sequence &&
          sample.Sequence == sequence)
        result.Add(sample);
    }
    return result.ToArray();
  }

  public bool TryGetBracket(
      long timestamp, out PoseHistorySample before,
      out PoseHistorySample after, out PoseBracketStatus status
  ) {
    var end = Volatile.Read(ref _nextSequence);
    var start = Math.Max(0, end - _items.Length);
    if (end <= start || !TryReadSequence(start, out var oldest) ||
        !TryReadSequence(end - 1, out var newest)) {
      before = new PoseHistorySample();
      after = new PoseHistorySample();
      status = PoseBracketStatus.Unavailable;
      return false;
    }
    if (timestamp <= oldest.Timestamp) {
      before = oldest;
      after = oldest;
      status = timestamp == oldest.Timestamp
          ? PoseBracketStatus.Exact
          : PoseBracketStatus.BeforeHistory;
      return true;
    }
    if (timestamp >= newest.Timestamp) {
      before = newest;
      after = newest;
      status = timestamp == newest.Timestamp
          ? PoseBracketStatus.Exact
          : PoseBracketStatus.AfterHistory;
      return true;
    }

    var low = start;
    var high = end - 1;
    while (low <= high) {
      var middle = low + (high - low) / 2;
      if (!TryReadSequence(middle, out var sample)) {
        before = new PoseHistorySample();
        after = new PoseHistorySample();
        status = PoseBracketStatus.Unavailable;
        return false;
      }
      if (sample.Timestamp == timestamp) {
        before = sample;
        after = sample;
        status = PoseBracketStatus.Exact;
        return true;
      }
      if (sample.Timestamp < timestamp)
        low = middle + 1;
      else
        high = middle - 1;
    }
    if (high < start || low >= end ||
        !TryReadSequence(high, out before) ||
        !TryReadSequence(low, out after)) {
      before = new PoseHistorySample();
      after = new PoseHistorySample();
      status = PoseBracketStatus.Unavailable;
      return false;
    }
    status = PoseBracketStatus.Interpolated;
    return true;
  }

  public bool TryGetLatestPair(
      out PoseHistorySample previous, out PoseHistorySample latest
  ) {
    var end = Volatile.Read(ref _nextSequence);
    var start = Math.Max(0, end - _items.Length);
    if (end - start < 2 ||
        !TryReadSequence(end - 2, out previous) ||
        !TryReadSequence(end - 1, out latest)) {
      previous = new PoseHistorySample();
      latest = new PoseHistorySample();
      return false;
    }
    return latest.Timestamp > previous.Timestamp;
  }

  public bool TryGetHeadVerticalVelocity(
      long windowTicks, long stopwatchFrequency, out float metresPerSecond
  ) {
    metresPerSecond = 0f;
    if (windowTicks <= 0 || stopwatchFrequency <= 0)
      return false;
    var end = Volatile.Read(ref _nextSequence);
    if (end <= 0 || !TryReadSequence(end - 1, out var latest) ||
        !latest.Head.IsValid)
      return false;
    var targetTimestamp = latest.Timestamp - windowTicks;
    if (!TryGetBracket(
            targetTimestamp, out var before, out var after, out var status
        ) || (status != PoseBracketStatus.Exact &&
              status != PoseBracketStatus.Interpolated))
      return false;
    var span = after.Timestamp - before.Timestamp;
    var amount = span <= 0 ? 0f : (float)Math.Max(0.0, Math.Min(
        1.0, (targetTimestamp - before.Timestamp) / (double)span
    ));
    var earlier = PoseHistoryMath.Interpolate(
        before.Head, after.Head, amount
    );
    if (!earlier.IsValid)
      return false;
    var elapsed = (latest.Timestamp - targetTimestamp) /
                  (double)stopwatchFrequency;
    if (elapsed <= 0.0)
      return false;
    metresPerSecond = (float)((latest.Head.Y - earlier.Y) / elapsed);
    return !float.IsNaN(metresPerSecond) &&
           !float.IsInfinity(metresPerSecond);
  }

  private bool TryReadSequence(long sequence, out PoseHistorySample sample) {
    var index = (int)(sequence % _items.Length);
    if (Volatile.Read(ref _publishedSequences[index]) != sequence) {
      sample = new PoseHistorySample();
      return false;
    }
    sample = _items[index];
    Thread.MemoryBarrier();
    return Volatile.Read(ref _publishedSequences[index]) == sequence &&
           sample.Sequence == sequence;
  }
}

internal static class PoseHistoryMath {
  public static PoseMatrix3x4 ToMatrix(TrackedPoseValue pose) {
    var x = pose.Qx;
    var y = pose.Qy;
    var z = pose.Qz;
    var w = pose.Qw;
    var xx = x * x;
    var yy = y * y;
    var zz = z * z;
    var xy = x * y;
    var xz = x * z;
    var yz = y * z;
    var wx = w * x;
    var wy = w * y;
    var wz = w * z;
    return new PoseMatrix3x4 {
      M0 = 1f - 2f * (yy + zz), M1 = 2f * (xy - wz),
      M2 = 2f * (xz + wy), M3 = pose.X,
      M4 = 2f * (xy + wz), M5 = 1f - 2f * (xx + zz),
      M6 = 2f * (yz - wx), M7 = pose.Y,
      M8 = 2f * (xz - wy), M9 = 2f * (yz + wx),
      M10 = 1f - 2f * (xx + yy), M11 = pose.Z,
    };
  }

  public static TrackedPoseValue FromMatrix(
      PoseMatrix3x4 matrix, bool valid, bool connected, int trackingResult
  ) {
    var result = new TrackedPoseValue {
      IsValid = valid,
      IsConnected = connected,
      TrackingResult = trackingResult,
      X = matrix.M3,
      Y = matrix.M7,
      Z = matrix.M11,
    };
    MatrixToQuaternion(
        matrix, out result.Qx, out result.Qy, out result.Qz, out result.Qw
    );
    return result;
  }

  public static TrackedPoseValue Interpolate(
      TrackedPoseValue first, TrackedPoseValue second, float amount
  ) {
    if (!first.IsValid)
      return second;
    if (!second.IsValid)
      return first;
    amount = Math.Max(0f, Math.Min(1f, amount));
    var result = new TrackedPoseValue {
      IsValid = true,
      IsConnected = first.IsConnected && second.IsConnected,
      TrackingResult = amount < 0.5f
          ? first.TrackingResult
          : second.TrackingResult,
      X = Lerp(first.X, second.X, amount),
      Y = Lerp(first.Y, second.Y, amount),
      Z = Lerp(first.Z, second.Z, amount),
    };
    Slerp(
        first.Qx, first.Qy, first.Qz, first.Qw,
        second.Qx, second.Qy, second.Qz, second.Qw, amount,
        out result.Qx, out result.Qy, out result.Qz, out result.Qw
    );
    return result;
  }

  public static TrackedPoseValue Extrapolate(
      TrackedPoseValue first, TrackedPoseValue second, float amount
  ) {
    if (!first.IsValid)
      return second;
    if (!second.IsValid)
      return first;
    amount = Math.Max(1f, amount);
    var result = new TrackedPoseValue {
      IsValid = true,
      IsConnected = first.IsConnected && second.IsConnected,
      TrackingResult = second.TrackingResult,
      X = Lerp(first.X, second.X, amount),
      Y = Lerp(first.Y, second.Y, amount),
      Z = Lerp(first.Z, second.Z, amount),
    };
    Slerp(
        first.Qx, first.Qy, first.Qz, first.Qw,
        second.Qx, second.Qy, second.Qz, second.Qw, amount,
        out result.Qx, out result.Qy, out result.Qz, out result.Qw
    );
    return result;
  }

  private static float Lerp(float first, float second, float amount) =>
      first + (second - first) * amount;

  private static void MatrixToQuaternion(
      PoseMatrix3x4 m, out float x, out float y, out float z, out float w
  ) {
    var trace = m.M0 + m.M5 + m.M10;
    if (trace > 0f) {
      var scale = (float)Math.Sqrt(trace + 1f) * 2f;
      w = 0.25f * scale;
      x = (m.M9 - m.M6) / scale;
      y = (m.M2 - m.M8) / scale;
      z = (m.M4 - m.M1) / scale;
    } else if (m.M0 > m.M5 && m.M0 > m.M10) {
      var scale = (float)Math.Sqrt(1f + m.M0 - m.M5 - m.M10) * 2f;
      w = (m.M9 - m.M6) / scale;
      x = 0.25f * scale;
      y = (m.M1 + m.M4) / scale;
      z = (m.M2 + m.M8) / scale;
    } else if (m.M5 > m.M10) {
      var scale = (float)Math.Sqrt(1f + m.M5 - m.M0 - m.M10) * 2f;
      w = (m.M2 - m.M8) / scale;
      x = (m.M1 + m.M4) / scale;
      y = 0.25f * scale;
      z = (m.M6 + m.M9) / scale;
    } else {
      var scale = (float)Math.Sqrt(1f + m.M10 - m.M0 - m.M5) * 2f;
      w = (m.M4 - m.M1) / scale;
      x = (m.M2 + m.M8) / scale;
      y = (m.M6 + m.M9) / scale;
      z = 0.25f * scale;
    }
    Normalize(ref x, ref y, ref z, ref w);
  }

  private static void Slerp(
      float ax, float ay, float az, float aw,
      float bx, float by, float bz, float bw, float amount,
      out float x, out float y, out float z, out float w
  ) {
    var dot = ax * bx + ay * by + az * bz + aw * bw;
    if (dot < 0f) {
      dot = -dot;
      bx = -bx;
      by = -by;
      bz = -bz;
      bw = -bw;
    }
    if (dot > 0.9995f) {
      x = Lerp(ax, bx, amount);
      y = Lerp(ay, by, amount);
      z = Lerp(az, bz, amount);
      w = Lerp(aw, bw, amount);
      Normalize(ref x, ref y, ref z, ref w);
      return;
    }
    dot = Math.Max(-1f, Math.Min(1f, dot));
    var theta = Math.Acos(dot);
    var sinTheta = Math.Sin(theta);
    var firstWeight = Math.Sin((1.0 - amount) * theta) / sinTheta;
    var secondWeight = Math.Sin(amount * theta) / sinTheta;
    x = (float)(ax * firstWeight + bx * secondWeight);
    y = (float)(ay * firstWeight + by * secondWeight);
    z = (float)(az * firstWeight + bz * secondWeight);
    w = (float)(aw * firstWeight + bw * secondWeight);
    Normalize(ref x, ref y, ref z, ref w);
  }

  private static void Normalize(
      ref float x, ref float y, ref float z, ref float w
  ) {
    var length = Math.Sqrt(x * x + y * y + z * z + w * w);
    if (length < 1e-12) {
      x = y = z = 0f;
      w = 1f;
      return;
    }
    var inverse = (float)(1.0 / length);
    x *= inverse;
    y *= inverse;
    z *= inverse;
    w *= inverse;
  }
}
