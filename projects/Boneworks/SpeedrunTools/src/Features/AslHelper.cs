using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
using Valve.VR;
using System.ComponentModel;

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
  const int BUFFER_SIZE = 1 * MEGABYTE;
  const int VALUE_SIZE = sizeof(int);
  const string VRCLIENT64_MODULE_NAME = "vrclient_x64.dll";
  const float LOADING_MARGIN = 0.5f;

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
  public static bool IsNotLoading = false;
  public static ConcurrentBag<IntPtr> PossibleAddresses = null;

  public AslHelper() {
    IsDev = true;
    IsAllowedInRuns = true;
  }

  public override void OnUpdate() {
    if (SceneManager.GetActiveScene().name == "loadingScene") {
      // TODO: Use this instead of fade to grid since ASL will use this
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
    if (!IsLoading) {
      throw new Exception("First scan can only be run while loading");
    }

    var isLoading = IsLoading;
    var targetValue = isLoading ? 1 : 0;
    var possibleAddresses = new ConcurrentBag<IntPtr>();
    var totalMb = VrclientModule.ModuleMemorySize / MEGABYTE;

    var result = Parallel.For(
        0, VrclientModule.ModuleMemorySize / BUFFER_SIZE,
        (index, loopState) => {
          var hasStateChanged = isLoading ? !IsLoading : !IsNotLoading;
          if (hasStateChanged) {
            var progressMb = index * BUFFER_SIZE / MEGABYTE;
            MelonLogger.Warning(
                $"ASL loading scan could not finish in time ({progressMb}mb / {totalMb}mb)");
            // TODO: Keep track of progress and resume next time?
            loopState.Break();
            return;
          }

          var buffer = new byte[BUFFER_SIZE];
          var readAddress = VrclientModule.BaseAddress + index * BUFFER_SIZE;
          var readSucceeded =
              ReadProcessMemory(ProcessHandle, readAddress, buffer,
                                buffer.Length, out var bytesRead);

          if (!readSucceeded) {
            MelonLogger.Error(
                $"Read of {VRCLIENT64_MODULE_NAME} memory to find loading state address failed",
                new Win32Exception(Marshal.GetLastWin32Error()));
            loopState.Break();
            return;
          }

          for (var i = 0; i < bytesRead; i += VALUE_SIZE) {
            if (bytesRead - i >= VALUE_SIZE) {
              var value = BitConverter.ToInt32(buffer, i);
              if (value == targetValue) {
                possibleAddresses.Add(readAddress + i);
              }
            }
          }
        });

    if (!result.IsCompleted) {
      return;
    }

    Dbg.Log(
        $"ASL: Initial scan found {possibleAddresses.Count} possible addresses ({totalMb}mb scanned)");
    PossibleAddresses = possibleAddresses;
  }

  public static void FilterScan() {
    if (PossibleAddresses == null) {
      throw new Exception(
          "Cannot filter possible addresses unless InitialScan has been called");
    }

    var prevCount = PossibleAddresses.Count;
    var filteredAddresses = new ConcurrentBag<IntPtr>();

    if (!IsLoading && !IsNotLoading) {
      throw new Exception(
          "Cannot start ASL loading scan because state is not clear");
    }

    var isLoading = IsLoading;
    var targetValue = isLoading ? 1 : 0;

    // TODO: Batch nearby addresses into a single ReadProcessMemory call
    var result = Parallel.ForEach(PossibleAddresses, (address, loopState) => {
      var hasStateChanged = isLoading ? !IsLoading : !IsNotLoading;
      if (hasStateChanged) {
        MelonLogger.Warning("ASL loading scan could not finish in time");
        // TODO: Keep track of progress and resume next time?
        loopState.Break();
        return;
      }

      var buffer = new byte[VALUE_SIZE];
      var readSucceeded = ReadProcessMemory(ProcessHandle, address, buffer,
                                            buffer.Length, out var bytesRead);

      if (!readSucceeded) {
        MelonLogger.Error(
            $"Read of {VRCLIENT64_MODULE_NAME} memory to find loading state address failed",
            new Win32Exception(Marshal.GetLastWin32Error()));
        loopState.Break();
        return;
      }

      var value = BitConverter.ToInt32(buffer, 0);
      if (value == targetValue) {
        filteredAddresses.Add(address);
      }
    });

    // TODO: Keep track of progress and resume next time?
    if (!result.IsCompleted && result.LowestBreakIteration.HasValue) {
      var totalMb = VrclientModule.ModuleMemorySize / MEGABYTE;
      var progressMb =
          result.LowestBreakIteration.Value * BUFFER_SIZE / MEGABYTE;
      MelonLogger.Warning(
          $"ASL loading scan could not finish in time ({progressMb}mb / {totalMb}mb)");
      return;
    }

    Dbg.Log(
        $"ASL: Filtered down to {filteredAddresses.Count} possible addresses (was {prevCount})");
    PossibleAddresses = filteredAddresses;

    if (PossibleAddresses.Count == 1) {
      BitConverter.GetBytes(PossibleAddresses.First().ToInt32())
          .CopyTo(AslSigBytes, 8);
      AslSigBytes[0] = 0xD5;
      MelonLogger.Msg("Found ASL loading address");
    } else if (PossibleAddresses.Count == 0) {
      MelonLogger.Warning(
          "Failed to find ASL loading address (no candidates remaining)");
    }

    // TODO: If count is less than 10, filter at a regular interval until it
    // reaches 1
  }

  public static void SetLoading(bool value) {
    IsLoading = value;

    if (IsLoading) {
      if (PossibleAddresses == null) {
        var process = Process.GetCurrentProcess();
        ProcessHandle = OpenProcess(PROCESS_VM_READ, false, process.Id);
        VrclientModule = GetModule(VRCLIENT64_MODULE_NAME);

        InitialScan();
      } else if (PossibleAddresses.Count > 1) {
        FilterScan();
      }
    }
  }

  public static void SetNotLoading(bool value) {
    IsNotLoading = value;

    if (IsNotLoading && PossibleAddresses.Count > 1) {
      FilterScan();
    }
  }

  // TODO: This actually isn't what makes the loading bit in vrclient turn true
  // TODO: Use active scene instead since that is what the ASL would be using
  [HarmonyPatch(typeof(CVRCompositor), nameof(CVRCompositor.FadeGrid))]
  class CVRCompositor_FadeGrid_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(float fSeconds, bool bFadeIn) {
      if (bFadeIn) {
        DoAfter(fSeconds - LOADING_MARGIN, () => SetNotLoading(false));
        DoAfter(fSeconds + LOADING_MARGIN, () => SetLoading(true));
      } else {
        SetLoading(false);
        DoAfter(LOADING_MARGIN, () => SetNotLoading(true));
      }
    }
  }

  private static void DoAfter(float seconds, Action callback) {
    if (seconds <= 0) {
      callback();
      return;
    }

    new System.Threading.Timer(timer => {
      ((System.Threading.Timer)timer).Dispose();
      callback();
    }, null, (int)(seconds * 1000f), Timeout.Infinite);
  }
}
}
