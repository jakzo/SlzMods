using UnityEngine;
using Valve.VR;

namespace Sst.BoneworksFrameFix;

internal sealed partial class PhysicsRateMenu {
  private readonly bool _useMenuRate;
  private readonly int _customRate;
  private SteamVR_Settings _steamSettings;
  private bool _originalLockSetting;

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
#if DEBUG
    DebugResetRateLog();
#endif
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
#if DEBUG
    DebugRateSelected(selectedRate);
#endif
    if (Time.timeScale <= 0f || selectedRate <= 0)
      return;
    var selectedDelta = Time.timeScale / selectedRate;
    if (Mathf.Abs(Time.fixedDeltaTime - selectedDelta) > 0.0000001f)
      Time.fixedDeltaTime = selectedDelta;
  }

  public void Shutdown() {
    if (_steamSettings) {
      _steamSettings.lockPhysicsUpdateRateToRenderFrequency =
          _originalLockSetting;
    }
  }

  private void UnlockPhysicsRate() {
    if (!_steamSettings) {
      var settings = Resources.Load<SteamVR_Settings>("SteamVR_Settings");
      if (!settings)
        return;
      _originalLockSetting = settings.lockPhysicsUpdateRateToRenderFrequency;
      _steamSettings = settings;
    }
    if (_steamSettings.lockPhysicsUpdateRateToRenderFrequency)
      _steamSettings.lockPhysicsUpdateRateToRenderFrequency = false;
  }
}
