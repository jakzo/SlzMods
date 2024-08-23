using System;
using System.Linq;
using MelonLoader;
using UnityEngine;
using SLZ.Marrow.Input;
using SLZ.Rig;
using SLZ.Interaction;
using Sst.Utilities;

namespace Sst.HandTracking;

public class HandTracker {
  public struct Options {
    public bool isLeft;
    public XRController marrowController;
    public Action<XRController> setMarrowController;
    public OVRInput.Controller ovrController;
    // Rotation is off for some reason so we need to correct it
    // TODO: Are these offsets related to HandActionMap.LeftAnimSpace? Or the
    // controller type?
    public Quaternion handRotationOffset;
    public Vector3 handPositionOffset;
    public Func<Vector2> getLocoAxis;
    public Func<bool> getWeaponButtonPressed;
  }

  private const float GRIP_CURL_THRESHOLD_DOWN = 0.6f;
  private const float GRIP_CURL_THRESHOLD_UP = 0.5f;
  private const float TRIGGER_DIST_FROM_WRIST = 0.1f;
  private const float MAX_HOVER_LOCK_TIME = 0.5f;
  private static OVRPlugin.HandFinger[] GRIP_FINGERS = {
    OVRPlugin.HandFinger.Middle,
    OVRPlugin.HandFinger.Ring,
    OVRPlugin.HandFinger.Pinky,
  };
  private static Quaternion WEAPON_ROTATION_OFFSET =
      Quaternion.Euler(0f, 20f, 0f);

  public Options Opts;
  public bool IsTracking = false;
  public bool IsGripping = false;
  public bool Proxy = false;
  public XRController ProxyController;
  public BaseController RigController;
  public HandState HandState;
  public HandPose HandPose;
  public Inventory Inventory;
  public AutoSight AutoSight;

  private OVRPlugin.Skeleton2 _skeleton;
  private bool[] _fingerGripStates = new bool[(int)OVRPlugin.HandFinger.Max];
  private ForcePull _forcePull;
  private Ui _ui;
  private Hand _physicalHand;
  private Quaternion _weaponRotationOffset = Quaternion.identity;

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

    _forcePull = new(this);
    _ui = new(this);
    Inventory = new(this);
    HandPose = new(this);
    AutoSight = new(this, null);

    Log("Initialized HandTracker");
  }

  public bool
  IsControllerConnected() => OVRInput.IsControllerConnected(Opts.ovrController);

  public void OnUpdate() {
    _logIndex++;

    if (RigController == null) {
      var controllerRig = LevelHooks.RigManager?.ControllerRig;
      if (controllerRig != null) {
        RigController = Opts.isLeft ? controllerRig.leftController
                                    : controllerRig.rightController;
      }
    }

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

  public void SetWeaponRotationOffset(float degrees) {
    _weaponRotationOffset =
        Quaternion.Euler(0f, Opts.isLeft ? -degrees : degrees, 0f);
  }

  public void UpdateProxyController() {
    HandState.Update();

    ProxyController._IsConnected_k__BackingField = true;
    ProxyController.IsTracking = HandState.IsTracked();
    if (ProxyController.IsTracking) {
      ProxyController.Rotation = HandState.Rotation * Opts.handRotationOffset;
      ProxyController.Position = HandState.Position +
          ProxyController.Rotation * Opts.handPositionOffset;

      if (IsHoldingInteractableItem()) {
        ProxyController.Rotation *= _weaponRotationOffset;
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

      HandPose.UpdateFingerCurls();
      UpdateGrip();
      _forcePull.UpdateHand();
      UpdateTrigger();
      _ui.UpdateHand();
      UpdateWeaponButton();
      Inventory.UpdateHand();
    }

    UpdateLocomotion();
  }

  private void UpdateGrip() {
    for (var i = 0; i < (int)OVRPlugin.HandFinger.Max; i++) {
      // TODO: Does it even report low confidence?
      if (HandState.FingerConfidences[i] != OVRPlugin.TrackingConfidence.High)
        continue;
      var threshold = _fingerGripStates[i] ? GRIP_CURL_THRESHOLD_UP
                                           : GRIP_CURL_THRESHOLD_DOWN;
      _fingerGripStates[i] = HandPose.FingerCurls[i] >= threshold;
    }

    IsGripping = GRIP_FINGERS.All(finger => _fingerGripStates[(int)finger]);

    if (IsGripping) {
      ProxyController.GripButtonDown = !ProxyController.GripButton;
      ProxyController.GripButtonUp = false;
      ProxyController.GripButton = true;
      ProxyController.Grip = 1f;
    } else {
      ProxyController.GripButtonUp = ProxyController.GripButton;
      ProxyController.GripButtonDown = false;
      ProxyController.GripButton = false;
      ProxyController.Grip = 0f;
    }
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

  private void UpdateTrigger() {
    if (_forcePull.IsPulling() || GetRelativeIndexTipPos() > 0f) {
      ProxyController.TriggerButtonDown = !ProxyController.TriggerButton;
      ProxyController.TriggerButtonUp = false;
      ProxyController.TriggerButton = true;
      ProxyController.TriggerTouched = true;
      ProxyController.Trigger = 1f;
    } else {
      ProxyController.TriggerButtonUp = ProxyController.TriggerButton;
      ProxyController.TriggerButtonDown = false;
      ProxyController.TriggerButton = false;
      ProxyController.TriggerTouched = false;
      ProxyController.Trigger = 0f;
    }
  }

  private bool IsHoldingInteractableItem() =>
      GetPhysicalHand()?.AttachedReceiver?.TryCast<TargetGrip>() != null;

  // Result is positive if tip of index finger is closer the wrist than
  // the index knuckle, else negative if tip is further from the wrist
  public float GetRelativeIndexTipPos() {
    var tipToProximalDiff =
        HandState.Joints[(int)OVRPlugin.BoneId.Hand_IndexTip].HandPosition.x -
        HandState.Joints[(int)OVRPlugin.BoneId.Hand_Index1].HandPosition.x;
    return Opts.isLeft ? -tipToProximalDiff : tipToProximalDiff;
  }

  private void UpdateWeaponButton() {
    if (Opts.getWeaponButtonPressed() && IsHoldingInteractableItem()) {
      ProxyController.BButtonDown = !ProxyController.BButton;
      ProxyController.BButtonUp = false;
      ProxyController.BButton = true;
    } else {
      ProxyController.BButtonUp = ProxyController.BButton;
      ProxyController.BButtonDown = false;
      ProxyController.BButton = false;
    }
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
}
