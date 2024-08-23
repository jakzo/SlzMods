using System;
using UnityEngine;

namespace Sst.HandTracking;

public class HandState {
  public bool IsLeft;
  public Vector3 Position;
  public Quaternion Rotation;
  public float Scale;
  public JointTransform[] Joints =
      new JointTransform[(int)OVRPlugin.SkeletonConstants.MaxHandBones];
  public OVRPlugin.TrackingConfidence HandConfidence;
  public OVRPlugin.TrackingConfidence[] FingerConfidences =
      new OVRPlugin.TrackingConfidence[(int)OVRPlugin.HandFinger.Max];
  public bool IsPinching;
  public bool PinchUp;
  public bool MenuDown;
  public bool IsMenuPressed;
  public bool HasState = false;

  private OVRPlugin.Hand _hand;
  private OVRPlugin.SkeletonType _skeletonType;
  private OVRInput.Controller _controller;
  private OVRPlugin.HandState _state = new();
  private OVRPlugin.Skeleton2 _skeleton = new();
  private DebugHand _debugHand;

  public HandState(bool isLeft) {
    IsLeft = isLeft;
    _controller =
        isLeft ? OVRInput.Controller.LHand : OVRInput.Controller.RHand;

    _skeletonType = isLeft ? OVRPlugin.SkeletonType.HandLeft
                           : OVRPlugin.SkeletonType.HandRight;
    if (!OVRPlugin.GetSkeleton2(_skeletonType, _skeleton)) {
      throw new Exception("Failed to get hand skeleton");
    }

    _debugHand = new(_skeleton);

    _hand = isLeft ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
    Update();
  }

  public void Update() {
    // TODO: Try out wide motion mode
    if (!OVRPlugin.GetHandState(OVRPlugin.Step.Render, _hand, _state) ||
        // Seems like confidence is only ever high when hand is tracked
        // TODO: If state does not change at all, immediately mark state as
        // untracked?
        _state.HandConfidence != OVRPlugin.TrackingConfidence.High) {
      HasState = false;
      return;
    }
    HasState = true;

    Position = Utils.FromFlippedZVector3f(_state.RootPose.Position);
    Rotation = Utils.FromFlippedZQuatf(_state.RootPose.Orientation);
    Scale = _state.HandScale;

    for (var i = 0; i < Joints.Length; i++) {
      var localRot = Utils.FromFlippedZQuatf(_state.BoneRotations[i]);
      var parentIdx = _skeleton.Bones[i].ParentBoneIndex;
      var parentJoint =
          OVRPlugin.IsValidBone((OVRPlugin.BoneId)parentIdx, _skeletonType)
          // parentIdx will always be less than i so Joints[parentIdx] will
          // already be updated
          ? Joints[parentIdx]
          : JointTransform.IDENTITY;
      var handRot = parentJoint.HandRotation * localRot;
      var localPos = parentJoint.HandRotation *
          Utils.FromFlippedZVector3f(_skeleton.Bones[i].Pose.Position);
      var handPos = parentJoint.HandPosition + localPos;

      Joints[i] = new JointTransform() {
        LocalPosition = localPos,
        LocalRotation = localRot,
        HandPosition = handPos,
        HandRotation = handRot,
        TrackingPosition = Position + Rotation * (handPos * Scale),
        TrackingRotation = Rotation * handRot,
      };

      _debugHand.Update(this);
    }

    HandConfidence = _state.HandConfidence;
    FingerConfidences = _state.FingerConfidences;

    MenuDown = (_state.Status & OVRPlugin.HandStatus.MenuPressed) != 0;
    var isIndexPinching =
        (_state.Pinches & OVRPlugin.HandFingerPinch.Index) != 0;
    IsMenuPressed = isIndexPinching && (IsMenuPressed || MenuDown);

    var wasPinching = IsPinching;
    IsPinching = isIndexPinching && !IsMenuPressed;
    PinchUp = wasPinching && !IsPinching;
  }

  public bool IsActive() =>
      // NOTE: Requires OVRInput.Update() to be called (game already does this)
      OVRInput.IsControllerConnected(_controller);

  public bool IsTracked() => HasState &&
      (_state.Status & OVRPlugin.HandStatus.HandTracked) != 0;
}

public struct JointTransform {
  public static JointTransform IDENTITY = new JointTransform() {
    LocalPosition = Vector3.zero,    LocalRotation = Quaternion.identity,
    HandPosition = Vector3.zero,     HandRotation = Quaternion.identity,
    TrackingPosition = Vector3.zero, TrackingRotation = Quaternion.identity,
  };

  public Vector3 LocalPosition;
  public Quaternion LocalRotation;
  public Vector3 HandPosition;
  public Quaternion HandRotation;
  public Vector3 TrackingPosition;
  public Quaternion TrackingRotation;
}
