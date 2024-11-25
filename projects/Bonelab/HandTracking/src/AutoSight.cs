using System;
using System.Collections.Generic;
using UnityEngine;
using Sst.Utilities;
using HarmonyLib;

using SLZ.Marrow;
using SLZ.Interaction;
using SLZ.Rig;
using SLZ.Bonelab;
using MelonLoader;
using UnityEngine.XR;

#if PATCH5
using Hand = SLZ.Marrow.Hand;
#elif PATCH4
using Hand = SLZ.Interaction.Hand;
#endif

namespace Sst.HandTracking;

public class AutoSight {
  private const float ROTATION_SIMILARITY_ACTIVATION_THRESHOLD = 0.998f;
  private const float MIN_OFFSET_DELTA = 0.005f;
  private const float MIN_OFFSET_DELTA_SQR =
      MIN_OFFSET_DELTA * MIN_OFFSET_DELTA;
  private const float ROTATION_SIMILARITY_DEACTIVATION_THRESHOLD = 0.995f;
  private const float TRANSITION_SPEED = 2f;
  private const float ROTATION_FACTOR = 0.2f;
  private const float POSITION_MAX_SPEED = 0.05f;

  private const float STILLNESS_POSITION_THRESHOLD = 0.04f;
  private const float STILLNESS_POSITION_THRESHOLD_SQR =
      STILLNESS_POSITION_THRESHOLD * STILLNESS_POSITION_THRESHOLD;
  private const float STILLNESS_ROTATION_THRESHOLD = 0.15f;
  private const float STILLNESS_ROTATION_THRESHOLD_SQR =
      STILLNESS_ROTATION_THRESHOLD * STILLNESS_ROTATION_THRESHOLD;

  private const float OFFSET_UPDATE_RATE_POS = 1f;

  // Sights have a slightly different offset depending on the gun but finding
  // the specific value per gun is a lot of manual effort and won't work for
  // modded guns whereas hardcoding it works well enough
  private static Vector3 SIGHT_OFFSET = new Vector3(0f, 0.02f, 0f);
  private static Vector3 OFFHAND_OFFSET_LEFT = new Vector3(-0.1f, 0f, 0f);
  private static Vector3 OFFHAND_OFFSET_RIGHT = new Vector3(
      -OFFHAND_OFFSET_LEFT.x, OFFHAND_OFFSET_LEFT.y, OFFHAND_OFFSET_LEFT.z
  );
  private static XRNode[] XR_EYES = { XRNode.LeftEye, XRNode.RightEye };

  public HandTracker Tracker;
  public HandTracker OtherTracker;
  public HandReciever AttachedReceiver;
  public InteractableHost Host;
  public Transform Sight;
  public bool IsGunHeld;
  public bool IsHoldingWithBothHands;
  public bool IsActive;
  public float RotationFactor = 1f;
  public Vector3 ActualVirtualHandPos;
  public Quaternion ActualVirtualHandRot;
  public Quaternion TargetHandRotation;
  public Vector3 DefaultOffsetPos;
  public Quaternion DefaultOffsetRot;
  public Vector3 ObservedOffsetDelta;
  public Vector3 ObservedOffsetPos;
  public Quaternion ObservedOffsetRot;

  public AutoSight(HandTracker tracker, HandTracker otherTracker) {
    Tracker = tracker;
    OtherTracker = otherTracker;
  }

  public void UpdateHand() {
    var physicalHand = Tracker.GetPhysicalHand();
    if (physicalHand == null) {
      RotationFactor = 1f;
      return;
    }

    var hasHeldItemChanged =
        physicalHand.AttachedReceiver != AttachedReceiver &&
        (physicalHand.AttachedReceiver == null || physicalHand.joint != null);
    if (hasHeldItemChanged) {
      AttachedReceiver = physicalHand.AttachedReceiver;
      Host = AttachedReceiver?.TryCast<TargetGrip>()
                 ?.Host?.TryCast<InteractableHost>();
      IsGunHeld = TryAddSightTransform();

      if (IsGunHeld) {
        ObservedOffsetPos = DefaultOffsetPos =
            CalculateDefaultOffsetPos(physicalHand);
        ObservedOffsetRot = DefaultOffsetRot =
            CalculateDefaultOffsetRot(physicalHand);
      }
    }

    if (!IsGunHeld) {
      RotationFactor = 1f;
      return;
    }

    var virtualRig = LevelHooks.RigManager.virtualHeptaRig;
    var virtualHand =
        Tracker.Opts.isLeft ? virtualRig.m_handLf : virtualRig.m_handRt;

    ActualVirtualHandPos = virtualHand.position;
    ActualVirtualHandRot = virtualHand.rotation;

    // TODO: Dampen offhand position
    // TODO: This doesn't affect this physical hand's target position at all
    // var otherHandHost =
    //     OtherTracker?.GetPhysicalHand()
    //         ?.AttachedReceiver?.Host?.TryCast<InteractableHost>();
    // IsHoldingWithBothHands = otherHandHost != null &&
    //     Host.GetInstanceID() == otherHandHost.GetInstanceID();
    // if (IsHoldingWithBothHands) {
    //   if (OtherTracker.Opts.isLeft) {
    //     virtualRig.m_handLf.localPosition += OFFHAND_OFFSET_LEFT;
    //   } else {
    //     virtualRig.m_handRt.localPosition += OFFHAND_OFFSET_RIGHT;
    //   }
    // }

    UpdateObservedOffset(physicalHand);

    var controllerRig =
        LevelHooks.RigManager.controllerRig.TryCast<OpenControllerRig>();
    if (controllerRig == null) {
      RotationFactor = 1f;
      return;
    }

    var virtualSightPos =
        ActualVirtualHandPos + ActualVirtualHandRot * ObservedOffsetPos;

    var rotationSimilarity =
        GetMaxRotationSimilarity(controllerRig, virtualSightPos);

    if (IsActive) {
      if (rotationSimilarity < ROTATION_SIMILARITY_DEACTIVATION_THRESHOLD) {
        Tracker.Log("Auto-sight deactivated");
        IsActive = false;
      }
    } else {
      if (rotationSimilarity >= ROTATION_SIMILARITY_ACTIVATION_THRESHOLD &&
          ObservedOffsetDelta.sqrMagnitude <= MIN_OFFSET_DELTA_SQR) {
        Tracker.Log("Auto-sight activated");
        IsActive = true;
      }
    }

    var shouldUpdateRotation = IsActive;
    var targetRotationFactor = IsActive ? ROTATION_FACTOR : 1f;
    if (RotationFactor != targetRotationFactor) {
      RotationFactor = Mathf.MoveTowards(
          RotationFactor, targetRotationFactor,
          TRANSITION_SPEED * Time.deltaTime
      );
      shouldUpdateRotation = true;
    }

    if (shouldUpdateRotation) {
      TargetHandRotation = CalculateTargetHandRotation(
          controllerRig.headset.position, virtualSightPos
      );

      virtualHand.rotation = Quaternion.Slerp(
          TargetHandRotation, ActualVirtualHandRot, RotationFactor
      );
    }
  }

