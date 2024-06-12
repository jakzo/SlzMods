using System;
using HarmonyLib;
using MelonLoader;
using Unity.XR.MockHMD;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR;
using SLZ.Marrow.Input;
using SLZ.Marrow.Utilities;
using SLZ.Rig;

namespace Sst.FlatPlayer;

public class FlatBooter : MelonMod {
  public static HmdActionMap XRHmd;
  public static LeftControllerActionMap LeftController;
  public static RightControllerActionMap RightController;
  public static HandActionMap LeftHand;
  public static HandActionMap RightHand;
  public static Camera mainCamera;
  public static bool isReady;

  private static FlatBooter instance;

  private MelonPreferences_Entry<float> cameraSpeed;
  private MelonPreferences_Category saveFile;
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

  public override void OnInitializeMelon() {
    instance = this;

    saveFile = MelonPreferences.CreateCategory("FlatPlayer");
    cameraSpeed = saveFile.CreateEntry<float>("CameraSensitivity", 0.2f);

    new HarmonyLib.Harmony("FlatPlayer").PatchAll();

    var loaders = XRGeneralSettings.Instance.Manager.loaders;
    loaders.Clear();
    loaders.Add(ScriptableObject.CreateInstance<MockHMDLoader>());

    LoggerInstance.Msg("MockHMD");
  }

