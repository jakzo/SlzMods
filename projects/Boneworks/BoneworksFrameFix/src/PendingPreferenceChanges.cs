using System.Threading;

namespace Sst.BoneworksFrameFix;

internal enum SettingsChange { Physics = 1, Tracking = 2, Hud = 4, Slots = 8 }

// Coalesce preference events without touching Unity from the event's thread.
internal sealed class PendingPreferenceChanges {
  private int _pending;

  public void Mark(SettingsChange change) {
    int previous;
    do {
      previous = Volatile.Read(ref _pending);
    } while (Interlocked.CompareExchange(
                 ref _pending, previous | (int)change, previous
             ) != previous);
  }

  public SettingsChange Consume() => (SettingsChange
  )Interlocked.Exchange(ref _pending, 0);
}
