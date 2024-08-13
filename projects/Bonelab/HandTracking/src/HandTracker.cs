using System;
using System.Linq;
using MelonLoader;
using UnityEngine;
using SLZ.Marrow.Input;
using SLZ.Rig;
using SLZ.Interaction;
using Sst.Utilities;
using SLZ.Bonelab;
using SLZ.VRMK;
using SLZ.Data;

namespace Sst.HandTracking;

public class HandTracker {
  public struct Options {
    public bool isLeft;
    public XRController marrowController;
    public Action<XRController> setMarrowController;
    public OVRInput.Controller ovrController;
    public OVRInput.Controller ovrHand;
    // Rotation is off for some reason so we need to correct it
    // TODO: Are these offsets related to HandActionMap.LeftAnimSpace?
    // TODO: Seem to be different offsets per avatar (or maybe that's just the
    // avatar space remapping)
    public Quaternion handRotationOffset;
    public Vector3 handPositionOffset;
    public Func<Vector2> getLocoAxis;
    public Func<bool> getWeaponButtonPressed;
  }

  private const float GRIP_CURL_THRESHOLD = 0.6f;
  private const float TRIGGER_DIST_FROM_WRIST = 0.1f;
  private const float MAX_HOVER_LOCK_TIME = 0.5f;
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
  private static OVRPlugin.HandFinger[] GRIP_FINGERS = {
    OVRPlugin.HandFinger.Middle,
    OVRPlugin.HandFinger.Ring,
    OVRPlugin.HandFinger.Pinky,
  };

  public Options Opts;
  public bool IsTracking = false;
  public bool PinchUp = false;
  public bool IsPinching = false;
  public bool IsMenuOpen = false;
  public bool IsGripping = false;
  public float? PedalInput;
  public bool Proxy = false;
  public XRController ProxyController;
  public HandState HandState;

  private OVRPlugin.HandState _handState;
  private OVRPlugin.Skeleton2 _skeleton;
  private float[] _fingerCurls = new float[(int)OVRPlugin.HandFinger.Max];
  private bool[] _fingerGripStates = new bool[(int)OVRPlugin.HandFinger.Max];
  private UIControllerInput _uiControllerInput;
  private ForcePull _forcePull;
  private Hand _physicalHand;

  private int _logIndex = 0;
  private static TMPro.TextMeshPro _wristLog;
  private string LogString(params object[] messageParts
  ) => string.Join(" ", messageParts.Select(part => part?.ToString()));
  internal void Log(params object[] messageParts) {
    var prefix = Opts.isLeft ? "[L] " : "[R] ";
    Mod.Instance.LoggerInstance.Msg(prefix + LogString(messageParts));
  }
  internal void LogSpam(params object[] messageParts) {
    if (_logIndex % 100 == 0)
      Log(messageParts);
  }
  internal void LogToWrist(params object[] messageParts) {
    if (LevelHooks.RigManager == null)
      return;
    if (!_wristLog)
      _wristLog = Bonelab.CreateTextOnWrist("Sst_HandTracker_WristLog");
    _wristLog.SetText(LogString(messageParts));
  }

  public HandTracker(Options options) {
    Opts = options;

    ProxyController = new ControllerActionMap() {
      _DeviceInfo_k__BackingField = "ProxyController",
      _xrDevice = new() { m_Initialized = true },
      Type = XRControllerType.OculusTouch,
    };
    HandState = new(Opts.isLeft);

    _forcePull = new() { Tracker = this };

    Log("Initialized HandTracker");
  }

  public bool
  IsControllerConnected() => OVRInput.IsControllerConnected(Opts.ovrController);

  public void OnUpdate() {
    _logIndex++;

    if (IsControllerConnected()) {
      if (IsTracking) {
        IsTracking = false;
        Opts.setMarrowController(Opts.marrowController);
        Log("Hand tracking is now inactive");
      }
      return;
    }

    if (!HandState.IsActive())
      return;

    if (!IsTracking) {
      IsTracking = true;
      Opts.setMarrowController(ProxyController);
      Log("Hand tracking is now active");
    }
  }

