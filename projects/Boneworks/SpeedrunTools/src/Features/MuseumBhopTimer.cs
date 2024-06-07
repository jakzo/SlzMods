using MelonLoader;
using UnityEngine;
using System;
using StressLevelZero.Rig;

namespace Sst.Features {
class MuseumBhopTimer : Feature {
  public DateTime MuseumStartTime;
  public RigManager RigManager;
  public TriggerLasers Trigger;

  public MuseumBhopTimer() { IsDev = IsEnabledByDefault = true; }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    if (sceneName != "scene_museum")
      return;

    MuseumStartTime = DateTime.Now;
    Utils.LogDebug("Museum B-hop timer started");

    RigManager = Utilities.Boneworks.GetRigManager();
    Trigger =
        GameObject.Find("ROOM_HALLOFLOCOMOTION/ROOM_JUMP/LASER/col_capsule")
            .GetComponent<TriggerLasers>();

    // It's annoying when you miss the trigger...
    // Trigger.OnTriggerEnterEvent.AddListener(
    //     new Action<Collider>(OnTriggerEnter));
  }

  // public void OnTriggerEnter(Collider collider) {
  //   var elapsed = DateTime.Now - MuseumStartTime;
  //   MelonLogger.Msg($"Museum laser reached in: {elapsed}");
  // }

  public override void OnUpdate() {
    if (RigManager == null || Trigger == null)
      return;

    if (RigManager.physicsRig.m_head.position.z < Trigger.transform.position.z)
      return;

    RigManager = null;
    Trigger = null;

    var elapsed = DateTime.Now - MuseumStartTime;
    MelonLogger.Msg($"Museum laser reached in: {elapsed}");
  }
}
}
