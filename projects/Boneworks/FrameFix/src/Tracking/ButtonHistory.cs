using System;
using System.Collections.Generic;

namespace Sst.BoneworksFrameFix;

internal enum SampledButton : byte { Unavailable, Released, Held }

internal struct SampledButtons
{
  public long Timestamp;
  public int Generation;
  public SampledButton Jump, SlowMotion;
}

// Main-thread owned. Keep action and native observations on separate timelines:
// the worker can already have sampled inputs newer than the game's action cache.
internal sealed class ButtonHistory
{
  private const int Capacity = 4096;
  private readonly Timeline _native = new Timeline();
  private readonly Timeline _actions = new Timeline();
  private readonly Queue<TimedButtonEdge> _evidence = new Queue<TimedButtonEdge>();
  private bool _nativeReady;
  private long _lastDeliveredTimestamp;
  private long _validatedActionTimestamp;

  public bool NativeRejected { get; private set; }
  public bool Held { get; private set; }
  public long LatestTimestamp => _native.LatestTimestamp;
  private bool UseNative => !NativeRejected &&
      (_nativeReady || !_actions.Observed && _native.Observed);
  private Queue<TimedButtonEdge> Pending => UseNative ? _native.Pending : _actions.Pending;
  public long NextTimestamp
  {
    get
    {
      DiscardDuplicates(Pending);
      return Pending.Count == 0 ? long.MaxValue : Pending.Peek().Timestamp;
    }
  }

  public void Reset()
  {
    _native.Reset();
    _actions.Reset();
    _evidence.Clear();
    _nativeReady = NativeRejected = Held = false;
    _lastDeliveredTimestamp = _validatedActionTimestamp = 0;
  }

  public bool Observe(long timestamp, SampledButton button)
  {
    if (NativeRejected)
      return false;
    var changed = _native.Observe(timestamp, button);
    if (changed)
    {
      if (_evidence.Count == Capacity)
        _evidence.Dequeue();
      _evidence.Enqueue(new TimedButtonEdge(timestamp, button == SampledButton.Held));
    }
    return changed;
  }

  public bool ObserveLive(long timestamp, long now, long maximumAge,
      SampledButton button, long timestampTolerance = 0)
  {
    if (!_actions.Observed && _evidence.Count > 0)
    {
      var baseline = _evidence.Peek();
      if (baseline.Timestamp < timestamp)
        _actions.Observe(baseline.Timestamp,
            baseline.Pressed ? SampledButton.Held : SampledButton.Released);
    }
    var changed = _actions.Observe(timestamp, button);
    if (!_actions.Observed || NativeRejected)
      return changed;
    if (!IsFresh(now, maximumAge))
    {
      _nativeReady = false;
      return changed;
    }
    timestamp = _actions.ChangeTimestamp;
    if (_validatedActionTimestamp == timestamp)
    {
      _nativeReady = true;
      return changed;
    }
    var matches = NativeMatchesAt(timestamp, timestampTolerance, _actions.Pressed);
    // Missing coverage is not a disagreement. Recheck after the worker has
    // sampled the event, without disturbing an already validated native stream.
    if (!matches.HasValue)
    {
      if (_evidence.Count > 0 && timestamp + Math.Max(0, timestampTolerance) < _evidence.Peek().Timestamp)
        _nativeReady = false;
      return changed;
    }
    if (matches.Value)
    {
      _validatedActionTimestamp = timestamp;
      _nativeReady = true;
    }
    else
    {
      NativeRejected = true;
      _native.Pending.Clear();
    }
    return changed;
  }

  private bool? NativeMatchesAt(long timestamp, long tolerance, bool pressed)
  {
    if (_evidence.Count == 0)
      return null;
    var start = timestamp - Math.Max(0, tolerance);
    var end = timestamp + Math.Max(0, tolerance);
    var first = _evidence.Peek();
    if (end < first.Timestamp)
      return null;
    var state = first.Pressed;
    foreach (var edge in _evidence)
    {
      if (edge.Timestamp <= start)
        state = edge.Pressed;
      else if (edge.Timestamp <= end)
      {
        if (state == pressed || edge.Pressed == pressed)
          return true;
        state = edge.Pressed;
      }
      else
        break;
    }
    if (state == pressed)
      return true;
    return _native.LatestTimestamp >= end ? false : (bool?)null;
  }

  public bool IsFresh(long now, long maximumAge) =>
      _native.Observed && now >= LatestTimestamp && now - LatestTimestamp <= maximumAge;

  public bool TryConsume(long timestamp, out TimedButtonEdge edge)
  {
    var pending = Pending;
    DiscardDuplicates(pending);
    while (pending.Count > 0 && pending.Peek().Timestamp <= timestamp)
    {
      edge = pending.Dequeue();
      // Source changes must not replay an edge already delivered by the other
      // source. Late corrective actions retain their original event timestamp.
      if (edge.Timestamp <= _lastDeliveredTimestamp &&
          (UseNative || edge.IsInitial || edge.Pressed == Held))
        continue;
      // A new source's baseline is not a new gameplay baseline. If it changes
      // an already delivered held state, the consumer needs the real edge.
      if (_lastDeliveredTimestamp != 0)
        edge.IsInitial = false;
      _lastDeliveredTimestamp = Math.Max(_lastDeliveredTimestamp, edge.Timestamp);
      Held = edge.Pressed;
      var other = UseNative ? _actions.Pending : _native.Pending;
      while (other.Count > 0 && other.Peek().Timestamp <= edge.Timestamp)
        other.Dequeue();
      return true;
    }
    edge = default;
    return false;
  }

  private void DiscardDuplicates(Queue<TimedButtonEdge> pending)
  {
    while (_lastDeliveredTimestamp != 0 && pending.Count > 0 &&
        (pending.Peek().Pressed == Held ||
         UseNative && pending.Peek().Timestamp <= _lastDeliveredTimestamp))
      pending.Dequeue();
  }

  private sealed class Timeline
  {
    public readonly Queue<TimedButtonEdge> Pending = new Queue<TimedButtonEdge>();
    public bool Observed { get; private set; }
    public long LatestTimestamp { get; private set; }
    public long ChangeTimestamp { get; private set; }
    public bool Pressed { get; private set; }

    public void Reset()
    {
      Pending.Clear();
      Observed = Pressed = false;
      LatestTimestamp = ChangeTimestamp = 0;
    }

    public bool Observe(long timestamp, SampledButton button)
    {
      if (button == SampledButton.Unavailable || timestamp <= LatestTimestamp)
        return false;
      if (Pending.Count == Capacity)
        Reset();
      var pressed = button == SampledButton.Held;
      var changed = !Observed || pressed != Pressed;
      if (changed)
      {
        Pending.Enqueue(new TimedButtonEdge(timestamp, pressed, !Observed));
        ChangeTimestamp = timestamp;
      }
      Observed = true;
      Pressed = pressed;
      LatestTimestamp = timestamp;
      return changed;
    }
  }
}
