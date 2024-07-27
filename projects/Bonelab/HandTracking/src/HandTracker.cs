using System;
using System.Linq;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using SLZ.Marrow.Input;
using SLZ.Rig;
using SLZ.Interaction;
using Sst.Utilities;
using SLZ.Bonelab;

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
    // TODO: Seem to be different offsets per avatar
    public Quaternion handRotationOffset;
    public Vector3 handPositionOffset;
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
  public HandLocomotion LocoState;
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
  private string LogString(params object[] messageParts) =>
      string.Join(" ", messageParts.Select(part => part.ToString()));
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

  public void UpdateProxyController(Vector2? locoAxis) {
    HandState.Update();

    ProxyController._IsConnected_k__BackingField = true;
    ProxyController.IsTracking = HandState.IsTracked();
    if (ProxyController.IsTracking) {

      ProxyController.Rotation = HandState.Rotation * Opts.handRotationOffset;
      ProxyController.Position =
          HandState.Position +
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
      // TODO: Inventory
    }

    UpdateLocomotion(locoAxis);
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

  private float CalculateFingerCurl(OVRPlugin.BoneId[] fingerJoints,
                                    float maxRotation) {
    var totalRotation = 0f;
    var prevJointRot = ToQuaternion(
        _handState.BoneRotations[(int)OVRPlugin.BoneId.Hand_WristRoot]);
    for (var i = 0; i < fingerJoints.Length; i++) {
      var jointRotation =
          ToQuaternion(_handState.BoneRotations[(int)fingerJoints[i]]);
      var relativeRotation = Quaternion.Inverse(prevJointRot) * jointRotation;
      totalRotation += (relativeRotation.eulerAngles.z + 180f) % 360f - 180f;
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

  // TODO: Replace with utils
  private Quaternion ToQuaternion(OVRPlugin.Quatf quatf) =>
      new Quaternion(quatf.x, quatf.y, quatf.z, quatf.w);

  private void UpdateLocomotion(Vector2? axis) {
    if (Opts.isLeft == Utils.IsLocoControllerLeft() && axis.HasValue) {
      ProxyController.Joystick2DAxis = axis.Value;

      if (axis.Value.sqrMagnitude > 0.1f) {
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

    // TODO: Are there any triggerable objects which don't use TargetGrip?
    var isHoldingObject =
        GetPhysicalHand()?.AttachedReceiver?.TryCast<TargetGrip>() != null;
    if (!isHoldingObject)
      return IsGripping;

    var indexPos = GetRelativeIndexTipPos();
    return indexPos.x > 0f;
  }

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
      jointPos += jointRot * Utils.FromFlippedXVector3f(
                                 _skeleton.Bones[(int)boneId].Pose.Position);
    }
    return Opts.isLeft ? jointPos : -jointPos;
  }

  private void UpdateVehiclePedals() {
    // TODO: Only set PedalInput if gripping steering wheel?
    if (!LevelHooks.RigManager?.activeSeat ||
        GetPhysicalHand()?.AttachedReceiver == null) {
      PedalInput = null;
      return;
    }

    var indexPos = GetRelativeIndexTipPos();
    var min = -0.01f;
    var max = 0.01f;
    PedalInput = Mathf.Clamp01((indexPos.x - min) / (max - min));
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
        menu.Activate(rigManager.ControllerRig.m_head, physicsRig.m_chest,
                      GetUiControllerInput(rigController), rigController);
      }
      return;
    }

    if (menu.m_IsActivated) {
      if (PinchUp) {
        menu.Trigger(false, false, GetUiControllerInput(rigController));
      }
    }

    // TODO: Do I need this for things like changing constrainer mode?
    // if ((_handState.Status & OVRPlugin.HandStatus.MenuPressed) != 0) {
    //   ProxyController.BButtonDown = !ProxyController.BButton;
    //   ProxyController.BButtonUp = false;
    //   ProxyController.BButton = true;
    //   if (ProxyController.BButtonDown)
    //     Log("B pressed via menu gesture");
    // } else {
    //   ProxyController.BButtonUp = ProxyController.BButton;
    //   ProxyController.BButtonDown = false;
    //   ProxyController.BButton = false;
    //   if (ProxyController.BButtonUp)
    //     Log("B up");
    // }
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

  // TODO: Animate fingers to the tracked joint positions instead of using curl
  public void OnOpenControllerProcessFingers(OpenController openController) {
    // Skip processing our curl values because they are already good
    openController._processedThumb = ProxyController.ThumbFinger;
    openController._processedIndex = ProxyController.IndexFinger;
    openController._processedMiddle = ProxyController.MiddleFinger;
    openController._processedRing = ProxyController.RingFinger;
    openController._processedPinky = ProxyController.PinkyFinger;
  }
}
