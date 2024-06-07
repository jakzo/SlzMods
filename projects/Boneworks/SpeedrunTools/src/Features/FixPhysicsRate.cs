using MelonLoader;
using UnityEngine;

namespace Sst.Features {
class FixPhysicsRate : Feature {
  public readonly Pref<float> PrefTargetRate = new Pref<float>() {
    Id = "targetPhysicsRate",
    Name = "Physics tick rate in hertz or 0 to use setting from game menu.",
    DefaultValue = 0f,
  };

  public FixPhysicsRate() { IsDev = true; }

  public void LockPhysicsToRefreshRate(bool isLocked) {
    Resources.Load<Valve.VR.SteamVR_Settings>("SteamVR_Settings")
        .lockPhysicsUpdateRateToRenderFrequency = isLocked;
  }

  public override void OnEnabled() { LockPhysicsToRefreshRate(false); }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    LockPhysicsToRefreshRate(false);
  }

  public override void OnDisabled() { LockPhysicsToRefreshRate(true); }

  public override void OnLateUpdate() {
    var targetRate = PrefTargetRate.Read();
    if (targetRate == 0f)
      targetRate = Data_Manager.Instance?.physicsUpdateRate ?? 0f;
    if (targetRate > 0f)
      Time.fixedDeltaTime = 1f / targetRate;
  }
}
}