  public void UpdateProxyController() {
    HandState.Update();

    ProxyController._IsConnected_k__BackingField = true;
    ProxyController.IsTracking = HandState.IsTracked();
    if (ProxyController.IsTracking) {

      ProxyController.Rotation = HandState.Rotation * Opts.handRotationOffset;
      ProxyController.Position = HandState.Position +
          ProxyController.Rotation * Opts.handPositionOffset;

      var ovrHand =
          Opts.isLeft ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
      if (_handState == null)
        _handState = new OVRPlugin.HandState();
      // TODO: Try out wide motion mode
      if (!OVRPlugin.GetHandState(OVRPlugin.Step.Render, ovrHand, _handState)) {
        _handState = null;
        return;
      }

      if (_skeleton == null) {
        _skeleton = new OVRPlugin.Skeleton2();
        var skeletonType = Opts.isLeft ? OVRPlugin.SkeletonType.HandLeft
                                       : OVRPlugin.SkeletonType.HandRight;
        if (!OVRPlugin.GetSkeleton2(skeletonType, _skeleton)) {
          _skeleton = null;
          MelonLogger.Warning("Failed to get hand skeleton");
          return;
        }
      }

      UpdateFingerCurls();
      UpdateUiPinch();
      _forcePull.Update();
      UpdateTrigger();
      UpdateMenu();
      UpdateVehiclePedals();
      UpdateWeaponButton();
    }

    UpdateLocomotion();
  }

  private void UpdateFingerCurls() {
    _fingerCurls[(int)OVRPlugin.HandFinger.Thumb] =
        CalculateFingerCurl(FINGER_JOINTS_THUMB, 100f);
    _fingerCurls[(int)OVRPlugin.HandFinger.Index] =
        CalculateFingerCurl(FINGER_JOINTS_INDEX, 200f);
    _fingerCurls[(int)OVRPlugin.HandFinger.Middle] =
        CalculateFingerCurl(FINGER_JOINTS_MIDDLE, 200f);
    _fingerCurls[(int)OVRPlugin.HandFinger.Ring] =
        CalculateFingerCurl(FINGER_JOINTS_RING, 200f);
    _fingerCurls[(int)OVRPlugin.HandFinger.Pinky] =
        CalculateFingerCurl(FINGER_JOINTS_PINKY, 200f);

    ProxyController.ThumbFinger = _fingerCurls[(int)OVRPlugin.HandFinger.Thumb];
    ProxyController.IndexFinger = _fingerCurls[(int)OVRPlugin.HandFinger.Index];
    ProxyController.MiddleFinger =
        _fingerCurls[(int)OVRPlugin.HandFinger.Middle];
    ProxyController.RingFinger = _fingerCurls[(int)OVRPlugin.HandFinger.Ring];
    ProxyController.PinkyFinger = _fingerCurls[(int)OVRPlugin.HandFinger.Pinky];

    foreach (var finger in GRIP_FINGERS) {
      if (_handState.FingerConfidences[(int)finger] ==
          OVRPlugin.TrackingConfidence.High) {
        _fingerGripStates[(int)finger] =
            _fingerCurls[(int)finger] >= GRIP_CURL_THRESHOLD;
      }
    }
    IsGripping = GRIP_FINGERS.All(finger => _fingerGripStates[(int)finger]);

    if (IsGripping) {
      ProxyController.GripButtonDown = !ProxyController.GripButton;
      ProxyController.GripButtonUp = false;
      ProxyController.GripButton = true;
      ProxyController.Grip = 1f;
      if (ProxyController.GripButtonDown)
        Log("Grip button down");
    } else {
      ProxyController.GripButtonUp = ProxyController.GripButton;
      ProxyController.GripButtonDown = false;
      ProxyController.GripButton = false;
      ProxyController.Grip = 0f;
      if (ProxyController.GripButtonUp)
        Log("Grip button up");
    }
  }

