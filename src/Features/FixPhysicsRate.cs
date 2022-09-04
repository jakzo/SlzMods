using MelonLoader;
using UnityEngine;

namespace SpeedrunTools.Features {
class FixPhysicsRate : Feature {
  private Valve.VR.SteamVR_Settings _steamVrSettings;
  private Data_Manager _dataManager;
  private int _lastPhysicsRate;

  public FixPhysicsRate() { IsEnabledByDefault = false; }

  public override void OnApplicationStart() {
    _steamVrSettings =
        Resources.Load<Valve.VR.SteamVR_Settings>("SteamVR_Settings");
  }

  public override void OnEnabled() {
    if (_steamVrSettings != null)
      _steamVrSettings.lockPhysicsUpdateRateToRenderFrequency = false;
  }

  public override void OnDisabled() {
    if (_steamVrSettings != null)
      _steamVrSettings.lockPhysicsUpdateRateToRenderFrequency = true;
    _dataManager = null;
  }

  public override void OnSceneWasLoaded(int buildIndex, string sceneName) {
    _dataManager = null;
  }

  public override void OnUpdate() {
    if (_dataManager == null)
      _dataManager = Data_Manager.Instance;
    if (_dataManager.physicsUpdateRate != _lastPhysicsRate &&
        Time.timeScale > 0 && _dataManager.physicsUpdateRate > 0) {
      Time.fixedDeltaTime =
          Time.timeScale / (float)_dataManager.physicsUpdateRate;
      _lastPhysicsRate = _dataManager.physicsUpdateRate;
    }
  }
}
}
