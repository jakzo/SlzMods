using MelonLoader;
using UnityEngine;
using Valve.VR;

namespace Sst.BoneworksPerformance;

internal sealed class PhysicsRateMenu {
  private readonly bool _useMenuRate;
  private readonly int _customRate;
  private SteamVR_Settings _steamSettings;
  private bool _originalLockSetting;
  private bool _hasOriginalLockSetting;
  private int _lastSelectedRate = int.MinValue;

  public PhysicsRateMenu(bool useMenuRate, int customRate) {
    _useMenuRate = useMenuRate;
    _customRate = customRate;
  }

  private bool ShouldOverride => _useMenuRate || _customRate > 0;

  public void Initialize() {
    if (ShouldOverride)
      UnlockPhysicsRate();
  }

  public void ResetScene() {
    _lastSelectedRate = int.MinValue;
    if (ShouldOverride)
      UnlockPhysicsRate();
  }

  public void OnLateUpdate() {
    if (!ShouldOverride)
      return;
    UnlockPhysicsRate();
    var selectedRate = _customRate;
    if (selectedRate <= 0) {
      var manager = Data_Manager.Instance;
      if (!manager)
        return;
      selectedRate = manager.physicsUpdateRate;
    }
    if (selectedRate != _lastSelectedRate) {
      _lastSelectedRate = selectedRate;
      MelonLogger.Msg(
          selectedRate > 0
              ? (_customRate > 0
                  ? "Custom physics tick rate selected " + selectedRate +
                    " Hz."
                  : "Physics-rate menu selected " + selectedRate + " Hz.")
              : "Physics-rate menu has no valid selected rate."
      );
    }
    if (Time.timeScale <= 0f || selectedRate <= 0)
      return;
    var selectedDelta = Time.timeScale / selectedRate;
    if (Mathf.Abs(Time.fixedDeltaTime - selectedDelta) > 0.0000001f)
      Time.fixedDeltaTime = selectedDelta;
  }

  public void Shutdown() {
    if (_steamSettings && _hasOriginalLockSetting) {
      _steamSettings.lockPhysicsUpdateRateToRenderFrequency =
          _originalLockSetting;
    }
  }

  private void UnlockPhysicsRate() {
    if (!_steamSettings) {
      _steamSettings = Resources.Load<SteamVR_Settings>("SteamVR_Settings");
      if (!_steamSettings)
        return;
      _originalLockSetting =
          _steamSettings.lockPhysicsUpdateRateToRenderFrequency;
      _hasOriginalLockSetting = true;
    }
    if (_steamSettings.lockPhysicsUpdateRateToRenderFrequency)
      _steamSettings.lockPhysicsUpdateRateToRenderFrequency = false;
  }
}
