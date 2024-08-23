using System;
using UnityEngine;
using SLZ.VRMK;
using HarmonyLib;
using SLZ.Rig;

namespace Sst.HandTracking;

public class HandPose {
  private static OVRPlugin.BoneId[] FINGER_JOINTS_THUMB = {
    OVRPlugin.BoneId.Hand_Thumb0,
    OVRPlugin.BoneId.Hand_Thumb1,
    OVRPlugin.BoneId.Hand_Thumb2,
    OVRPlugin.BoneId.Hand_Thumb3,
  };
  private static OVRPlugin.BoneId[] FINGER_JOINTS_INDEX = {
    OVRPlugin.BoneId.Hand_Index1,
    OVRPlugin.BoneId.Hand_Index2,
    OVRPlugin.BoneId.Hand_Index3,
  };
  private static OVRPlugin.BoneId[] FINGER_JOINTS_MIDDLE = {
    OVRPlugin.BoneId.Hand_Middle1,
    OVRPlugin.BoneId.Hand_Middle2,
    OVRPlugin.BoneId.Hand_Middle3,
  };
  private static OVRPlugin.BoneId[] FINGER_JOINTS_RING = {
    OVRPlugin.BoneId.Hand_Ring1,
    OVRPlugin.BoneId.Hand_Ring2,
    OVRPlugin.BoneId.Hand_Ring3,
  };
  private static OVRPlugin.BoneId[] FINGER_JOINTS_PINKY = {
    OVRPlugin.BoneId.Hand_Pinky1,
    OVRPlugin.BoneId.Hand_Pinky2,
    OVRPlugin.BoneId.Hand_Pinky3,
  };

  private static SLZ.Data.HandPose.PoseData BASE_HAND_TRACKING_POSE = new() {
    thumb1 = Quaternion.Euler(54.7015f - 45f, 80.8802f - 80f, 36.1174f - 30f),
    thumb2 = 0.9604f,
    thumb3 = -0.7674f,
    index1 = Quaternion.Euler(359.7946f, 10.2376f, 4.6807f),
    index2 = -0.045f,
    index3 = -0.2698f,
    middle1 = Quaternion.Euler(355.5994f, 359.7487f + 8f, 0.4831f),
    middle2 = 0.1587f,
    middle3 = -0.0703f,
    ring1 = Quaternion.Euler(356.0975f, 355.5379f + 8f, 6.0902f),
    ring2 = -0.064f,
    ring3 = -0.064f,
    pinky1 = Quaternion.Euler(355.7065f - 15f, 345.3653f - 5f, 10.7265f),
    pinky2 = 0f,
    pinky3 = 0f,
  };

  public float[] FingerCurls = new float[(int)OVRPlugin.HandFinger.Max];

  private HandTracker _tracker;

  public HandPose(HandTracker tracker) { _tracker = tracker; }

  public void UpdateFingerCurls() {
    FingerCurls[(int)OVRPlugin.HandFinger.Thumb] =
        CalculateFingerCurl(FINGER_JOINTS_THUMB, 100f);
    FingerCurls[(int)OVRPlugin.HandFinger.Index] =
        CalculateFingerCurl(FINGER_JOINTS_INDEX, 200f);
    FingerCurls[(int)OVRPlugin.HandFinger.Middle] =
        CalculateFingerCurl(FINGER_JOINTS_MIDDLE, 200f);
    FingerCurls[(int)OVRPlugin.HandFinger.Ring] =
        CalculateFingerCurl(FINGER_JOINTS_RING, 200f);
    FingerCurls[(int)OVRPlugin.HandFinger.Pinky] =
        CalculateFingerCurl(FINGER_JOINTS_PINKY, 200f);

    _tracker.ProxyController.ThumbFinger =
        FingerCurls[(int)OVRPlugin.HandFinger.Thumb];
    _tracker.ProxyController.IndexFinger =
        FingerCurls[(int)OVRPlugin.HandFinger.Index];
    _tracker.ProxyController.MiddleFinger =
        FingerCurls[(int)OVRPlugin.HandFinger.Middle];
    _tracker.ProxyController.RingFinger =
        FingerCurls[(int)OVRPlugin.HandFinger.Ring];
    _tracker.ProxyController.PinkyFinger =
        FingerCurls[(int)OVRPlugin.HandFinger.Pinky];
  }

  private float
  CalculateFingerCurl(OVRPlugin.BoneId[] fingerJoints, float maxRotation) {
    var totalRotation = 0f;
    foreach (var joint in fingerJoints) {
      var rot =
          _tracker.HandState.Joints[(int)joint].LocalRotation.eulerAngles.z;
      totalRotation += 180f - (360f + 180f - rot) % 360f;
    }
    return MapToFingerCurve(Mathf.Clamp01(totalRotation / maxRotation));
  }

  // Correct for Bonelab controller finger not curling linearly
  private float MapToFingerCurve(float linearCurl) {
    var mapping = new(float, float)[] {
      (0.0f, 0.00f), (0.1f, 0.10f), (0.2f, 0.13f), (0.3f, 0.20f),
      (0.4f, 0.32f), (0.5f, 0.38f), (0.6f, 0.42f), (0.7f, 0.52f),
      (0.8f, 0.68f), (0.9f, 0.84f), (1.0f, 1.00f),
    };

    for (int i = 0; i < mapping.Length - 1; i++) {
      var inputMin = mapping[i].Item1;
      var inputMax = mapping[i + 1].Item1;
      if (linearCurl >= inputMin && linearCurl <= inputMax) {
        var t = (linearCurl - inputMin) / (inputMax - inputMin);
        var outputMin = mapping[i].Item2;
        var outputMax = mapping[i + 1].Item2;
        return outputMin + t * (outputMax - outputMin);
      }
    }

    return linearCurl;
  }

