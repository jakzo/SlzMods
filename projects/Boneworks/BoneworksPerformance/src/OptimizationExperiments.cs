#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MelonLoader;
using UnityEngine;

namespace Sst.BoneworksPerformance;

internal enum OptimizationPreset {
  Baseline,
  PerformanceCores,
  PerformanceCoresHighPriority,
  PerformanceCoresHighPriorityReducedLodBias,
}

internal sealed class OptimizationExperiments {
  private readonly MelonPreferences_Entry<bool> _performanceCoresOnly;
  private readonly MelonPreferences_Entry<bool> _highProcessPriority;
  private readonly MelonPreferences_Entry<bool> _reduceLodBias;
  private readonly Process _process = Process.GetCurrentProcess();
  private readonly ProcessPriorityClass _originalPriority;
  private readonly float _originalLodBias;
  private readonly int _originalMaxQueuedFrames;
  private uint[] _performanceCpuSets = new uint[0];
  private OptimizationPreset _activePreset;
  private bool _reduceLodBiasActive;
  private int _frameQueueOverrideDepth = -1;
  private int _frameQueueOverrideCount;

  public int OriginalMaxQueuedFrames => _originalMaxQueuedFrames;

  public OptimizationExperiments(
      MelonPreferences_Entry<bool> performanceCoresOnly,
      MelonPreferences_Entry<bool> highProcessPriority,
      MelonPreferences_Entry<bool> reduceLodBias
  ) {
    _performanceCoresOnly = performanceCoresOnly;
    _highProcessPriority = highProcessPriority;
    _reduceLodBias = reduceLodBias;
    _originalPriority = _process.PriorityClass;
    _originalLodBias = QualitySettings.lodBias;
    _originalMaxQueuedFrames = QualitySettings.maxQueuedFrames;
    _performanceCpuSets = DiscoverPerformanceCpuSets();
  }

  public void ApplyConfigured() {
    ClearFrameQueueOverride();
    QualitySettings.maxQueuedFrames = _originalMaxQueuedFrames;
    Apply(
        _performanceCoresOnly.Value,
        _highProcessPriority.Value,
        _reduceLodBias.Value,
        "Configured"
    );
  }

  public void ApplyPreset(OptimizationPreset preset) {
    _activePreset = preset;
    switch (preset) {
      case OptimizationPreset.PerformanceCores:
        Apply(true, false, false, preset.ToString());
        break;
      case OptimizationPreset.PerformanceCoresHighPriority:
        Apply(true, true, false, preset.ToString());
        break;
      case OptimizationPreset.PerformanceCoresHighPriorityReducedLodBias:
        Apply(true, true, true, preset.ToString());
        break;
      default:
        Apply(false, false, false, preset.ToString());
        break;
    }
  }

  public void ApplySceneSettings() {
    if (_reduceLodBiasActive)
      QualitySettings.lodBias = _originalLodBias * 0.75f;
  }

  public int ApplyFrameQueueDepth(int requestedDepth) {
    ClearFrameQueueOverride();
    Apply(false, false, false, "FrameQueueBaseline");
    var depth = requestedDepth < 0
        ? _originalMaxQueuedFrames
        : requestedDepth;
    _frameQueueOverrideDepth = depth;
    _frameQueueOverrideCount = 0;
    MaintainFrameQueueDepth();
    MelonLogger.Msg(
        "Frame-queue experiment: requested=" + depth + ", immediate=" +
        QualitySettings.maxQueuedFrames + ", startup value=" +
        _originalMaxQueuedFrames + "."
    );
    return depth;
  }

  public void MaintainFrameQueueDepth() {
    if (_frameQueueOverrideDepth < 0 ||
        QualitySettings.maxQueuedFrames == _frameQueueOverrideDepth)
      return;
    _frameQueueOverrideCount++;
    try {
      QualitySettings.maxQueuedFrames = _frameQueueOverrideDepth;
    } catch (Exception exception) {
      MelonLogger.Warning(
          "Could not change the maximum queued frames: " + exception.Message
      );
      _frameQueueOverrideDepth = -1;
    }
  }

  public void Restore() {
    ClearFrameQueueOverride();
    SetDefaultCpuSets(new uint[0]);
    try {
      _process.PriorityClass = _originalPriority;
    } catch {
    }
    QualitySettings.lodBias = _originalLodBias;
    QualitySettings.maxQueuedFrames = _originalMaxQueuedFrames;
  }