  private float
  CalculateFingerCurl(OVRPlugin.BoneId[] fingerJoints, float maxRotation) {
    var totalRotation = 0f;
    foreach (var joint in fingerJoints) {
      var rot = HandState.Joints[(int)joint].LocalRotation.eulerAngles.z;
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

  private void UpdateLocomotion() {
    if (Opts.isLeft != Utils.IsLocoControllerLeft())
      return;

    ProxyController.Joystick2DAxis = Opts.getLocoAxis();

    if (ProxyController.Joystick2DAxis.sqrMagnitude > 0.1f) {
      ProxyController.JoystickButtonDown = !ProxyController.JoystickButton;
      ProxyController.JoystickButtonUp = false;
      ProxyController.JoystickButton = true;
      ProxyController.JoystickTouch = true;
    } else {
      ProxyController.JoystickButtonUp = ProxyController.JoystickButton;
      ProxyController.JoystickButtonDown = false;
      ProxyController.JoystickButton = false;
      ProxyController.JoystickTouch = false;
    }
  }

  private void UpdateUiPinch() {
    var isPinching =
        (_handState.Pinches & OVRPlugin.HandFingerPinch.Index) != 0;

    if (ProxyController.BButton && PinchUp) {
      IsMenuOpen = false;
      Log("Closed menu due to pinch");
    }

    PinchUp = isPinching && !IsPinching;
    IsPinching = isPinching;
  }

  private void UpdateTrigger() {
    if (IsTriggerPressed()) {
      ProxyController.TriggerButtonDown = !ProxyController.TriggerButton;
      ProxyController.TriggerButtonUp = false;
      ProxyController.TriggerButton = true;
      ProxyController.TriggerTouched = true;
      ProxyController.Trigger = 1f;
      if (ProxyController.TriggerButtonDown)
        Log("Trigger button down");
    } else {
      ProxyController.TriggerButtonUp = ProxyController.TriggerButton;
      ProxyController.TriggerButtonDown = false;
      ProxyController.TriggerButton = false;
      ProxyController.TriggerTouched = false;
      ProxyController.Trigger = 0f;
      if (ProxyController.TriggerButtonUp)
        Log("Trigger button up");
    }
  }

  private bool IsTriggerPressed() {
    if (_forcePull.IsPulling())
      return true;

    if (!IsHoldingInteractableItem())
      return IsGripping;

    var indexPos = GetRelativeIndexTipPos();
    return indexPos.x > 0f;
  }

  private bool
  IsHoldingInteractableItem() => GetPhysicalHand()?.AttachedReceiver?.Host
      != null;

  // TODO: Just use HandState.HandPosition
  private Vector3 GetRelativeIndexTipPos() {
    var jointPos = Vector3.zero;
    var jointRot = Quaternion.identity;
    // TODO: Can we work backwards from the tip based on parent bone?
    foreach (var boneId in new[] {
               OVRPlugin.BoneId.Hand_Index1,
               OVRPlugin.BoneId.Hand_Index2,
               OVRPlugin.BoneId.Hand_Index3,
               OVRPlugin.BoneId.Hand_IndexTip,
             }) {
      var rot = _handState.BoneRotations[(int)boneId];
      jointRot *=
          Utils.FromFlippedXQuatf(_handState.BoneRotations[(int)boneId]);
      jointPos += jointRot *
          Utils.FromFlippedXVector3f(_skeleton.Bones[(int)boneId].Pose.Position
          );
    }
    return Opts.isLeft ? jointPos : -jointPos;
  }

  private void UpdateVehiclePedals() {
    if (!LevelHooks.RigManager?.activeSeat ||
        GetPhysicalHand()?.AttachedReceiver == null) {
      PedalInput = null;
      return;
    }

    if (!IsGrippingSteeringWheel()) {
      PedalInput = null;
      return;
    }

    var indexPos = GetRelativeIndexTipPos();
    var min = -0.01f;
    var max = 0.01f;
    PedalInput = Mathf.Clamp01((indexPos.x - min) / (max - min));
  }

  // TODO: Is this reliable enough to detect modded vehicle steering wheels?
  private bool IsGrippingSteeringWheel() {
    var attachedHost =
        GetPhysicalHand()?.AttachedReceiver?.Host?.TryCast<InteractableHost>();
    var hingeHost = attachedHost?.GetComponent<HingeVirtualController>()
                        ?.host?.TryCast<InteractableHost>();
    return hingeHost?.GetInstanceID() == attachedHost.GetInstanceID();
  }

  // TODO: Some weirdness with popup menu closing on first pinch
  private void UpdateMenu() {
    var menu = UIRig.Instance?.popUpMenu;
    if (!menu)
      return;
    var rigManager = LevelHooks.RigManager;
    if (!rigManager)
      return;
    var rigController = Utils.RigControllerOf(this);

    var isMenuPressed =
        (_handState.Status & OVRPlugin.HandStatus.MenuPressed) != 0;
    if (isMenuPressed) {
      if (menu.m_IsActivated) {
        menu.Deactivate();
      } else {
        var physicsRig = rigManager.physicsRig;
        menu.Activate(
            rigManager.ControllerRig.m_head, physicsRig.m_chest,
            GetUiControllerInput(rigController), rigController
        );
      }
      return;
    }

    if (menu.m_IsActivated) {
      if (PinchUp) {
        menu.Trigger(false, false, GetUiControllerInput(rigController));
      }
    }
  }

  private void UpdateWeaponButton() {
    if (Opts.getWeaponButtonPressed() && IsHoldingInteractableItem()) {
      ProxyController.BButtonDown = !ProxyController.BButton;
      ProxyController.BButtonUp = false;
      ProxyController.BButton = true;
      if (ProxyController.BButtonDown)
        Log("Weapon button down");
    } else {
      ProxyController.BButtonUp = ProxyController.BButton;
      ProxyController.BButtonDown = false;
      ProxyController.BButton = false;
    }
  }

  private UIControllerInput GetUiControllerInput(BaseController rigController) {
    if (!_uiControllerInput) {
      _uiControllerInput = rigController.GetComponent<UIControllerInput>();
    }
    return _uiControllerInput;
  }

  public Hand GetPhysicalHand() {
    if (_physicalHand)
      return _physicalHand;

    var physicsRig = LevelHooks.RigManager?.physicsRig;
    if (physicsRig == null)
      return null;

    _physicalHand = Opts.isLeft ? physicsRig.leftHand : physicsRig.rightHand;
    return _physicalHand;
  }

  public void OnOpenControllerProcessFingers(OpenController openController) {
    // Skip processing our curl values because they are already good
    openController._processedThumb = ProxyController.ThumbFinger;
    openController._processedIndex = ProxyController.IndexFinger;
    openController._processedMiddle = ProxyController.MiddleFinger;
    openController._processedRing = ProxyController.RingFinger;
    openController._processedPinky = ProxyController.PinkyFinger;
  }

  public void OnHandAnimate(HandPoseAnimator animator) {
    var hand = GetPhysicalHand();
    if (!IsTracking || hand.AttachedReceiver != null)
      return;

    var openPose = hand.Animator._openPose;
    Quaternion FingerJointRotation(
        Quaternion baseRotation, OVRPlugin.BoneId boneId
    ) => baseRotation *
        Utils.FlipXY(HandState.Joints[(int)boneId].LocalRotation);
    float FingerJointAngle(float baseAngleDegrees, OVRPlugin.BoneId boneId) =>
        baseAngleDegrees +
        HandState.Joints[(int)boneId].LocalRotation.eulerAngles.z;

    // TODO: Tune these values to match the default hand tracking pose
    // TODO: How do we know when the hand is in the open pose?
    // TODO: Can we have it smoothly transition from hand tracked joints to
    // other poses?
    hand.Animator._currentPoseData = new HandPose.PoseData() {
      thumb1 = FingerJointRotation(
          Quaternion.Euler(54.7015f, 80.8802f, 36.1174f),
          OVRPlugin.BoneId.Hand_Thumb1
      ),
      thumb2 = FingerJointAngle(0.9604f, OVRPlugin.BoneId.Hand_Thumb2),
      thumb3 = FingerJointAngle(-0.7674f, OVRPlugin.BoneId.Hand_Thumb3),
      index1 = FingerJointRotation(
          Quaternion.Euler(359.7946f, 10.2376f, 4.6807f),
          OVRPlugin.BoneId.Hand_Index1
      ),
      index2 = FingerJointAngle(-0.045f, OVRPlugin.BoneId.Hand_Index2),
      index3 = FingerJointAngle(-0.2698f, OVRPlugin.BoneId.Hand_Index3),
      middle1 = FingerJointRotation(
          Quaternion.Euler(355.5994f, 359.7487f + 8f, 0.4831f),
          OVRPlugin.BoneId.Hand_Middle1
      ),
      middle2 = FingerJointAngle(0.1587f, OVRPlugin.BoneId.Hand_Middle2),
      middle3 = FingerJointAngle(-0.0703f, OVRPlugin.BoneId.Hand_Middle3),
      ring1 = FingerJointRotation(
          Quaternion.Euler(356.0975f, 355.5379f + 8f, 6.0902f),
          OVRPlugin.BoneId.Hand_Ring1
      ),
      ring2 = FingerJointAngle(-0.064f, OVRPlugin.BoneId.Hand_Ring2),
      ring3 = FingerJointAngle(-0.064f, OVRPlugin.BoneId.Hand_Ring3),
      pinky1 = FingerJointRotation(
          Quaternion.Euler(355.7065f - 15f, 345.3653f - 5f, 10.7265f),
          OVRPlugin.BoneId.Hand_Pinky1
      ),
      pinky2 = FingerJointAngle(0f, OVRPlugin.BoneId.Hand_Pinky2),
      pinky3 = FingerJointAngle(0f, OVRPlugin.BoneId.Hand_Pinky3),
    };
  }
}