  public override void OnLateUpdate() {
    if (!isReady)
      return;

    if (Input.GetKey(KeyCode.Escape))
      Cursor.lockState = CursorLockMode.None;

    if (Input.GetMouseButton(0) && Cursor.lockState != CursorLockMode.Locked)
      Cursor.lockState = CursorLockMode.Locked;

    if (Cursor.lockState != CursorLockMode.Locked)
      return;

    if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.Equals))
      cameraSpeed.Value += 0.05f;

    if (Input.GetKeyDown(KeyCode.Minus))
      cameraSpeed.Value =
          Mathf.Clamp(cameraSpeed.Value - 0.05f, 0f, float.MaxValue);

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

    RightController.Joystick2DAxis =
        new Vector2((Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) -
                        (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                    (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) -
                        (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));

    if (Input.GetKeyDown(KeyCode.Q))
      leftHandLocked = !leftHandLocked;
    if (Input.GetKeyDown(KeyCode.E))
      rightHandLocked = !rightHandLocked;

    var leftPos = leftHandDefaultPos;
    var rightPos = rightHandDefaultPos;
    if (Input.GetKey(KeyCode.F)) {
      if (leftHandLocked)
        leftPos.z = 1f;
      if (rightHandLocked)
        rightPos.z = 1f;
    }

    if (Input.GetKeyDown(KeyCode.R))
      rotateMode = !rotateMode;
    if (Input.GetKeyDown(KeyCode.T))
      aimMode = !aimMode;

    if (rotateMode) {
      handsLastRotation.z += Input.GetAxis("Mouse X") * cameraSpeed.Value;
      handsLastRotation.y += Input.GetAxis("Mouse Y") * cameraSpeed.Value;

      var playerRotation =
          Quaternion.AngleAxis(handsLastRotation.x, Vector3.up) *
          Quaternion.AngleAxis(handsLastRotation.y, Vector3.left) *
          Quaternion.AngleAxis(handsLastRotation.z, Vector3.forward);
      if (leftHandLocked)
        LeftController._rotation = playerRotation * leftHandRot;
      if (rightHandLocked)
        RightController._rotation = playerRotation * rightHandRot;
    } else {
      cameraLastRotation.x += Input.GetAxis("Mouse X") * cameraSpeed.Value;
      cameraLastRotation.y += Input.GetAxis("Mouse Y") * cameraSpeed.Value;
      cameraLastRotation.y = Mathf.Clamp(cameraLastRotation.y, -87f, 87f);

      var playerRotation =
          Quaternion.AngleAxis(cameraLastRotation.x, Vector3.up) *
          Quaternion.AngleAxis(cameraLastRotation.y, Vector3.left);
      XRHmd._rotation = playerRotation;
      handsLastRotation = cameraLastRotation;
      if (leftHandLocked) {
        LeftController._rotation = playerRotation * leftHandRot;
        LeftController._position = playerRotation * leftPos;
      }
      if (rightHandLocked) {
        RightController._rotation = playerRotation * rightHandRot;
        RightController._position = playerRotation * rightPos;
      }
    }

    if (Input.GetKeyDown(KeyCode.Tab)) {
      LeftController.BButton = true;
      LeftController.BButtonDown = true;
    }
    if (Input.GetKeyUp(KeyCode.Tab)) {
      LeftController.BButton = false;
      LeftController.BButtonUp = true;
    }

    if (Input.GetKey(KeyCode.Space)) {
      RightController.AButton = true;
      RightController.AButtonDown = true;
    }
    if (Input.GetKeyUp(KeyCode.Space)) {
      RightController.AButtonUp = true;
      RightController.AButton = false;
    }

    if (Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.LeftShift))
      LeftController.JoystickButtonDown = true;

    if (leftHandLocked)
      leftHandGrip = Input.GetMouseButton(0);
    if (rightHandLocked)
      rightHandGrip = Input.GetMouseButton(1);

    LeftController.Grip = leftHandGrip ? 1f : 0f;
    RightController.Grip = rightHandGrip ? 1f : 0f;
    LeftController.GripButton = leftHandGrip;
    RightController.GripButton = rightHandGrip;

    var isTriggerPressed =
        Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftControl);
    LeftController.TriggerButtonDown = leftHandLocked && isTriggerPressed;
    LeftController.Trigger = leftHandLocked && isTriggerPressed ? 1f : 0f;
    RightController.TriggerButtonDown = rightHandLocked && isTriggerPressed;
    RightController.Trigger = rightHandLocked && isTriggerPressed ? 1f : 0f;
  }

  [HarmonyPatch(typeof(OpenControllerRig), nameof(OpenControllerRig.OnAwake))]
  internal static class OpenControllerAwake {
    [HarmonyPrefix]
    private static void Prefix(OpenControllerRig __instance) {
      if (__instance.transform.parent.gameObject.name != "[RigManager (Blank)]")
        return;

      var xr = MarrowGame.xr;
      XRHmd = xr.HMD.Cast<HmdActionMap>();
      LeftController = xr.LeftController.Cast<LeftControllerActionMap>();
      RightController = xr.RightController.Cast<RightControllerActionMap>();
      LeftHand = xr.LeftHand.Cast<HandActionMap>();
      RightHand = xr.RightHand.Cast<HandActionMap>();

      mainCamera = Camera.main;

      var leftXrDevice = LeftController._xrDevice;
      leftXrDevice.m_Initialized = true;
      LeftController._xrDevice = leftXrDevice;
      var rightXrDevice = RightController._xrDevice;
      rightXrDevice.m_Initialized = true;
      RightController._xrDevice = rightXrDevice;

      var characteristics = InputDeviceCharacteristics.Controller |
                            InputDeviceCharacteristics.TrackedDevice;
      LeftController._Characteristics_k__BackingField =
          characteristics | InputDeviceCharacteristics.Left;
      RightController._Characteristics_k__BackingField =
          characteristics | InputDeviceCharacteristics.Right;

      LeftController.Type = 0;
      RightController.Type = 0;

      LeftController._IsConnected_k__BackingField = true;
      RightController._IsConnected_k__BackingField = true;

      LeftController._position = Vector3.zero;
      RightController._position = Vector3.zero;

      LeftController._rotation = Quaternion.identity;
      RightController._rotation = Quaternion.identity;

      LeftController.Refresh();
      RightController.Refresh();

      isReady = true;
      mainCamera.cameraType = CameraType.SceneView;
      mainCamera.fieldOfView = 90f;
    }
  }

  [HarmonyPatch(typeof(OpenControllerRig), nameof(OpenControllerRig.OnDestroy))]
  internal static class OpenControllerDestroy {
    [HarmonyPrefix]
    private static void Prefix(OpenControllerRig __instance) {
      if (__instance.transform.parent.gameObject.name == "[RigManager (Blank)]")
        isReady = false;
    }
  }

  [HarmonyPatch(typeof(XRApi), nameof(XRApi.InitializeXRLoader))]
  internal static class XRApi_InitializeXRLoader {
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result) {
      __result = true;
      return false;
    }
  }

  [HarmonyPatch(typeof(InputDevice), nameof(InputDevice.TryGetFeatureValue),
                new Type[] { typeof(InputFeatureUsage<bool>), typeof(bool) },
                new ArgumentType[] { ArgumentType.Normal, ArgumentType.Out })]
  internal static class XRDevice_IsPresent {
    [HarmonyPrefix]
    private static bool Prefix(InputFeatureUsage<bool> usage, out bool value,
                               ref bool __result) {
      Debug.Log("Requesting Boolean Feature " + usage.name);
      value = usage.name == "UserPresence";
      return false;
    }
  }

  [HarmonyPatch(typeof(SLZ.Marrow.Input.XRDevice),
                nameof(SLZ.Marrow.Input.XRDevice.IsTracking),
                MethodType.Getter)]
  internal static class XRDevice_IsTracking {
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result) {
      __result = true;
      return false;
    }
  }
}
