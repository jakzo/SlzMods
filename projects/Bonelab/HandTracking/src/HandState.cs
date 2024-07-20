using System;
using MelonLoader;
using UnityEngine;

namespace Sst.HandTracking;

public class HandState {
  private static Vector3 FromFlippedXVector3f(OVRPlugin.Vector3f vector) =>
      new Vector3(-vector.x, vector.y, vector.z);

  private static Vector3 FromFlippedZVector3f(OVRPlugin.Vector3f vector) =>
      new Vector3(vector.x, vector.y, -vector.z);

  private static Quaternion FromFlippedXQuatf(OVRPlugin.Quatf quat) =>
      new Quaternion(quat.x, -quat.y, -quat.z, quat.w);

  private static Quaternion FromFlippedZQuatf(OVRPlugin.Quatf quat) =>
      new Quaternion(-quat.x, -quat.y, quat.z, quat.w);

  public bool IsLeft;
  public Vector3 Position;
  public Quaternion Rotation;
  public float Scale;
  public JointTransform[] Joints =
      new JointTransform[(int)OVRPlugin.SkeletonConstants.MaxHandBones];
  public OVRPlugin.TrackingConfidence HandConfidence;
  public OVRPlugin.TrackingConfidence[] FingerConfidences;
  public bool IsPinching;

  private OVRPlugin.Hand _hand;
  private OVRPlugin.SkeletonType _skeletonType;
  private OVRInput.Controller _controller;
  private OVRPlugin.HandState _state = new();
  private OVRPlugin.Skeleton2 _skeleton = new();

  public HandState(bool isLeft) {
    IsLeft = isLeft;
    _controller =
        isLeft ? OVRInput.Controller.LHand : OVRInput.Controller.RHand;

    _skeletonType = isLeft ? OVRPlugin.SkeletonType.HandLeft
                           : OVRPlugin.SkeletonType.HandRight;
    if (!OVRPlugin.GetSkeleton2(_skeletonType, _skeleton)) {
      throw new Exception("Failed to get hand skeleton");
    }

    _hand = isLeft ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
    Update();
  }

  public void Update() {
    if (!OVRPlugin.GetHandState(OVRPlugin.Step.Render, _hand, _state)) {
      throw new Exception("Failed to get hand state");
    }

    Position = FromFlippedZVector3f(_state.RootPose.Position);
    Rotation = FromFlippedZQuatf(_state.RootPose.Orientation);
    Scale = _state.HandScale;

    for (var i = 0; i < Joints.Length; i++) {
      var localRot = FromFlippedXQuatf(_state.BoneRotations[i]);
      var parentIdx = _skeleton.Bones[i].ParentBoneIndex;
      var parentJoint =
          OVRPlugin.IsValidBone((OVRPlugin.BoneId)parentIdx, _skeletonType)
              // parentIdx will always be less than i so Joints[parentIdx] will
              // already be updated
              ? Joints[parentIdx]
              : JointTransform.IDENTITY;
      var handRot = parentJoint.HandRotation * localRot;
      var localPos =
          handRot * FromFlippedXVector3f(_skeleton.Bones[i].Pose.Position);
      var handPos = parentJoint.HandPosition + localPos;

      Joints[i] = new JointTransform() {
        LocalPosition = localPos,
        LocalRotation = localRot,
        HandPosition = handPos,
        HandRotation = handRot,
      };
    }

    HandConfidence = _state.HandConfidence;
    FingerConfidences = _state.FingerConfidences;

    IsPinching = (_state.Pinches & OVRPlugin.HandFingerPinch.Index) != 0;
  }

  public bool IsActive() =>
      // NOTE: Requires OVRInput.Update() to be called (game already does this)
      OVRInput.IsControllerConnected(_controller);

  public bool IsTracked() => (_state.Status &
                              OVRPlugin.HandStatus.HandTracked) != 0;
}

public struct JointTransform {
  public static JointTransform IDENTITY;

  public Vector3 LocalPosition;
  public Quaternion LocalRotation;
  public Vector3 HandPosition;
  public Quaternion HandRotation;
}
