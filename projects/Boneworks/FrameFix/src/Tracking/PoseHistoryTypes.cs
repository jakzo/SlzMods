using System;
using System.Threading;

namespace Sst.BoneworksFrameFix;

internal partial struct TrackedPoseValue
{
  public bool IsValid;
  public float X, Y, Z;
}

internal partial struct PoseHistorySample
{
  public long Sequence;
  public long Timestamp;
  public TrackedPoseValue Head;
}

internal enum PoseBracketStatus
{
  Unavailable,
  Exact,
  Interpolated,
  Extrapolated,
  BeforeHistory,
  AfterHistory,
  Stale,
}

internal struct TimedButtonEdge
{
  public long Timestamp;
  public bool Pressed;
  public bool IsInitial;

  public TimedButtonEdge(long timestamp, bool pressed, bool isInitial = false)
  {
    Timestamp = timestamp;
    Pressed = pressed;
    IsInitial = isInitial;
  }
}

internal enum SlowMotionReplayCommand
{
  None,
  DecreaseTimeScale,
  ToggleTimeScale,
}

internal sealed class SlowMotionReplayState
{
  private readonly long _toggleDelayTicks;

  public long PendingToggleTimestamp { get; private set; }

  public SlowMotionReplayState(long stopwatchFrequency)
  {
    if (stopwatchFrequency <= 0)
      throw new ArgumentOutOfRangeException(nameof(stopwatchFrequency));
    _toggleDelayTicks = stopwatchFrequency / 4;
  }

  public void Reset() => PendingToggleTimestamp = 0;

  public SlowMotionReplayCommand Apply(TimedButtonEdge edge)
  {
    if (edge.Pressed)
    {
      PendingToggleTimestamp = 0;
      return SlowMotionReplayCommand.DecreaseTimeScale;
    }
    PendingToggleTimestamp = edge.Timestamp + _toggleDelayTicks;
    return SlowMotionReplayCommand.None;
  }

  public SlowMotionReplayCommand ConsumeToggleAt(long timestamp)
  {
    if (PendingToggleTimestamp == 0 ||
        PendingToggleTimestamp > timestamp)
      return SlowMotionReplayCommand.None;
    PendingToggleTimestamp = 0;
    return SlowMotionReplayCommand.ToggleTimeScale;
  }
}

internal sealed class FixedTimeMapper
{
  private long _anchorTimestamp;
  private float _anchorGameTime;
  private float _anchorTimeScale;
  private bool _hasAnchor;

