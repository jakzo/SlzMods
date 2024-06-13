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
using System.Reflection;

namespace Sst.FlatPlayer;

public class FlatBooter : MelonMod {
  public static FlatBooter instance;

  public static HmdActionMap Hmd;
  public static LeftControllerActionMap LeftController;
  public static RightControllerActionMap RightController;
  public static HandActionMap LeftHand;
  public static HandActionMap RightHand;
  public static Camera mainCamera;
  public static bool isReady;

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

    var harmony = new HarmonyLib.Harmony("FlatPlayer");
    harmony.PatchAll();

    var loaders = XRGeneralSettings.Instance.Manager.loaders;
    loaders.Clear();
    loaders.Add(ScriptableObject.CreateInstance<MockHMDLoader>());

    LoggerInstance.Msg("MockHMD");
  }

  [HarmonyPatch(typeof(HmdActionMap), nameof(HmdActionMap.Refresh))]
  internal static class HmdActionMap_Refresh {
    [HarmonyPrefix]
    private static bool Prefix() {
      instance.UpdateHmd();
      return false;
    }
  }

  public void UpdateHmd() {
    var playerRotation =
        Quaternion.AngleAxis(cameraLastRotation.x, Vector3.up) *
        Quaternion.AngleAxis(cameraLastRotation.y, Vector3.left);
    Hmd.Rotation = playerRotation;
    Hmd._lastRotation = playerRotation;
  }

  [HarmonyPatch(typeof(ControllerActionMap),
                nameof(ControllerActionMap.Refresh))]
  internal static class ControllerActionMap_Refresh {
    [HarmonyPrefix]
    private static bool Prefix(ControllerActionMap __instance) {
      if (__instance.Equals(LeftController)) {
        instance.UpdateLeftController();
      } else {
        instance.UpdateRightController();
      }
      return false;
    }
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
    }
    if (Input.GetKeyUp(KeyCode.Tab)) {
      LeftController.BButton = false;
      LeftController.BButtonUp = true;
    }

    if (Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.LeftShift))
      LeftController.JoystickButtonDown = true;

    if (leftHandLocked)
      leftHandGrip = Input.GetMouseButton(0);

    LeftController.Grip = leftHandGrip ? 1f : 0f;
    LeftController.GripButton = leftHandGrip;

    var isTriggerPressed =
        Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftControl);
    LeftController.TriggerButtonDown = leftHandLocked && isTriggerPressed;
    LeftController.Trigger = leftHandLocked && isTriggerPressed ? 1f : 0f;

    if (leftHandLocked) {
      if (rotateMode) {
        var playerRotation =
            Quaternion.AngleAxis(handsLastRotation.x, Vector3.up) *
            Quaternion.AngleAxis(handsLastRotation.y, Vector3.left) *
            Quaternion.AngleAxis(handsLastRotation.z, Vector3.forward);
        LeftController.Rotation = playerRotation * leftHandRot;
      } else {
        var leftPos = leftHandDefaultPos;
        if (Input.GetKey(KeyCode.F)) {
          leftPos.z = 1f;
        }
        var playerRotation =
            Quaternion.AngleAxis(cameraLastRotation.x, Vector3.up) *
            Quaternion.AngleAxis(cameraLastRotation.y, Vector3.left);
        LeftController.Rotation = playerRotation * leftHandRot;
        LeftController.Position = playerRotation * leftPos;
      }
    }

    LeftController._lastPosition = LeftController.Position;
    LeftController._lastRotation = LeftController.Rotation;
  }

  public void UpdateRightController() {
    RightController.Joystick2DAxis =
        new Vector2((Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) -
                        (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                    (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) -
                        (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));

    if (Input.GetKey(KeyCode.Space)) {
      RightController.AButton = true;
      RightController.AButtonDown = true;
    }
    if (Input.GetKeyUp(KeyCode.Space)) {
      RightController.AButtonUp = true;
      RightController.AButton = false;
    }

    if (rightHandLocked)
      rightHandGrip = Input.GetMouseButton(1);

    RightController.Grip = rightHandGrip ? 1f : 0f;
    RightController.GripButton = rightHandGrip;

    var isTriggerPressed =
        Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftControl);
    RightController.TriggerButtonDown = rightHandLocked && isTriggerPressed;
    RightController.Trigger = rightHandLocked && isTriggerPressed ? 1f : 0f;

    if (rightHandLocked) {
      if (rotateMode) {
        var playerRotation =
            Quaternion.AngleAxis(handsLastRotation.x, Vector3.up) *
            Quaternion.AngleAxis(handsLastRotation.y, Vector3.left) *
            Quaternion.AngleAxis(handsLastRotation.z, Vector3.forward);
        RightController.Rotation = playerRotation * rightHandRot;
      } else {
        var rightPos = rightHandDefaultPos;
        if (Input.GetKey(KeyCode.F)) {
          rightPos.z = 1f;
        }
        var playerRotation =
            Quaternion.AngleAxis(cameraLastRotation.x, Vector3.up) *
            Quaternion.AngleAxis(cameraLastRotation.y, Vector3.left);
        RightController.Rotation = playerRotation * rightHandRot;
        RightController.Position = playerRotation * rightPos;
      }
    }

    RightController._lastPosition = RightController.Position;
    RightController._lastRotation = RightController.Rotation;
  }

  public override void OnUpdate() {
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

    if (Input.GetKeyDown(KeyCode.Q))
      leftHandLocked = !leftHandLocked;
    if (Input.GetKeyDown(KeyCode.E))
      rightHandLocked = !rightHandLocked;

    if (Input.GetKeyDown(KeyCode.R))
      rotateMode = !rotateMode;
    if (Input.GetKeyDown(KeyCode.T))
      aimMode = !aimMode;

    if (rotateMode) {
      handsLastRotation.z += Input.GetAxis("Mouse X") * cameraSpeed.Value;
      handsLastRotation.y += Input.GetAxis("Mouse Y") * cameraSpeed.Value;
    } else {
      cameraLastRotation.x += Input.GetAxis("Mouse X") * cameraSpeed.Value;
      cameraLastRotation.y += Input.GetAxis("Mouse Y") * cameraSpeed.Value;
      cameraLastRotation.y = Mathf.Clamp(cameraLastRotation.y, -87f, 87f);

      handsLastRotation = cameraLastRotation;
    }
  }

  [HarmonyPatch(typeof(OpenControllerRig), nameof(OpenControllerRig.OnAwake))]
  internal static class OpenControllerAwake {
    [HarmonyPrefix]
    private static void Prefix(OpenControllerRig __instance) {
      if (__instance.transform.parent.gameObject.name != "[RigManager (Blank)]")
        return;

      var xr = MarrowGame.xr;

      Hmd = new HmdActionMap();
      xr.HMD = Hmd;
      var hmdDevice = Hmd._xrDevice;
      hmdDevice.m_Initialized = true;
      Hmd._xrDevice = hmdDevice;
      Hmd._IsConnected_k__BackingField = true;

      LeftController = new LeftControllerActionMap();
      xr.LeftController = LeftController;
      var leftXrDevice = LeftController._xrDevice;
      leftXrDevice.m_Initialized = true;
      LeftController._xrDevice = leftXrDevice;

      RightController = new RightControllerActionMap();
      xr.RightController = RightController;
      var rightXrDevice = RightController._xrDevice;
      rightXrDevice.m_Initialized = true;
      RightController._xrDevice = rightXrDevice;

      LeftHand = new HandActionMap(true);
      xr.LeftHand = LeftHand;
      var leftHandXrDevice = LeftHand._xrDevice;
      leftHandXrDevice.m_Initialized = true;
      LeftHand._xrDevice = leftHandXrDevice;

      RightHand = new HandActionMap(true);
      xr.RightHand = RightHand;
      var rightHandXrDevice = RightHand._xrDevice;
      rightHandXrDevice.m_Initialized = true;
      RightHand._xrDevice = rightHandXrDevice;

      var characteristics = InputDeviceCharacteristics.Controller |
                            InputDeviceCharacteristics.TrackedDevice;
      LeftController._Characteristics_k__BackingField =
          characteristics | InputDeviceCharacteristics.Left;
      RightController._Characteristics_k__BackingField =
          characteristics | InputDeviceCharacteristics.Right;

      LeftController.Type = RightController.Type = XRControllerType.OculusTouch;
      LeftController._IsConnected_k__BackingField =
          RightController._IsConnected_k__BackingField = true;
      LeftController.Position = RightController.Position = Vector3.zero;
      LeftController.Rotation = RightController.Rotation = Quaternion.identity;

      Hmd.Refresh();
      LeftController.Refresh();
      RightController.Refresh();
      LeftHand.Refresh();
      RightHand.Refresh();

      mainCamera = Camera.main;
      mainCamera.cameraType = CameraType.SceneView;
      mainCamera.fieldOfView = 90f;

      isReady = true;
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

  [HarmonyPatch]
  internal static class XRApi_InitializeXRLoader {
    private const string STEAM_CLASS_NAME = "__c__DisplayClass50_0";
    private const string STEAM_METHOD_NAME = "_InitializeXRLoader_b__0";
    private const string OCULUS_CLASS_NAME = "__c";
    private const string OCULUS_METHOD_NAME = "_Initialize_b__45_0";

    [HarmonyTargetMethod]
    public static MethodBase TargetMethod() {
      var xrApi = typeof(XRApi);
      return xrApi.GetNestedType(STEAM_CLASS_NAME)
                 ?.GetMethod(STEAM_METHOD_NAME) ??
             xrApi.GetNestedType(OCULUS_CLASS_NAME)
                 ?.GetMethod(OCULUS_METHOD_NAME);
    }

    [HarmonyPrefix]
    public static bool Prefix(ref bool __result) {
      __result = true;
      return false;
    }
  }

  [HarmonyPatch(typeof(InputDevice), nameof(InputDevice.TryGetFeatureValue),
                [typeof(InputFeatureUsage<bool>), typeof(bool)],
                [ArgumentType.Normal, ArgumentType.Out])]
  internal static class XRDevice_IsPresent {
    [HarmonyPrefix]
    private static bool Prefix(InputFeatureUsage<bool> usage, out bool value) {
      MelonLogger.Msg("Requesting Boolean Feature: " + usage.name);
      value = usage.name == "UserPresence";
      return false;
    }
  }

  [HarmonyPatch(typeof(SLZ.Marrow.Input.XRDevice),
                nameof(SLZ.Marrow.Input.XRDevice.IsTracking),
                MethodType.Getter)]
  internal static class XRDevice_IsTracking {
    [HarmonyPrefix]
    public static bool Prefix(ref bool __result) {
      __result = true;
      return false;
    }
  }

  [HarmonyPatch(typeof(InputSubsystemManager),
                nameof(InputSubsystemManager.HasFocus))]
  internal static class InputSubsystemManager_HasFocus {
    [HarmonyPrefix]
    public static bool Prefix(ref bool __result) {
      __result = true;
      return false;
    }
  }
}
