using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Sst.BoneworksPerformance;

// High-resolution, cancellable waits without spinning or changing system timer
// resolution.
internal sealed class SamplerWait : IDisposable {
  private sealed class TimerHandle : WaitHandle {
    public TimerHandle() {
      // CREATE_WAITABLE_TIMER_HIGH_RESOLUTION; TIMER_MODIFY_STATE |
      // SYNCHRONIZE.
      var handle = CreateWaitableTimerEx(IntPtr.Zero, null, 2, 0x00100002);
      if (handle == IntPtr.Zero)
        throw new Win32Exception(
            Marshal.GetLastWin32Error(),
            "Could not create high-resolution input timer."
        );
      SafeWaitHandle = new SafeWaitHandle(handle, true);
    }
  }

  private readonly TimerHandle _timer = new TimerHandle();
  private readonly WaitHandle _stop;
  private readonly IntPtr[] _handles;

  public SamplerWait(WaitHandle stop) {
    _stop = stop;
    _handles = new[] {
      stop.SafeWaitHandle.DangerousGetHandle(),
      _timer.SafeWaitHandle.DangerousGetHandle()
    };
  }

  public bool Until(long timestamp) {
    var remaining = timestamp - Stopwatch.GetTimestamp();
    if (remaining <= 0)
      return !_stop.WaitOne(0);
    var due = -Math.Max(
        1L, (long)Math.Ceiling(remaining * 10000000.0 / Stopwatch.Frequency)
    );
    if (!SetWaitableTimerEx(
            _timer.SafeWaitHandle, ref due, 0, IntPtr.Zero, IntPtr.Zero,
            IntPtr.Zero, 0
        ))
      throw new Win32Exception(Marshal.GetLastWin32Error());
    // Avoid Mono's managed WaitAny temporary handle arrays on every sample.
    var result = WaitForMultipleObjects(2, _handles, false, uint.MaxValue);
    if (result == uint.MaxValue)
      throw new Win32Exception(Marshal.GetLastWin32Error());
    return result == 1;
  }

  public void Dispose() => _timer.Dispose();

  [DllImport(
      "kernel32.dll", EntryPoint = "CreateWaitableTimerExW",
      CharSet = CharSet.Unicode, SetLastError = true
  )]
  private static extern IntPtr CreateWaitableTimerEx(
      IntPtr attributes, string name, uint flags, uint access
  );

  [DllImport("kernel32.dll", SetLastError = true)]
  [return:MarshalAs(UnmanagedType.Bool)]
  private static extern bool SetWaitableTimerEx(
      SafeWaitHandle timer, ref long dueTime, int period,
      IntPtr completionRoutine, IntPtr argument, IntPtr wakeContext,
      uint tolerableDelay
  );

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern uint WaitForMultipleObjects(
      uint count, [In] IntPtr[] handles,
      [MarshalAs(UnmanagedType.Bool)] bool waitAll, uint milliseconds
  );
}