  private void ClearFrameQueueOverride() {
    if (_frameQueueOverrideDepth >= 0) {
      MelonLogger.Msg(
          "Frame-queue backend overrides while depth " +
          _frameQueueOverrideDepth + " was active: " +
          _frameQueueOverrideCount + "."
      );
    }
    _frameQueueOverrideDepth = -1;
    _frameQueueOverrideCount = 0;
  }

  private void Apply(
      bool performanceCoresOnly,
      bool highPriority,
      bool reduceLodBias,
      string label
  ) {
    _reduceLodBiasActive = reduceLodBias;
    var cpuSetsApplied = SetDefaultCpuSets(
        performanceCoresOnly ? _performanceCpuSets : new uint[0]
    );
    try {
      _process.PriorityClass = highPriority
          ? ProcessPriorityClass.High
          : _originalPriority;
    } catch (Exception exception) {
      MelonLogger.Warning(
          "Could not change process priority: " + exception.Message
      );
    }
    QualitySettings.lodBias = reduceLodBias
        ? _originalLodBias * 0.75f
        : _originalLodBias;
    MelonLogger.Msg(
        "Optimization preset " + label + ": performance CPU sets=" +
        (performanceCoresOnly ? _performanceCpuSets.Length.ToString() : "off") +
        " (applied=" + cpuSetsApplied + "), priority=" +
        _process.PriorityClass + ", LOD bias=" +
        QualitySettings.lodBias.ToString("0.###") + " (original=" +
        _originalLodBias.ToString("0.###") + ")."
    );
  }

  private bool SetDefaultCpuSets(uint[] cpuSets) {
    try {
      return SetProcessDefaultCpuSets(
          _process.Handle,
          cpuSets.Length == 0 ? null : cpuSets,
          (uint)cpuSets.Length
      );
    } catch (Exception exception) {
      MelonLogger.Warning(
          "Could not set process CPU sets: " + exception.Message
      );
      return false;
    }
  }

  private static uint[] DiscoverPerformanceCpuSets() {
    uint required;
    GetSystemCpuSetInformation(IntPtr.Zero, 0, out required, IntPtr.Zero, 0);
    if (required == 0)
      return new uint[0];
    var buffer = Marshal.AllocHGlobal((int)required);
    try {
      if (!GetSystemCpuSetInformation(
              buffer, required, out required, IntPtr.Zero, 0
          ))
        return new uint[0];
      var entries = new List<CpuSetEntry>();
      var offset = 0;
      byte maximumEfficiencyClass = 0;
      while (offset + 8 <= required) {
        var entry = IntPtr.Add(buffer, offset);
        var size = Marshal.ReadInt32(entry, 0);
        if (size < 20 || offset + size > required)
          break;
        var type = Marshal.ReadInt32(entry, 4);
        if (type == 0) {
          var value = new CpuSetEntry {
            Id = unchecked((uint)Marshal.ReadInt32(entry, 8)),
            EfficiencyClass = Marshal.ReadByte(entry, 18),
          };
          entries.Add(value);
          if (value.EfficiencyClass > maximumEfficiencyClass)
            maximumEfficiencyClass = value.EfficiencyClass;
        }
        offset += size;
      }
      var selected = new List<uint>();
      for (var i = 0; i < entries.Count; i++) {
        var entry = entries[i];
        if (entry.EfficiencyClass == maximumEfficiencyClass)
          selected.Add(entry.Id);
      }
      MelonLogger.Msg(
          "CPU-set topology: " + entries.Count + " logical processors, " +
          selected.Count + " in performance class " +
          maximumEfficiencyClass + "."
      );
      return selected.ToArray();
    } finally {
      Marshal.FreeHGlobal(buffer);
    }
  }

  private struct CpuSetEntry {
    public uint Id;
    public byte EfficiencyClass;
  }

  [DllImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool GetSystemCpuSetInformation(
      IntPtr information, uint bufferLength, out uint returnedLength,
      IntPtr process, uint flags
  );

  [DllImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool SetProcessDefaultCpuSets(
      IntPtr process, uint[] cpuSetIds, uint cpuSetIdCount
  );
}
#endif
