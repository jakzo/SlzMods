using System;
using System.Collections.Generic;
using UnityEngine;
using Sst.Utilities;
using HarmonyLib;

using SLZ.Marrow;
using SLZ.Interaction;
using SLZ.Rig;
using SLZ.Bonelab;

namespace Sst.HandTracking;

public class AutoSight {
  private const float ROTATION_SIMILARITY_ACTIVATION_THRESHOLD = 0.98f;
  private const float ROTATION_SIMILARITY_DEACTIVATION_THRESHOLD = 0.96f;
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
  public Vector3 DampedVirtualHandPos;

  public AutoSight(HandTracker tracker, HandTracker otherTracker) {
    Tracker = tracker;
    OtherTracker = otherTracker;
  }

  public void UpdateHand() {
    var result = CalculateHandToSightOffset();
    if (!result.HasValue)
      return;
    var handToSight = result.Value;

    // TODO: Do for both eyes
    var eye = LevelHooks.RigManager.controllerRig.TryCast<OpenControllerRig>()
                  ?.cameras?[0];
    if (eye == null)
      return;

    var virtualRig = LevelHooks.RigManager.virtualHeptaRig;
    var virtualHand =
        Tracker.Opts.isLeft ? virtualRig.m_handLf : virtualRig.m_handRt;
    var virtualHandPos = virtualHand.position;
    var virtualHandRot = virtualHand.rotation;

    var sightPosOfHand =
        virtualHand.position + virtualHand.rotation * handToSight.Pos;
    var sightRotOfHand = virtualHand.rotation * handToSight.Rot;

    var targetSightRotation =
        Quaternion.LookRotation(sightPosOfHand - eye.transform.position);
    TargetHandRotation =
        targetSightRotation * Quaternion.Inverse(handToSight.Rot);

    var rotationSimilarity = Quaternion.Dot(TargetHandRotation, virtualHandRot);

    if (IsActive) {
      if (rotationSimilarity < ROTATION_SIMILARITY_DEACTIVATION_THRESHOLD) {
        Tracker.Log("Auto-sight deactivated");
        IsActive = false;
        return;
      }
    } else if (rotationSimilarity >= ROTATION_SIMILARITY_ACTIVATION_THRESHOLD) {
      Tracker.Log("Auto-sight activated");
      IsActive = true;
      DampedVirtualHandPos = virtualHand.localPosition;
    } else {
      return;
    }

    virtualHand.rotation = TargetHandRotation;

    // TODO: Scale up damping factor when delta from last position increases
    var virtualHandPosDelta = virtualHand.localPosition - DampedVirtualHandPos;
    DampedVirtualHandPos += virtualHandPosDelta * POSITION_DAMPING_FACTOR;
    virtualHand.localPosition = DampedVirtualHandPos;
  }

  public (Vector3 Pos, Quaternion Rot)? CalculateHandToSightOffset() {
    // TODO: More efficient way to get held gun and cache all this stuff?
    var physicalHand = Tracker.GetPhysicalHand();
    if (physicalHand == null)
      return null;

    var host = physicalHand?.AttachedReceiver?.TryCast<TargetGrip>()
                   ?.Host?.TryCast<InteractableHost>();

    var result = GetSight(host);
    if (!result.HasValue)
      return null;
    var sight = result.Value;

    // TODO: This is a little bit off for some reason (depends on gun)
    var handToSightPos = physicalHand.jointStartRotation *
        (Quaternion.Inverse(host.Rb.rotation) * (sight.Pos - host.Rb.position) -
         physicalHand.joint.connectedAnchor + physicalHand.joint.anchor);
    var handToSightRot = sight.Rot * Quaternion.Inverse(host.Rb.rotation) *
        physicalHand.jointStartRotation;

    return (handToSightPos, handToSightRot);
  }

  public (Vector3 Pos, Quaternion Rot)? GetSight(InteractableHost host) {
    var gun = host?.GetComponent<Gun>();
    if (gun?.firePointTransform == null || host?.Rb == null)
      return null;

    var sightRot = gun.firePointTransform.rotation;
    var sightPos = gun.firePointTransform.position + sightRot * SIGHT_OFFSET;

    return (sightPos, sightRot);
  }

  [HarmonyPatch(
      typeof(GameWorldSkeletonRig), nameof(GameWorldSkeletonRig.OnFixedUpdate)
  )]
  internal static class RemapRig_OnFixedUpdate {
    [HarmonyPostfix]
    private static void Postfix(GameWorldSkeletonRig __instance) {
      if (!__instance.Equals(LevelHooks.RigManager?.virtualHeptaRig))
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
