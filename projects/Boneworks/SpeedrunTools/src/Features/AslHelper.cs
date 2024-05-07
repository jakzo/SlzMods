using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sst.Features {
class AslHelper : Feature {
  [DllImport("kernel32.dll")]
  static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle,
                                   int dwProcessId);

  [DllImport("kernel32.dll")]
  static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
                                       byte[] lpBuffer, int dwSize,
                                       out int lpNumberOfBytesRead);

  const int PROCESS_VM_READ = 0x0010;
  const int MEGABYTE = 1024 * 1024;
  const int VALUE_SIZE = sizeof(int);
  const string VRCLIENT64_MODULE_NAME = "vrclient_x64.dll";
  const int LOADING_SCENE_INDEX = 25;
  const int LOADING_MARGIN_MS = 100;

  public static byte[] AslSigBytes = {
    // 0 = magic string start
    // Signature is set dynamically to avoid finding this hardcoded array
    0x00, // 0xD5
    0xE2,
    0x03,
    0x34,
    0xC2,
    0xDF,
    0x63,
    0xB3,
    // 8 = address
    0x00,
    0x00,
    0x00,
    0x00,
    0x00,
    0x00,
    0x00,
    0x00,
  };

  public static ProcessModule VrclientModule;
  public static IntPtr ProcessHandle;
  public static bool IsLoading = false;
  public static HashSet<IntPtr> PossibleAddresses = null;

  public AslHelper() { IsAllowedInRuns = true; }

  public override void OnUpdate() {
    if (PossibleAddresses != null && PossibleAddresses.Count <= 1)
      return;

    var sceneIndex = SceneManager.GetActiveScene().buildIndex;
    var isNowLoading = sceneIndex == LOADING_SCENE_INDEX;
    if (sceneIndex >= 0 && isNowLoading != IsLoading) {
      IsLoading = isNowLoading;

      if (PossibleAddresses == null) {
        if (!IsLoading)
          return;

        var process = Process.GetCurrentProcess();
        ProcessHandle = OpenProcess(PROCESS_VM_READ, false, process.Id);
        VrclientModule = GetModule(VRCLIENT64_MODULE_NAME);
      }

      var timer = new System.Timers.Timer() {
        Interval = LOADING_MARGIN_MS,
        AutoReset = false,
        Enabled = true,
      };
      timer.Elapsed += (source, args) => {
        timer.Dispose();
        Scan();
      };
    }
  }

  public static ProcessModule GetModule(string name) {
    foreach (ProcessModule module in Process.GetCurrentProcess().Modules) {
      if (module.ModuleName == name) {
        return module;
      }
    }
    throw new Exception($"Could not find module: {name}");
  }

  public static IEnumerable<IntPtr> AllAddresses() {
    var moduleEnd =
        VrclientModule.BaseAddress + VrclientModule.ModuleMemorySize;
    for (var i = VrclientModule.BaseAddress; (long)i < (long)moduleEnd;
         i += VALUE_SIZE) {
      yield return i;
    }
  }

  public static void Scan() {
    var isLoading = IsLoading;
    var targetValue = isLoading ? 1 : 0;

    if (PossibleAddresses == null && !isLoading) {
      throw new Exception("Initial call to Scan was not called while loading");
    }

    Dbg.Log($"Scan({targetValue})");

    var prevCount = PossibleAddresses?.Count ?? 0;
    var filteredAddresses = new HashSet<IntPtr>();

    var buffer = new byte[VrclientModule.ModuleMemorySize];
    var readSucceeded =
        ReadProcessMemory(ProcessHandle, VrclientModule.BaseAddress, buffer,
                          buffer.Length, out var bytesRead);

    if (!readSucceeded) {
      MelonLogger.Error(
          $"Read of {VRCLIENT64_MODULE_NAME} memory to find loading state address failed",
          new Win32Exception(Marshal.GetLastWin32Error()));
      return;
    }

    foreach (var address in PossibleAddresses ?? AllAddresses()) {
      var offset = (int)((long)address - (long)VrclientModule.BaseAddress);
      if (offset > buffer.Length)
        break;
      var value = BitConverter.ToInt32(buffer, offset);
      if (value == targetValue) {
        filteredAddresses.Add(address);
      }
    }

    if (PossibleAddresses == null) {
      var totalMb = VrclientModule.ModuleMemorySize / MEGABYTE;
      MelonLogger.Msg(
          $"ASL: Initial scan found {filteredAddresses.Count} possible addresses ({totalMb}mb scanned)");
    } else {
      MelonLogger.Msg(
          $"ASL: Filtered down to {filteredAddresses.Count} possible addresses (was {prevCount})");
    }

    PossibleAddresses = filteredAddresses;
    if (filteredAddresses.Count == 1) {
      BitConverter.GetBytes(filteredAddresses.First().ToInt64())
          .CopyTo(AslSigBytes, 8);
      AslSigBytes[0] = 0xD5;
      MelonLogger.Msg("Found ASL loading address");
      Dbg.Log($"Loading address = {filteredAddresses.First()}");
    } else if (filteredAddresses.Count == 0) {
      MelonLogger.Warning(
          "Failed to find ASL loading address (no candidates remaining)");
    }
  }
}
}
