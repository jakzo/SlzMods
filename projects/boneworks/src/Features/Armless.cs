using MelonLoader;
using UnityEngine;
using StressLevelZero.Rig;
using System.Collections.Generic;

namespace Sst.Features {
public class Armless : Feature {
  private static string DUMMY_PALM_NAME = "SpeedrunTools_Armless_DummyPalm";

  public static bool IsLeftArmEnabled = true;
  public static bool IsRightArmEnabled = true;
  public static bool IsLeftControllerEnabled = true;
  public static bool IsRightControllerEnabled = true;

  public Armless() {
    // One controller
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) =>
          Mod.GameState.currentSceneIdx != Utils.SCENE_MENU_IDX &&
          Utils.GetKeyControl() && Input.GetKey(KeyCode.O),
      Handler =
          () => {
            if (!IsLeftArmEnabled) {
              MelonLogger.Msg("Enabling controllers");
              SetArmsEnabled(true, true, true);
            } else if (!IsRightArmEnabled) {
              MelonLogger.Msg("Disabling left controller");
              SetArmsEnabled(false, true, true);
            } else {
              MelonLogger.Msg("Disabling right controller");
              SetArmsEnabled(true, false, true);
            }
          },
    });

    // Armless
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) =>
          Mod.GameState.currentSceneIdx != Utils.SCENE_MENU_IDX &&
          Utils.GetKeyControl() && Input.GetKey(KeyCode.A),
      Handler =
          () => {
            if (IsLeftArmEnabled && IsRightArmEnabled) {
              MelonLogger.Msg("Disabling arms");
              SetArmsEnabled(false, false, false);
            } else {
              MelonLogger.Msg("Enabling arms");
              SetArmsEnabled(true, true, false);
            }
          },
    });
  }

  public override void OnDisabled() { SetArmsEnabled(true, true, true); }

  public override void OnSceneWasInitialized(int buildIndex,
                                             string sceneName) =>
      OnSceneWasInitializedStatic(buildIndex, sceneName);
  public static void OnSceneWasInitializedStatic(int buildIndex,
                                                 string sceneName) {
    if (buildIndex == Utils.SCENE_MENU_IDX)
      return;
    if (!IsLeftArmEnabled)
      SetArmEnabled(true, false);
    if (!IsRightArmEnabled)
      SetArmEnabled(false, false);
  }

  public static void SetArmsEnabled(bool isLeftArmEnabled,
                                    bool isRightArmEnabled,
                                    bool alsoSetControllers) {
    SetArmEnabled(true, isLeftArmEnabled);
    SetArmEnabled(false, isRightArmEnabled);
    IsLeftControllerEnabled = !alsoSetControllers || isLeftArmEnabled;
    IsRightControllerEnabled = !alsoSetControllers || isRightArmEnabled;
  }

  public static void SetArmEnabled(bool leftArm, bool isEnabled) {
    if (Mod.GameState.rigManager == null) {
      if (!isEnabled)
        Utils.LogDebug("Rig manager not found. Not disabling arm.");
      return;
    }

    if (leftArm)
      IsLeftArmEnabled = isEnabled;
    else
      IsRightArmEnabled = isEnabled;

    var physicsRig = Mod.GameState.rigManager.physicsRig;
    var hand = leftArm ? physicsRig.leftHand : physicsRig.rightHand;
    hand.GetComponent<Collider>().enabled = isEnabled;
    foreach (var collider in hand.transform.GetComponentsInChildren<Collider>())
      collider.enabled = isEnabled;
    var armTransform = Utilities.Unity.FindDescendantTransform(
        Mod.GameState.rigManager.gameWorldSkeletonRig.gameObject.transform,
        leftArm ? "l_AC_AuxSHJnt" : "r_AC_AuxSHJnt");
    if (armTransform == null)
      Utils.LogDebug("Arm bone not found");
    else
      armTransform.localScale = isEnabled ? Vector3.one : Vector3.zero;
    var dummyPalm = GameObject.Find(DUMMY_PALM_NAME);
    if (dummyPalm == null) {
      dummyPalm = new GameObject(DUMMY_PALM_NAME);
      dummyPalm.transform.SetParent(physicsRig.transform);
      dummyPalm.transform.localPosition = new Vector3(0, -99999, 0);
    }
    hand.palmPositionTransform = isEnabled
                                     ? hand.transform.FindChild("PalmCenter")
                                     : dummyPalm.transform;
  }

  private static bool IsControllerEnabled(Controller controller) =>
      controller.handedness == StressLevelZero.Handedness.LEFT
          ? IsLeftControllerEnabled
      : controller.handedness == StressLevelZero.Handedness.RIGHT
          ? IsRightControllerEnabled
          : false;

  public static void OnCacheInputs(Controller controller) {
    if (!IsControllerEnabled(controller) &&
        Mod.GameState.currentSceneIdx != Utils.SCENE_MENU_IDX)
      CancelInputs(controller);
  }
  public static void CancelInputs(Controller controller) {
    Features.Gripless.CancelGrip(controller);
    controller._aButton = false;
    controller._aButtonDown = false;
    controller._aButtonUp = false;
    controller._thumbstick = false;
    controller._thumbstickDown = false;
    controller._thumbstickUp = false;
    controller._thumbstickAxis = Vector2.zero;
  }

  public static void OnProcessFingers(Controller controller) {
    if (!IsControllerEnabled(controller) &&
        Mod.GameState.currentSceneIdx != Utils.SCENE_MENU_IDX)
      Features.Gripless.CancelProcessFingers(controller);
  }

  public static void OnSolveGrip(Controller controller) {
    if (!IsControllerEnabled(controller) &&
        Mod.GameState.currentSceneIdx != Utils.SCENE_MENU_IDX)
      Features.Gripless.OnSolveGrip(controller);
  }
}
}
