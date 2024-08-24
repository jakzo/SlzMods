using System;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;

using SLZ.Marrow.Input;
using SLZ.Marrow.Warehouse;
using SLZ.Marrow.Utilities;
using SLZ.SaveData;

namespace Sst.Utilities;

public class FlatMode {
  private static Vector3 ORIGIN = new Vector3(0f, 1.76f, 0f);

  public HmdActionMap Hmd;
  public LeftControllerActionMap LeftController;
  public RightControllerActionMap RightController;
  public HandActionMap LeftHand;
  public HandActionMap RightHand;

  public Camera MainCamera;
  public bool IsReady;

  private static FlatMode _instance;
  private MelonPreferences_Entry<float> _prefCameraSpeed;
  private Vector2 cameraLastRotation = Vector2.zero;
  private Vector3 handsLastRotation = Vector3.zero;
  private Vector3 leftHandDefaultPos = new Vector3(-0.2f, -0.2f, 0.3f);
  private Vector3 rightHandDefaultPos = new Vector3(0.2f, -0.2f, 0.3f);
  private Quaternion leftHandDefaultRot =
      Quaternion.Euler(new Vector3(330f, 50f, 250f));
  private Quaternion rightHandDefaultRot =
      Quaternion.Euler(new Vector3(330f, 310f, 110f));
  private Quaternion leftHandAimRot =
      Quaternion.Euler(new Vector3(290f, 355f, 0f));
  private Quaternion rightHandAimRot =
      Quaternion.Euler(new Vector3(290f, 355f, 0f));
  private bool leftHandLocked = true;
  private bool rightHandLocked = true;
  private bool leftHandGrip = false;
  private bool rightHandGrip = false;
  private bool rotateMode = false;
  private bool aimMode = true;

  private Quaternion leftHandRot {
    get => aimMode ? leftHandAimRot : leftHandDefaultRot;
  }

  private Quaternion rightHandRot {
    get => aimMode ? rightHandAimRot : rightHandDefaultRot;
  }

  public FlatMode(MelonPreferences_Entry<float> prefCameraSpeed) {
    _prefCameraSpeed = prefCameraSpeed;
  }

  public void Start() {
    _instance = this;

    var xr = MarrowGame.xr;

    var hmd = new HmdActionMap();
    xr.HMD = hmd;
    var hmdDevice = hmd._xrDevice;
    hmdDevice.m_Initialized = true;
    hmd._xrDevice = hmdDevice;
    hmd._IsConnected_k__BackingField = true;

    var leftController = new LeftControllerActionMap();
    xr.LeftController = leftController;
    var leftXrDevice = leftController._xrDevice;
    leftXrDevice.m_Initialized = true;
    leftController._xrDevice = leftXrDevice;

    var rightController = new RightControllerActionMap();
    xr.RightController = rightController;
    var rightXrDevice = rightController._xrDevice;
    rightXrDevice.m_Initialized = true;
    rightController._xrDevice = rightXrDevice;

    var leftHand = new HandActionMap(true);
    xr.LeftHand = leftHand;
    var leftHandXrDevice = leftHand._xrDevice;
    leftHandXrDevice.m_Initialized = true;
    leftHand._xrDevice = leftHandXrDevice;

    var rightHand = new HandActionMap(true);
    xr.RightHand = rightHand;
    var rightHandXrDevice = rightHand._xrDevice;
    rightHandXrDevice.m_Initialized = true;
    rightHand._xrDevice = rightHandXrDevice;

    var characteristics = InputDeviceCharacteristics.Controller |
        InputDeviceCharacteristics.TrackedDevice;
    leftController._Characteristics_k__BackingField =
        characteristics | InputDeviceCharacteristics.Left;
    rightController._Characteristics_k__BackingField =
        characteristics | InputDeviceCharacteristics.Right;

    leftController.Type = rightController.Type = XRControllerType.OculusTouch;
    leftController._IsConnected_k__BackingField =
        rightController._IsConnected_k__BackingField = true;
    leftController.Position = rightController.Position = ORIGIN;
    leftController.Rotation = rightController.Rotation = Quaternion.identity;

    Hmd = hmd;
    LeftController = leftController;
    RightController = rightController;
    LeftHand = leftHand;
    RightHand = rightHand;

    Hmd.Refresh();
    LeftController.Refresh();
    RightController.Refresh();
    LeftHand.Refresh();
    RightHand.Refresh();

    var isPassthroughCamera =
        DataManager.Instance._settings.SpectatorSettings.SpectatorCameraMode ==
        SpectatorCameraMode.Passthrough;
    if (isPassthroughCamera) {
      MainCamera = Camera.main;
      MainCamera.cameraType = CameraType.SceneView;
      MainCamera.fieldOfView = 90f;
    }

    ResetRotation();
    IsReady = true;
  }

  public void Stop() {
    ResetRotation();
    IsReady = false;
  }

  public void ResetRotation() {
    cameraLastRotation = handsLastRotation = Vector3.zero;
  }

  public void UpdateHmd() {
    var playerRotation =
        Quaternion.AngleAxis(cameraLastRotation.x, Vector3.up) *
        Quaternion.AngleAxis(cameraLastRotation.y, Vector3.left);
    Hmd.Rotation = Hmd._lastRotation = playerRotation;
    Hmd.Position = Hmd._lastPosition = ORIGIN;
  }

