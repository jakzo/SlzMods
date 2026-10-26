using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Sst.BoneworksFrameFix;

// A separate legacy OpenVR client is necessary: GetControllerState is frozen in
// the game's action-based client. Never initialize or update that client's input.
internal static class Program
{
  [DllImport("kernel32", CharSet = CharSet.Unicode)]
  private static extern bool SetDllDirectory(string path);
  [DllImport("openvr_api", CallingConvention = CallingConvention.Cdecl)]
  private static extern uint VR_InitInternal(ref int error, int type);
  [DllImport("openvr_api", CallingConvention = CallingConvention.Cdecl)]
  private static extern void VR_ShutdownInternal();
  [DllImport("openvr_api", CallingConvention = CallingConvention.Cdecl)]
  private static extern IntPtr VR_GetGenericInterface(string version, ref int error);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]
  private delegate uint GetRole(int role);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]
  [return: MarshalAs(UnmanagedType.I1)]
  private delegate bool GetState(uint device, IntPtr data, uint size);

  private static int Main(string[] args)
  {
    if (args.Length != 4)
      return 1;
    var initialized = false;
    try
    {
      using var parent = Process.GetProcessById(int.Parse(args[0]));
      using var stop = EventWaitHandle.OpenExisting(args[1] + ".stop");
      using var buffer = new NativeButtonBuffer(args[1], false);
      using var wait = new SamplerWait(stop);
      var interval = Stopwatch.Frequency / Math.Max(30, Math.Min(1000, int.Parse(args[3])));
      if (!SetDllDirectory(args[2]))
        return 2;
      var error = 0;
      VR_InitInternal(ref error, 3); // VRApplication_Background, no scene or overlay.
      if (error != 0)
        return 3;
      initialized = true;
      var table = VR_GetGenericInterface("FnTable:IVRSystem_019", ref error);
      if (table == IntPtr.Zero || error != 0)
        return 4;
      // Fixed ABI from Valve's IVRSystem_019, not the runtime's newest interface.
      var role = (GetRole)Marshal.GetDelegateForFunctionPointer(
          Marshal.ReadIntPtr(table, 18 * IntPtr.Size), typeof(GetRole));
      var state = (GetState)Marshal.GetDelegateForFunctionPointer(
          Marshal.ReadIntPtr(table, 34 * IntPtr.Size), typeof(GetState));
      var data = Marshal.AllocHGlobal(64); // Windows x64 VRControllerState_t.
      try
      {
        var next = Stopwatch.GetTimestamp();
        while (!parent.HasExited && !stop.WaitOne(0))
        {
          var started = Stopwatch.GetTimestamp();
          var leftValid = Read(state, role(1), data, out var left);
          var rightValid = Read(state, role(2), data, out var right);
          var timestamp = started + (Stopwatch.GetTimestamp() - started) / 2;
          buffer.Publish(timestamp, left, right, (leftValid ? 1 : 0) | (rightValid ? 2 : 0));
          next = SamplerSchedule.Next(next, Stopwatch.GetTimestamp(), interval);
          if (!wait.Until(next))
            break;
        }
      }
      finally { Marshal.FreeHGlobal(data); }
      return 0;
    }
    catch { return 5; } // The parent detects exit and keeps the action fallback.
    finally { if (initialized) VR_ShutdownInternal(); }
  }

  private static bool Read(GetState state, uint device, IntPtr data, out ulong pressed)
  {
    var valid = device != uint.MaxValue && state(device, data, 64);
    pressed = valid ? unchecked((ulong)Marshal.ReadInt64(data, 8)) : 0;
    return valid;
  }
}
