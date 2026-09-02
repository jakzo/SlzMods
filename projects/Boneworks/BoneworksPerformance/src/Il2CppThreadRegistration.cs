using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Sst.BoneworksPerformance;

internal sealed class Il2CppThreadRegistration : IDisposable {
  private IntPtr _thread;

  private Il2CppThreadRegistration(IntPtr thread) {
    _thread = thread;
  }

  public static Il2CppThreadRegistration Attach() {
    var domain = Il2CppDomainGet();
    if (domain == IntPtr.Zero)
      throw new InvalidOperationException("IL2CPP domain is unavailable.");
    var thread = Il2CppThreadAttach(domain);
    if (thread == IntPtr.Zero)
      throw new InvalidOperationException("IL2CPP rejected the pose thread.");
    return new Il2CppThreadRegistration(thread);
  }

  public void Dispose() {
    var thread = Interlocked.Exchange(ref _thread, IntPtr.Zero);
    if (thread != IntPtr.Zero)
      Il2CppThreadDetach(thread);
  }

  [DllImport("GameAssembly.dll", EntryPoint = "il2cpp_domain_get",
      CallingConvention = CallingConvention.Cdecl)]
  private static extern IntPtr Il2CppDomainGet();

  [DllImport("GameAssembly.dll", EntryPoint = "il2cpp_thread_attach",
      CallingConvention = CallingConvention.Cdecl)]
  private static extern IntPtr Il2CppThreadAttach(IntPtr domain);

  [DllImport("GameAssembly.dll", EntryPoint = "il2cpp_thread_detach",
      CallingConvention = CallingConvention.Cdecl)]
  private static extern void Il2CppThreadDetach(IntPtr thread);
}