  public void UpdateLeftController() {
    var moveDirection = Vector2.zero;
    if (Input.GetKey(KeyCode.W))
      moveDirection += Vector2.up;
    if (Input.GetKey(KeyCode.A))
      moveDirection += Vector2.left;
    if (Input.GetKey(KeyCode.S))
      moveDirection += Vector2.down;
    if (Input.GetKey(KeyCode.D))
      moveDirection += Vector2.right;
    LeftController.Joystick2DAxis = moveDirection;

    if (Input.GetKeyDown(KeyCode.Tab)) {
      LeftController.BButton = true;
      LeftController.BButtonDown = true;
    } else {
      LeftController.BButtonDown = false;
    }
    if (Input.GetKeyUp(KeyCode.Tab)) {
      LeftController.BButton = false;
      LeftController.BButtonUp = true;
    } else {
      LeftController.BButtonUp = false;
    }

    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
      LeftController.JoystickButtonDown = true;

    if (leftHandLocked)
      leftHandGrip = Input.GetMouseButton(0);

    HandleControllerInput(
        LeftController, leftHandGrip, leftHandLocked, leftHandDefaultPos,
        leftHandRot
    );
  }

  public void UpdateRightController() {
    var stickAxis = Vector2.zero;
    if (Input.GetKey(KeyCode.UpArrow))
      stickAxis += Vector2.up;
    if (Input.GetKey(KeyCode.LeftArrow))
      stickAxis += Vector2.left;
    if (Input.GetKey(KeyCode.DownArrow))
      stickAxis += Vector2.down;
    if (Input.GetKey(KeyCode.RightArrow))
      stickAxis += Vector2.right;
    RightController.Joystick2DAxis = stickAxis;

    if (Input.GetKey(KeyCode.Space)) {
      RightController.AButton = true;
      RightController.AButtonDown = true;
    } else {
      RightController.AButtonDown = false;
    }
    if (Input.GetKeyUp(KeyCode.Space)) {
      RightController.AButtonUp = true;
      RightController.AButton = false;
    } else {
      RightController.AButtonUp = false;
    }

    if (rightHandLocked)
      rightHandGrip = Input.GetMouseButton(1);

    HandleControllerInput(
        RightController, rightHandGrip, rightHandLocked, rightHandDefaultPos,
        rightHandRot
    );
  }

  private void HandleControllerInput(
      ControllerActionMap controller, bool isGripping, bool isLocked,
      Vector3 defaultPos, Quaternion rot
  ) {
    controller.Grip = isGripping ? 1f : 0f;
    controller.GripButton = isGripping;

    controller.TriggerButtonDown = isLocked && IsTriggerPressed();
    controller.Trigger = isLocked && IsTriggerPressed() ? 1f : 0f;

    if (isLocked) {
      if (rotateMode) {
        var playerRotation =
            Quaternion.AngleAxis(handsLastRotation.x, Vector3.up) *
            Quaternion.AngleAxis(handsLastRotation.y, Vector3.left) *
            Quaternion.AngleAxis(handsLastRotation.z, Vector3.forward);
        controller.Rotation = playerRotation * rot;
      } else {
        var newPos = defaultPos;
        if (Input.GetKey(KeyCode.F)) {
          newPos.z = 1f;
        }
        var playerRotation =
            Quaternion.AngleAxis(cameraLastRotation.x, Vector3.up) *
            Quaternion.AngleAxis(cameraLastRotation.y, Vector3.left);
        controller.Rotation = playerRotation * rot;
        controller.Position = ORIGIN + playerRotation * newPos;
      }
    }

    controller._lastPosition = controller.Position;
    controller._lastRotation = controller.Rotation;
  }

  private bool IsTriggerPressed() =>
      Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftControl);

  public void OnUpdate() {
    if (!IsReady)
      return;

    if (Input.GetKey(KeyCode.Escape))
      Cursor.lockState = CursorLockMode.None;

    if (Input.GetMouseButton(0) && Cursor.lockState != CursorLockMode.Locked)
      Cursor.lockState = CursorLockMode.Locked;

    if (Cursor.lockState != CursorLockMode.Locked)
      return;

    if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.Equals))
      _prefCameraSpeed.Value += 0.05f;

    if (Input.GetKeyDown(KeyCode.Minus))
      _prefCameraSpeed.Value =
          Mathf.Clamp(_prefCameraSpeed.Value - 0.05f, 0f, float.MaxValue);

    if (Input.GetKeyDown(KeyCode.Q))
      leftHandLocked = !leftHandLocked;
    if (Input.GetKeyDown(KeyCode.E))
      rightHandLocked = !rightHandLocked;

    if (Input.GetKeyDown(KeyCode.R))
      rotateMode = !rotateMode;
    if (Input.GetKeyDown(KeyCode.T))
      aimMode = !aimMode;

    if (rotateMode) {
      handsLastRotation.z += Input.GetAxis("Mouse X") * _prefCameraSpeed.Value;
      handsLastRotation.y += Input.GetAxis("Mouse Y") * _prefCameraSpeed.Value;
    } else {
      cameraLastRotation.x += Input.GetAxis("Mouse X") * _prefCameraSpeed.Value;
      cameraLastRotation.y += Input.GetAxis("Mouse Y") * _prefCameraSpeed.Value;
      cameraLastRotation.y = Mathf.Clamp(cameraLastRotation.y, -87f, 87f);

      handsLastRotation = cameraLastRotation;
    }
  }
}
