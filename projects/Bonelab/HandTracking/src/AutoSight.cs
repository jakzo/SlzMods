using System;
using System.Collections.Generic;
using UnityEngine;
using SLZ.Bonelab;
using SLZ.Interaction;
using Sst.Utilities;
using SLZ.Rig;
using SLZ.Marrow;
using HarmonyLib;
using SLZ.VRMK;

namespace Sst.HandTracking;

public class AutoSight {
  private const float ROTATION_SIMILARITY_ACTIVATION_THRESHOLD = 0.95f;
  private const float ROTATION_SIMILARITY_DEACTIVATION_THRESHOLD = 0.9f;
  private const float ROTATION_FACTOR = 0.25f;
  private const float POSITION_DAMPING_FACTOR = 0.25f;
  // Sights have a slightly different offset depending on the gun but finding
  // the specific value per gun is a lot of manual effort and won't work for
  // modded guns whereas hardcoding it works well enough
  private static Vector3 SIGHT_OFFSET = new Vector3(0f, 0.03f, 0f);

  public HandTracker Tracker;
  public HandTracker OtherTracker;
  public InteractableHost Weapon;
  public bool IsActive;
  public Quaternion TargetHandRotation;
  public Vector3 DampedRemapHandPos;

  public AutoSight(HandTracker tracker, HandTracker otherTracker) {
    Tracker = tracker;
    OtherTracker = otherTracker;
  }

  public void UpdateHand() {
    // TODO: More efficient way to get held gun and cache all this stuff?
    var hand = Tracker.GetPhysicalHand();
    if (hand == null)
      return;

    var host = hand?.AttachedReceiver?.TryCast<TargetGrip>()
                   ?.Host?.TryCast<InteractableHost>();
    var gun = host?.GetComponent<Gun>();
    if (gun?.firePointTransform == null || host?.Rb == null)
      return;

    var eye = LevelHooks.RigManager.controllerRig.TryCast<OpenControllerRig>()
                  ?.cameras?[0];
    if (eye == null)
      return;

    var sightRot = gun.firePointTransform.rotation;
    var sightPos = gun.firePointTransform.position + sightRot * SIGHT_OFFSET;

    var handToSightPos = sightPos - host.Rb.transform.position -
        hand.joint.connectedAnchor + hand.joint.anchor;
    var sightToHandPos = -handToSightPos;
    var sightToHandRot =
        sightRot * host.Rb.transform.rotation * hand.joint.targetRotation;
    var handToSightRot = Quaternion.Inverse(sightToHandRot);

    var sightPosOfHand = hand.transform.position + handToSightPos;
    var sightRotOfHand = hand.transform.rotation * handToSightRot;

    var remapRig = LevelHooks.RigManager.remapHeptaRig;
    var remapHand = Tracker.Opts.isLeft ? remapRig.m_handLf : remapRig.m_handRt;
    var remapHandPos = remapHand.position;
    var remapHandRot = remapHand.rotation;

    var targetSightRotation =
        Quaternion.LookRotation(sightPosOfHand - eye.transform.position);
    TargetHandRotation = targetSightRotation * sightToHandRot;

    var rotationSimilarity = Quaternion.Dot(TargetHandRotation, remapHandRot);

    if (IsActive) {
      if (rotationSimilarity < ROTATION_SIMILARITY_DEACTIVATION_THRESHOLD) {
        Tracker.Log("Auto-sight deactivated");
        IsActive = false;
        return;
      }
    } else if (rotationSimilarity >= ROTATION_SIMILARITY_ACTIVATION_THRESHOLD) {
      Tracker.Log("Auto-sight activated");
      IsActive = true;
      DampedRemapHandPos = remapHand.localPosition;
    } else {
      return;
    }

    remapHand.rotation = TargetHandRotation;

    // TODO: Scale up damping factor when delta from last position increases
    var remapHandPosDelta = remapHand.localPosition - DampedRemapHandPos;
    DampedRemapHandPos += remapHandPosDelta * POSITION_DAMPING_FACTOR;
    remapHand.localPosition = DampedRemapHandPos;
  }

  [HarmonyPatch(typeof(RemapRig), nameof(RemapRig.OnEarlyUpdate))]
  internal static class RemapRig_OnEarlyUpdate {
    [HarmonyPostfix]
    private static void Postfix(RemapRig __instance) {
      if (!__instance.Equals(LevelHooks.RigManager?.remapHeptaRig))
        return;
      // Mod.Instance.TrackerLeft?.AutoSight.UpdateHand();
      // Mod.Instance.TrackerRight?.AutoSight.UpdateHand();
    }
  }

  // TODO: Do we still need this?
  // [HarmonyPatch(
  //     typeof(GameWorldSkeletonRig),
  //     nameof(GameWorldSkeletonRig.OnFixedUpdate)
  // )]
  // internal static class GameWorldSkeletonRig_OnFixedUpdate {
  //   // [HarmonyPrefix]
  //   // private static bool Prefix(GameWorldSkeletonRig __instance) {
  //   [HarmonyPostfix]
  //   private static void Postfix(GameWorldSkeletonRig __instance) {
  //     __instance.m_handLf.localPosition =
  //         new Vector3(Mathf.Sin(Time.time) * 0.25f, 1.5f, 0.2f);
  //     __instance.m_handLf.localRotation = Quaternion.identity;
  //     // __instance.m_elbowLf.localPosition = new Vector3(0.2f, 1.2f, 0.2f);
  //     // __instance.m_elbowLf.localRotation = Quaternion.identity;
  //     // __instance.bodyPose = __instance._basePose;
  //     // return false;
  //   }
  // }

  // [HarmonyPatch(typeof(PhysHand), nameof(PhysHand.UpdateArmTargets))]
  // internal static class PhysHand_UpdateArmTargets {
  //   [HarmonyPrefix]
  //   private static bool Prefix(PhysHand __instance) {
  //     var tracker =
  //     Mod.Instance.GetTrackerOfHand(__instance.hand.handedness);
  //     tracker.AutoSight.UpdateHand();
  //     if (!tracker.AutoSight.IsActive)
  //       return;

  //     tracker.LogSpam("maxTor", maxTor);
  //     var delta = Quaternion.Inverse(targetRotation) *
  //         tracker.AutoSight.TargetHandRotation;

  //     targetRotation = tracker.AutoSight.TargetHandRotation *
  //         Quaternion.Slerp(Quaternion.identity, delta, ROTATION_FACTOR);
  //   }
  // }
}