  [HarmonyPatch(typeof(OpenController), nameof(OpenController.ProcessFingers))]
  internal static class OpenController_ProcessFingers {
    [HarmonyPrefix]
    private static bool Prefix(OpenController __instance) {
      var tracker = Mod.Instance.GetTrackerFromProxyController(
          Utils.XrControllerOf(__instance)
      );
      if (tracker == null || !tracker.IsTracking)
        return true;

      // Skip processing our curl values because they are already good
      __instance._processedThumb = tracker.ProxyController.ThumbFinger;
      __instance._processedIndex = tracker.ProxyController.IndexFinger;
      __instance._processedMiddle = tracker.ProxyController.MiddleFinger;
      __instance._processedRing = tracker.ProxyController.RingFinger;
      __instance._processedPinky = tracker.ProxyController.PinkyFinger;
      return false;
    }
  }

  // Animates the Bonelab hand pose to match the hand tracking pose
  // Specifically matches joint rotations, so differences between the size and
  // shape of the user's hand and the avatar's hand may cause the pose to
  // visually appear different (eg. touching your real world thumb and pinky
  // tips may not cause the tips to touch in the game due to differences in hand
  // width, finger length, etc.)
  public void UpdateHandPose(HandPoseAnimator animator) {
    var hand = _tracker.GetPhysicalHand();
    if (!_tracker.IsTracking || hand.AttachedReceiver != null)
      return;

    var openPose = hand.Animator._openPose;

    // TODO: How do we know when the hand is in the open pose?
    // TODO: Can we have it smoothly transition from hand tracked joints to
    // other poses?
    hand.Animator._currentPoseData = new SLZ.Data.HandPose.PoseData() {
      // Some clues to Oculus' rationale behind the bones they track:
      // https://github.com/immersive-web/webxr-hand-input/issues/1#issuecomment-575810282
      thumb1 = FingerJointRotation(
          BASE_HAND_TRACKING_POSE.thumb1, OVRPlugin.BoneId.Hand_ForearmStub,
          OVRPlugin.BoneId.Hand_Thumb0, OVRPlugin.BoneId.Hand_Thumb1
      ),
      thumb2 = FingerJointAngle(
          BASE_HAND_TRACKING_POSE.thumb2, OVRPlugin.BoneId.Hand_Thumb2
      ),
      thumb3 = FingerJointAngle(
          BASE_HAND_TRACKING_POSE.thumb3, OVRPlugin.BoneId.Hand_Thumb3
      ),
      index1 = FingerJointRotation(
          BASE_HAND_TRACKING_POSE.index1, OVRPlugin.BoneId.Hand_Index1
      ),
      index2 = FingerJointAngle(
          BASE_HAND_TRACKING_POSE.index2, OVRPlugin.BoneId.Hand_Index2
      ),
      index3 = FingerJointAngle(
          BASE_HAND_TRACKING_POSE.index3, OVRPlugin.BoneId.Hand_Index3
      ),
      middle1 = FingerJointRotation(
          BASE_HAND_TRACKING_POSE.middle1, OVRPlugin.BoneId.Hand_Middle1
      ),
      middle2 = FingerJointAngle(
          BASE_HAND_TRACKING_POSE.middle2, OVRPlugin.BoneId.Hand_Middle2
      ),
      middle3 = FingerJointAngle(
          BASE_HAND_TRACKING_POSE.middle3, OVRPlugin.BoneId.Hand_Middle3
      ),
      ring1 = FingerJointRotation(
          BASE_HAND_TRACKING_POSE.ring1, OVRPlugin.BoneId.Hand_Ring1
      ),
      ring2 = FingerJointAngle(
          BASE_HAND_TRACKING_POSE.ring2, OVRPlugin.BoneId.Hand_Ring2
      ),
      ring3 = FingerJointAngle(
          BASE_HAND_TRACKING_POSE.ring3, OVRPlugin.BoneId.Hand_Ring3
      ),
      pinky1 = FingerJointRotation(
          // OVRPlugin.BoneId.Hand_Pinky0 exists but appears to never rotate
          BASE_HAND_TRACKING_POSE.pinky1, OVRPlugin.BoneId.Hand_Pinky1
      ),
      pinky2 = FingerJointAngle(
          BASE_HAND_TRACKING_POSE.pinky2, OVRPlugin.BoneId.Hand_Pinky2
      ),
      pinky3 = FingerJointAngle(
          BASE_HAND_TRACKING_POSE.pinky3, OVRPlugin.BoneId.Hand_Pinky3
      ),
    };
  }

  private Quaternion FingerJointRotation(
      Quaternion baseRotation, params OVRPlugin.BoneId[] boneIds
  ) {
    var result = baseRotation;
    foreach (var boneId in boneIds) {
      result *=
          Utils.FlipXY(_tracker.HandState.Joints[(int)boneId].LocalRotation);
    }
    return result;
  }

  private float FingerJointAngle(
      float baseAngleDegrees, OVRPlugin.BoneId boneId
  ) => baseAngleDegrees +
      _tracker.HandState.Joints[(int)boneId].LocalRotation.eulerAngles.z;

  [HarmonyPatch(
      typeof(HandPoseAnimator), nameof(HandPoseAnimator.ApplyPoseToTransforms)
  )]
  internal static class HandPoseAnimator_ApplyPoseToTransforms {
    [HarmonyPrefix]
    private static void Prefix(HandPoseAnimator __instance) {
      var tracker = Mod.Instance.GetTrackerOfHand(__instance.handedness);
      if (!(tracker?.IsControllerConnected() ?? true))
        tracker.HandPose.UpdateHandPose(__instance);
    }
  }
}