  public Vector3 CalculateDefaultOffsetPos(Hand hand) =>
      // TODO: This is a little bit off for some reason (depends on gun)
      hand.jointStartRotation *
      (Quaternion.Inverse(Host.Rb.rotation) *
           (Sight.position - Host.Rb.position) -
       hand.joint.connectedAnchor + hand.joint.anchor);

  public Quaternion CalculateDefaultOffsetRot(Hand hand) => Sight.rotation
      * Quaternion.Inverse(Host.Rb.rotation) * hand.jointStartRotation;

  public bool TryAddSightTransform() {
    var gun = Host?.GetComponent<Gun>();
    if (Host?.Rb == null || gun?.firePointTransform == null ||
        !AttachedReceiver.Equals(gun.triggerGrip))
      return false;

    if (Sight == null) {
      Sight = new GameObject("HandTracking_Sight").transform;
      Sight.SetLocalPositionAndRotation(SIGHT_OFFSET, Quaternion.identity);
    }

    Sight.SetParent(gun.firePointTransform, false);
    return true;
  }

  public void UpdateObservedOffset(Hand physicalHand) {
    var stillness =
        Mathf.Min(StillnessOf(Host.Rb), StillnessOf(physicalHand.rb));

    var handToSightPos = Quaternion.Inverse(ActualVirtualHandRot) *
        (Sight.position - ActualVirtualHandPos);
    ObservedOffsetDelta = handToSightPos - ObservedOffsetPos;
    ObservedOffsetPos += Vector3.ClampMagnitude(
        ObservedOffsetDelta, OFFSET_UPDATE_RATE_POS * Time.deltaTime * stillness
    );

    var handToSightRot =
        Quaternion.Inverse(ActualVirtualHandRot) * Sight.rotation;
    ObservedOffsetRot = handToSightRot;
  }

  public float GetMaxRotationSimilarity(
      OpenControllerRig controllerRig, Vector3 virtualSightPos
  ) {
    var maxRotationSimilarity = 0f;
    for (var i = 0; i < XR_EYES.Length; i++) {
      var eyePos = controllerRig.vrRoot.TransformPoint(
          InputTracking.GetLocalPosition(XR_EYES[i])
      );
      var targetHandRot = CalculateTargetHandRotation(eyePos, virtualSightPos);
      var rotationSimilarity =
          Quaternion.Dot(targetHandRot, ActualVirtualHandRot);
      if (rotationSimilarity > maxRotationSimilarity)
        maxRotationSimilarity = rotationSimilarity;
    }
    return maxRotationSimilarity;
  }

  public Quaternion CalculateTargetHandRotation(
      Vector3 eyePosition, Vector3 virtualSightPosition
  ) {
    var targetSightRotation =
        Quaternion.LookRotation(virtualSightPosition - eyePosition);
    return targetSightRotation * Quaternion.Inverse(ObservedOffsetRot);
  }

  public static float StillnessOf(Rigidbody rb) => Mathf.Min(
      Mathf.Min(
          STILLNESS_POSITION_THRESHOLD_SQR / rb.velocity.sqrMagnitude,
          STILLNESS_ROTATION_THRESHOLD_SQR / rb.angularVelocity.sqrMagnitude
      ),
      1f
  );

  [HarmonyPatch(
      typeof(GameWorldSkeletonRig), nameof(GameWorldSkeletonRig.OnFixedUpdate)
  )]
  internal static class RemapRig_OnFixedUpdate {
    [HarmonyPostfix]
    private static void Postfix(GameWorldSkeletonRig __instance) {
      if (!__instance.Equals(LevelHooks.RigManager?.virtualHeptaRig))
        return;

      if (Mod.Instance.TrackerLeft?.IsTracking ?? false)
        Mod.Instance.AutoSightLeft?.UpdateHand();
      if (Mod.Instance.TrackerRight?.IsTracking ?? false)
        Mod.Instance.AutoSightRight?.UpdateHand();
    }
  }
}
