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
  const int LOADING_MARGIN_MS = 0;
  const int RETRY_INTERVAL_MS = 500;

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
  };

  public static ProcessModule VrclientModule;
  public static IntPtr ProcessHandle;
  public static bool IsLoading = false;
  public static HashSet<IntPtr> PossibleAddresses = null;
  public static System.Timers.Timer ScanTimer = null;

  public AslHelper() {
    IsDev = true;
    IsAllowedInRuns = true;
  }

  public override void OnUpdate() {
    if (PossibleAddresses != null && PossibleAddresses.Count <= 1)
      return;

    var sceneIndex = SceneManager.GetActiveScene().buildIndex;
    var isNowLoading = sceneIndex == LOADING_SCENE_INDEX;
    if (sceneIndex >= 0 && isNowLoading != IsLoading) {
      IsLoading = isNowLoading;

      DoAfter(LOADING_MARGIN_MS, () => {
        if (PossibleAddresses == null) {
          if (IsLoading) {
            var process = Process.GetCurrentProcess();
            ProcessHandle = OpenProcess(PROCESS_VM_READ, false, process.Id);
            VrclientModule = GetModule(VRCLIENT64_MODULE_NAME);

            InitialScan();
          }
        } else {
          FilterScan();
        }
      });
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

  public static void InitialScan() {
    Dbg.Log("InitialScan()");

    var targetValue = 1;
    var possibleAddresses = new HashSet<IntPtr>();

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

    for (var i = 0; i < bytesRead; i += VALUE_SIZE) {
      if (bytesRead - i >= VALUE_SIZE) {
        var value = BitConverter.ToInt32(buffer, i);
        if (value == targetValue) {
          possibleAddresses.Add(VrclientModule.BaseAddress + i);
        }
      }
    }

    var totalMb = VrclientModule.ModuleMemorySize / MEGABYTE;
    Dbg.Log(
        $"ASL: Initial scan found {possibleAddresses.Count} possible addresses ({totalMb}mb scanned)");

    PossibleAddresses = possibleAddresses;
  }

  public static void FilterScan() {
    var isLoading = IsLoading;
    var targetValue = isLoading ? 1 : 0;
    Dbg.Log($"FilterScan({targetValue})");

    if (PossibleAddresses == null) {
      throw new Exception(
          "Cannot filter possible addresses unless InitialScan has been called");
    }

    var prevCount = PossibleAddresses.Count;
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

    foreach (var address in PossibleAddresses) {
      var value = BitConverter.ToInt32(
          buffer, (int)((long)address - (long)VrclientModule.BaseAddress));
      if (value == targetValue) {
        filteredAddresses.Add(address);
      }
    }

    Dbg.Log(
        $"ASL: Filtered down to {filteredAddresses.Count} possible addresses (was {prevCount})");

    // Update address list after a delay in case we scanned the memory on the
    // boundary of a load starting/ending
    DoAfter(LOADING_MARGIN_MS,
            () => UpdatePossibleAddresses(filteredAddresses, isLoading));
  }

  public static void UpdatePossibleAddresses(HashSet<IntPtr> addresses,
                                             bool wasLoading) {
    if (wasLoading != IsLoading)
      return;

    PossibleAddresses = addresses;
    if (addresses.Count == 1) {
      BitConverter.GetBytes(addresses.First().ToInt32()).CopyTo(AslSigBytes, 8);
      AslSigBytes[0] = 0xD5;
      MelonLogger.Msg("Found ASL loading address");
      Dbg.Log($"Loading address = {addresses.First()}");
    } else if (addresses.Count == 0) {
      MelonLogger.Warning(
          "Failed to find ASL loading address (no candidates remaining)");
    } else if (addresses.Count <= 10) {
      DoAfter(RETRY_INTERVAL_MS - LOADING_MARGIN_MS, FilterScan);
    }
  }

  public static void DoAfter(int delayMs, Action callback) {
    CancelTimer();
    ScanTimer = new System.Timers.Timer() {
      Interval = Math.Max(1, delayMs),
      AutoReset = false,
      Enabled = true,
    };
    ScanTimer.Elapsed += (source, args) => {
      CancelTimer();
      callback();
    };
  }

  public static void CancelTimer() {
    if (ScanTimer != null) {
      ScanTimer.Dispose();
      ScanTimer = null;
    }
  }
}
}
