using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Sst.BoneworksFrameFix;

// Scheduling-only comparison. This does not measure OpenVR or in-game FPS.
internal static class SamplerBenchmark
{
  public static void Run()
  {
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      throw new PlatformNotSupportedException("Thread CPU timing uses GetThreadTimes.");
    Measure(true);
    Measure(false);
  }

  private static void Measure(bool spin)
  {
    var worker = new Thread(() =>
    {
      using var stop = new ManualResetEvent(false);
      using var wait = new SamplerWait(stop);
      var gaps = new List<double>(1000);
      var started = Stopwatch.GetTimestamp();
      var end = started + Stopwatch.Frequency * 2;
      var interval = Stopwatch.Frequency / 250;
      var next = started;
      var last = started;
      var cpuStarted = CpuTicks();
      var allocatedStarted = GC.GetAllocatedBytesForCurrentThread();
      while (Stopwatch.GetTimestamp() < end)
      {
        var now = Stopwatch.GetTimestamp();
        var remaining = next - now;
        if (remaining > 0)
        {
          if (spin)
          {
            var waitMs = (int)(remaining * 1000L / Stopwatch.Frequency);
            if (waitMs > 0) stop.WaitOne(waitMs);
            else { Thread.SpinWait(32); stop.WaitOne(0); }
          } else
          {
            wait.Until(next);
          }
          continue;
        }
        if (now > started) gaps.Add((now - last) * 1000.0 / Stopwatch.Frequency);
        last = now;
        next = spin ? next + interval : SamplerSchedule.Next(next, now, interval);
        if (spin && now - next > interval * 4) next = now + interval;
      }
      var cpuMs = (CpuTicks() - cpuStarted) / 10000.0;
      var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedStarted;
      var elapsedMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
      gaps.Sort();
      Console.WriteLine($"{(spin ? "old-spin" : "blocking")}: CPU {cpuMs:F2} ms / {elapsedMs:F0} ms " +
          $"({cpuMs / elapsedMs * 100:F2}% of one logical CPU), " +
          $"{gaps.Count * 1000.0 / elapsedMs:F1} Hz, gap p95 {gaps[(int)(gaps.Count * 0.95)]:F3} ms, max {gaps[gaps.Count - 1]:F3} ms, allocated {allocated} bytes");
    });
    worker.Start();
    worker.Join();
  }

  private static long CpuTicks()
  {
    if (!GetThreadTimes(GetCurrentThread(), out _, out _, out var kernel, out var user))
      throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    return kernel + user;
  }

  [DllImport("kernel32.dll")]
  private static extern IntPtr GetCurrentThread();

  [DllImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool GetThreadTimes(IntPtr thread, out long created, out long exited,
      out long kernel, out long user);
}
