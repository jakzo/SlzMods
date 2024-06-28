using System;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Sst.Utilities;

namespace Sst.HandTracking;

public class Mod : MelonMod {
  public static (OVRInput.Controller, Color)[] HANDS = {
    (OVRInput.Controller.LHand, Color.red),
    (OVRInput.Controller.RHand, Color.blue),
  };

  public static Mod Instance;

  public (OVRInput.Controller, GameObject)[] Visualizations = null;

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);
    Instance = this;

    // UnityEngine.XR.Hand.Hand_TryGetFingerBonesAsList(
    //     1, UnityEngine.XR.HandFinger.Index, out var rootBone);
  }

  public override void OnUpdate() {
    var parent = LevelHooks.RigManager?.ControllerRig.transform ??
                 LevelHooks.BasicTrackingRig?.transform;
    if (!parent)
      return;

    if (!OVRInput.IsControllerConnected(OVRInput.Controller.Hands)) {
      if (Visualizations != null) {
        foreach (var (hand, obj) in Visualizations) {
          if (obj)
            GameObject.Destroy(obj);
        }
        Visualizations = null;
        MelonLogger.Msg("Hand tracking is now inactive");
      }
      return;
    }

    if (Visualizations == null) {
      Visualizations = CreateVisualizations(parent);
      MelonLogger.Msg("Hand tracking is now active");
    }

    UpdateHandPositions();
  }

  private (OVRInput.Controller,
           GameObject)[] CreateVisualizations(Transform parent) =>
      HANDS
          .Select(hand => {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.transform.SetParent(parent);
            obj.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            obj.GetComponent<Renderer>().material.color = hand.Item2;
            return (hand.Item1, obj);
          })
          .ToArray();

  private void UpdateHandPositions() {
    foreach (var (hand, obj) in Visualizations) {
      obj.transform.position = OVRInput.GetLocalControllerPosition(hand);
    }
  }
}
