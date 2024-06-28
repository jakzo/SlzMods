using System;
using System.Linq;
using MelonLoader;
using HarmonyLib;
using UnityEngine;

using StressLevelZero;
using StressLevelZero.Rig;

namespace Sst.Utilities;

public class FlatMode {
  private static Vector3 ORIGIN = new Vector3(0f, 1.76f, 0f);

  public ControllerRig ControllerRig;
  public Controller LeftController;
  public Controller RightController;

  public Camera mainCamera;
  public bool isReady;

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
    if (isReady)
      return;

    _instance = this;

    var rigScreen = Data_Manager.Instance.control_player.rigScreen;
    ControllerRig = rigScreen.m_RigManager.ControllerRig;
    LeftController = ControllerRig.leftController.Cast<Controller>();
    RightController = ControllerRig.rightController.Cast<Controller>();

    var isPassthroughCamera = rigScreen.m_Options.cameraMode ==
                              RigScreenOptions.Options.CameraMode.HEADSET;
    if (isPassthroughCamera) {
      mainCamera = Camera.main;
      mainCamera.cameraType = CameraType.SceneView;
      mainCamera.fieldOfView = 90f;
    }

    cameraLastRotation = handsLastRotation = Vector3.zero;
    isReady = true;
  }

  public void Stop() { isReady = false; }

  public void UpdateHmd() {
    var playerRotation =
        Quaternion.AngleAxis(cameraLastRotation.x, Vector3.up) *
        Quaternion.AngleAxis(cameraLastRotation.y, Vector3.left);
    ControllerRig.hmdTransform.rotation = playerRotation;
    ControllerRig.hmdTransform.position = ORIGIN;
  }

  public void UpdateController(Controller controller) {
    if (controller.handedness == Handedness.LEFT) {
      controller._thumbstickAxis =
          ArrowKeysToAxis(KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D);

      controller._bButton = Input.GetKey(KeyCode.Tab);
      controller._bButtonDown = Input.GetKeyDown(KeyCode.Tab);
      controller._bButtonUp = Input.GetKeyUp(KeyCode.Tab);

      var shiftKeys = new[] { KeyCode.LeftShift, KeyCode.RightShift };
      controller._thumbstick = shiftKeys.Any(Input.GetKey);
      controller._thumbstickDown = shiftKeys.Any(Input.GetKeyDown);
      controller._thumbstickUp = shiftKeys.Any(Input.GetKeyUp);

      if (leftHandLocked)
        leftHandGrip = Input.GetMouseButton(0);

      HandleControllerInput(controller, leftHandGrip, leftHandLocked,
                            leftHandDefaultPos, leftHandRot);
    } else if (controller.handedness == Handedness.RIGHT) {
      controller._thumbstickAxis =
          ArrowKeysToAxis(KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow,
                          KeyCode.RightArrow);

      controller._aButton = Input.GetKey(KeyCode.Space);
      controller._aButtonDown = Input.GetKeyDown(KeyCode.Space);
      controller._aButtonUp = Input.GetKeyUp(KeyCode.Space);

      if (rightHandLocked)
        rightHandGrip = Input.GetMouseButton(1);

      HandleControllerInput(controller, rightHandGrip, rightHandLocked,
                            rightHandDefaultPos, rightHandRot);
    } else {
      Dbg.Log("Controller had no handedness");
    }
  }

  private Vector2 ArrowKeysToAxis(KeyCode up, KeyCode down, KeyCode left,
                                  KeyCode right) {
    var axis = Vector2.zero;
    if (Input.GetKey(up))
      axis += Vector2.up;
    if (Input.GetKey(down))
      axis += Vector2.down;
    if (Input.GetKey(left))
      axis += Vector2.left;
    if (Input.GetKey(right))
      axis += Vector2.right;
    return axis;
  }

  private void HandleControllerInput(Controller controller, bool isGripping,
                                     bool isLocked, Vector3 defaultPos,
                                     Quaternion rot) {
    controller.gripAxis = isGripping ? 1f : 0f;
    controller._secondaryInteractionButton = isGripping;

    controller._primaryAxis = isLocked && IsTriggerPressed() ? 1f : 0f;
    controller._primaryInteractionButton = isLocked && IsTriggerPressed();

    if (isLocked) {
      if (rotateMode) {
        var playerRotation =
            Quaternion.AngleAxis(handsLastRotation.x, Vector3.up) *
            Quaternion.AngleAxis(handsLastRotation.y, Vector3.left) *
            Quaternion.AngleAxis(handsLastRotation.z, Vector3.forward);
        controller._localTrackRot = playerRotation * rot;
      } else {
        var newPos = defaultPos;
        if (Input.GetKey(KeyCode.F)) {
          newPos.z = 1f;
        }
        var playerRotation =
            Quaternion.AngleAxis(cameraLastRotation.x, Vector3.up) *
            Quaternion.AngleAxis(cameraLastRotation.y, Vector3.left);
        controller._localTrackRot = playerRotation * rot;
        controller._localTrackPos = ORIGIN + playerRotation * newPos;
      }
    }
  }

  private bool IsTriggerPressed() =>
      Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftControl);

  public void OnUpdate() {
    if (!isReady)
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
