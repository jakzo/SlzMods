using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Sst.BoneworksFrameFix;

internal sealed class NativeInputClock
{
  private readonly long _nativeAnchor, _managedAnchor;
  private readonly double _scale;

  public NativeInputClock()
  {
    if (!QueryPerformanceFrequency(out var frequency))
      throw new InvalidOperationException("Could not calibrate the input clock.");
    var shortest = long.MaxValue;
    for (var i = 0; i < 5; i++)
    {
      var before = Stopwatch.GetTimestamp();
      if (!QueryPerformanceCounter(out var native))
        throw new InvalidOperationException("Could not calibrate the input clock.");
      var elapsed = Stopwatch.GetTimestamp() - before;
      if (elapsed >= shortest)
        continue;
      shortest = elapsed;
      _nativeAnchor = native;
      _managedAnchor = before + elapsed / 2;
    }
    _scale = (double)Stopwatch.Frequency / frequency;
  }

  internal NativeInputClock(long nativeAnchor, long managedAnchor, long nativeFrequency, long managedFrequency)
  {
    _nativeAnchor = nativeAnchor;
    _managedAnchor = managedAnchor;
    _scale = (double)managedFrequency / nativeFrequency;
  }

  // Unity's Mono Stopwatch has a process-local epoch. Never compare it directly
  // with the helper's .NET Framework Stopwatch, even on the same Windows machine.
  public long ToManaged(long timestamp) => _managedAnchor +
      (long)Math.Round((timestamp - _nativeAnchor) * _scale);

  [DllImport("kernel32")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool QueryPerformanceCounter(out long value);
  [DllImport("kernel32")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool QueryPerformanceFrequency(out long value);
}
