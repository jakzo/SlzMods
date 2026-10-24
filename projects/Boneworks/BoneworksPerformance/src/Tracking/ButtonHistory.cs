using System.Collections.Generic;

namespace Sst.BoneworksPerformance;

internal enum SampledButton : byte { Unavailable, Released, Held }

internal struct SampledButtons
{
  public long Timestamp;
  public int Generation;
  public SampledButton Jump, SlowMotion;
}

// Owned by the main thread. The sampler publishes states through PoseHistoryBuffer.
internal sealed class ButtonHistory
{
  private readonly Queue<TimedButtonEdge> _pending = new Queue<TimedButtonEdge>();
  private bool _observed, _pressed;
  public bool Held { get; private set; }
  public long LatestTimestamp { get; private set; }
  public long NextTimestamp => _pending.Count == 0 ? long.MaxValue : _pending.Peek().Timestamp;

  public void Reset()
  {
    _pending.Clear();
    _observed = _pressed = Held = false;
    LatestTimestamp = 0;
  }

  public bool Observe(long timestamp, SampledButton button)
  {
    if (button == SampledButton.Unavailable || timestamp <= LatestTimestamp)
      return false;
    var pressed = button == SampledButton.Held;
    // A paused game may keep observing inputs without consuming any physics ticks.
    // Rebaseline rather than replay an unbounded backlog after it resumes.
    if (_pending.Count >= 4096)
      Reset();
    var changed = !_observed || pressed != _pressed;
    if (changed)
      _pending.Enqueue(new TimedButtonEdge(timestamp, pressed, !_observed));
    _observed = true;
    _pressed = pressed;
    LatestTimestamp = timestamp;
    return changed;
  }

  public bool IsFresh(long now, long maximumAge) =>
      _observed && now - LatestTimestamp <= maximumAge;

  public bool TryConsume(long timestamp, out TimedButtonEdge edge)
  {
    if (_pending.Count == 0 || NextTimestamp > timestamp)
    {
      edge = default;
      return false;
    }
    edge = _pending.Dequeue();
    Held = edge.Pressed;
    return true;
  }
}