  public void ObserveFrame(
      long timestamp, float gameTime, float timeScale
  )
  {
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
  )
  {
    anchorTimestamp = _anchorTimestamp;
    anchorGameTime = _anchorGameTime;
    anchorTimeScale = _anchorTimeScale;
    if (!_hasAnchor || stopwatchFrequency <= 0)
    {
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

internal sealed class FixedTickTimeline
{
  private readonly FixedTimeMapper _mapper;
  private readonly double _catchUpSpeed;
  private bool _hasTick;
  private float _lastFixedTime;
  private double _timestamp;

  public long LastCorrectionTicks { get; private set; }
  public bool LastForwardCorrectionSuppressed { get; private set; }

  public FixedTickTimeline(
      FixedTimeMapper mapper, double catchUpSpeed = 2.0
  )
  {
    _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    _catchUpSpeed = Math.Max(1.0, Math.Min(8.0, catchUpSpeed));
  }

  public void Reset()
  {
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
  )
  {
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
    if (!isNextTick)
    {
      _timestamp = independentlyMappedTimestamp;
      reanchored = _hasTick;
    } else
    {
      _timestamp = nextTimestamp;
      if (beginRenderFrame)
      {
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

internal sealed partial class PoseHistoryBuffer
{
  private readonly PoseHistorySample[] _items;
  private readonly long[] _publishedSequences;
  private long _nextSequence;

  public int Capacity => _items.Length;
  public long TotalWritten => Volatile.Read(ref _nextSequence);

  public PoseHistoryBuffer(int capacity)
  {
    if (capacity < 1)
      throw new ArgumentOutOfRangeException(nameof(capacity));
    _items = new PoseHistorySample[capacity];
    _publishedSequences = new long[capacity];
    for (var i = 0; i < capacity; i++)
      _publishedSequences[i] = -1;
  }

  public void Add(PoseHistorySample sample)
  {
    var sequence = Interlocked.Increment(ref _nextSequence) - 1;
    sample.Sequence = sequence;
    var index = (int)(sequence % _items.Length);
    Volatile.Write(ref _publishedSequences[index], -1);
    Thread.MemoryBarrier();
    _items[index] = sample;
    Thread.MemoryBarrier();
    Volatile.Write(ref _publishedSequences[index], sequence);
  }

  public bool TryGetBracket(
      long timestamp, out PoseHistorySample before,
      out PoseHistorySample after, out PoseBracketStatus status
  )
  {
    var end = Volatile.Read(ref _nextSequence);
    var start = Math.Max(0, end - _items.Length);
    if (end <= start || !TryReadSequence(start, out var oldest) ||
        !TryReadSequence(end - 1, out var newest))
    {
      before = new PoseHistorySample();
      after = new PoseHistorySample();
      status = PoseBracketStatus.Unavailable;
      return false;
    }
    if (timestamp <= oldest.Timestamp)
    {
      before = oldest;
      after = oldest;
      status = timestamp == oldest.Timestamp
          ? PoseBracketStatus.Exact
          : PoseBracketStatus.BeforeHistory;
      return true;
    }
    if (timestamp >= newest.Timestamp)
    {
      before = newest;
      after = newest;
      status = timestamp == newest.Timestamp
          ? PoseBracketStatus.Exact
          : PoseBracketStatus.AfterHistory;
      return true;
    }

    var low = start;
    var high = end - 1;
    while (low <= high)
    {
      var middle = low + (high - low) / 2;
      if (!TryReadSequence(middle, out var sample))
      {
        before = new PoseHistorySample();
        after = new PoseHistorySample();
        status = PoseBracketStatus.Unavailable;
        return false;
      }
      if (sample.Timestamp == timestamp)
      {
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
        !TryReadSequence(low, out after))
    {
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
  )
  {
    var end = Volatile.Read(ref _nextSequence);
    var start = Math.Max(0, end - _items.Length);
    if (end - start < 2 ||
        !TryReadSequence(end - 2, out previous) ||
        !TryReadSequence(end - 1, out latest))
    {
      previous = new PoseHistorySample();
      latest = new PoseHistorySample();
      return false;
    }
    return latest.Timestamp > previous.Timestamp;
  }

  public bool TryGetHeadVerticalVelocity(
      long windowTicks, long stopwatchFrequency, out float metresPerSecond
  )
  {
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

  private bool TryReadSequence(long sequence, out PoseHistorySample sample)
  {
    var index = (int)(sequence % _items.Length);
    if (Volatile.Read(ref _publishedSequences[index]) != sequence)
    {
      sample = new PoseHistorySample();
      return false;
    }
    sample = _items[index];
    Thread.MemoryBarrier();
    return Volatile.Read(ref _publishedSequences[index]) == sequence &&
           sample.Sequence == sequence;
  }
}

internal static partial class PoseHistoryMath
{

  public static TrackedPoseValue Interpolate(
      TrackedPoseValue first, TrackedPoseValue second, float amount
  )
  {
    if (!first.IsValid)
      return second;
    if (!second.IsValid)
      return first;
    amount = Math.Max(0f, Math.Min(1f, amount));
    var result = new TrackedPoseValue
    {
      IsValid = true,
      X = Lerp(first.X, second.X, amount),
      Y = Lerp(first.Y, second.Y, amount),
      Z = Lerp(first.Z, second.Z, amount),
    };
#if DEBUG
    DebugInterpolateRotation(first, second, amount, ref result);
#endif
    return result;
  }

  public static TrackedPoseValue Extrapolate(
      TrackedPoseValue first, TrackedPoseValue second, float amount
  )
  {
    if (!first.IsValid)
      return second;
    if (!second.IsValid)
      return first;
    amount = Math.Max(1f, amount);
    var result = new TrackedPoseValue
    {
      IsValid = true,
      X = Lerp(first.X, second.X, amount),
      Y = Lerp(first.Y, second.Y, amount),
      Z = Lerp(first.Z, second.Z, amount),
    };
#if DEBUG
    DebugInterpolateRotation(first, second, amount, ref result);
#endif
    return result;
  }

  private static float Lerp(float first, float second, float amount) =>
      first + (second - first) * amount;

}
