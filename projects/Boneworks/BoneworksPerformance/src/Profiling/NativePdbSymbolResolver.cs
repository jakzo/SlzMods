#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MelonLoader;

namespace Sst.BoneworksPerformance;

internal static class NativePdbSymbolResolver {
  private const uint SymoptCaseInsensitive = 0x00000001;
  private const uint SymoptUndname = 0x00000002;
  private const uint SymoptDeferredLoads = 0x00000004;
  private const int MaximumSymbolName = 2048;
  private static readonly object Sync = new object();
  private static IntPtr _process;
  private static bool _initialized;
  private static bool _unityPlayerLoaded;

  public static bool UnityPlayerLoaded => _unityPlayerLoaded;

  public static void Initialize(IEnumerable<NativeModuleRange> modules) {
    lock (Sync) {
      if (_initialized)
        return;
      _process = Process.GetCurrentProcess().Handle;
      SymSetOptions(
          SymoptCaseInsensitive | SymoptUndname | SymoptDeferredLoads
      );
      if (!SymInitialize(_process, null, false)) {
        MelonLogger.Warning(
            "DbgHelp could not initialize native PDB symbol resolution."
        );
        return;
      }
      var loaded = 0;
      foreach (var module in modules) {
        if (!string.Equals(
                module.Name, "UnityPlayer.dll",
                StringComparison.OrdinalIgnoreCase
            ))
          continue;
        var moduleInfo = Process.GetCurrentProcess().MainModule;
        foreach (ProcessModule candidate in Process.GetCurrentProcess().Modules) {
          if (string.Equals(
                  candidate.ModuleName, module.Name,
                  StringComparison.OrdinalIgnoreCase
              )) {
            moduleInfo = candidate;
            break;
          }
        }
        if (SymLoadModuleEx(
                _process, IntPtr.Zero, moduleInfo.FileName, null,
                unchecked((ulong)module.Start),
                unchecked((uint)(module.End - module.Start)),
                IntPtr.Zero, 0
            ) != 0)
          loaded++;
      }
      _initialized = true;
      _unityPlayerLoaded = loaded > 0;
      MelonLogger.Msg(
          loaded == 0
              ? "UnityPlayer PDB was not found; native offsets will be retained."
              : "Loaded UnityPlayer native PDB symbols through DbgHelp."
      );
    }
  }

  public static bool TryResolve(
      long address, NativeModuleRange module, out string name
  ) {
    name = null;
    if (!_initialized || !string.Equals(
            module.Name, "UnityPlayer.dll", StringComparison.OrdinalIgnoreCase
        ))
      return false;
    lock (Sync) {
      var bytes = Marshal.SizeOf(typeof(SymbolInfo)) + MaximumSymbolName;
      var buffer = Marshal.AllocHGlobal(bytes);
      try {
        for (var i = 0; i < bytes; i++)
          Marshal.WriteByte(buffer, i, 0);
        var symbol = new SymbolInfo {
          SizeOfStruct = unchecked((uint)Marshal.SizeOf(typeof(SymbolInfo))),
          MaxNameLen = MaximumSymbolName,
        };
        Marshal.StructureToPtr(symbol, buffer, false);
        if (!SymFromAddr(
                _process, unchecked((ulong)address), out var displacement,
                buffer
            ))
          return false;
        symbol = (SymbolInfo)Marshal.PtrToStructure(
            buffer, typeof(SymbolInfo)
        );
        if ((symbol.Size > 0 && displacement >= symbol.Size) ||
            (symbol.Size == 0 && displacement > 65536))
          return false;
        var nameAddress = IntPtr.Add(
            buffer, Marshal.OffsetOf(typeof(SymbolInfo), "Name").ToInt32()
        );
        name = Marshal.PtrToStringAnsi(
            nameAddress, unchecked((int)symbol.NameLen)
        );
        return !string.IsNullOrEmpty(name);
      } finally {
        Marshal.FreeHGlobal(buffer);
      }
    }
  }

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
  private struct SymbolInfo {
    public uint SizeOfStruct;
    public uint TypeIndex;
    public ulong Reserved1;
    public ulong Reserved2;
    public uint Index;
    public uint Size;
    public ulong ModBase;
    public uint Flags;
    public ulong Value;
    public ulong Address;
    public uint Register;
    public uint Scope;
    public uint Tag;
    public uint NameLen;
    public uint MaxNameLen;
    public byte Name;
  }

  [DllImport("dbghelp.dll", SetLastError = true)]
  private static extern bool SymInitialize(
      IntPtr process, string userSearchPath, bool invadeProcess
  );

  [DllImport("dbghelp.dll", SetLastError = true)]
  private static extern uint SymSetOptions(uint options);

  [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
  private static extern ulong SymLoadModuleEx(
      IntPtr process, IntPtr file, string imageName, string moduleName,
      ulong baseOfDll, uint dllSize, IntPtr data, uint flags
  );

  [DllImport("dbghelp.dll", SetLastError = true)]
  private static extern bool SymFromAddr(
      IntPtr process, ulong address, out ulong displacement, IntPtr symbol
  );
}
#endif
