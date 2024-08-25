using System;
using System.Linq;
using MelonLoader;
using UnityEngine;
using SLZ.Marrow.Utilities;
using Sst.Utilities;
using SLZ.Bonelab;
using SLZ.Marrow.Warehouse;
using HarmonyLib;
using SLZ.Marrow.Input;
using SLZ.Marrow.Interaction;

namespace Sst.HandTracking;

public class Mod : MelonMod {
  public static Mod Instance;
  public static Preferences Preferences;

  private HandLocomotion _handLoco;
  private HeadLocomotion _headLoco;
  private Jumping _jumping;
  private Vehicles _vehicles;
  private WeaponButtonDetector _weaponButtonLeft;
  private WeaponButtonDetector _weaponButtonRight;

  public NooseHelper NooseHelper;
  public HandTracker[] Trackers = { null, null };
  public HandTracker TrackerLeft {
    get => Trackers[0];
    set => Trackers[0] = value;
  }
  public HandTracker TrackerRight {
    get => Trackers[1];
    set => Trackers[1] = value;
  }

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);
    Instance = this;

    Preferences.Init();
    Preferences = new();
    Preferences.WeaponRotationOffset.OnEntryValueChanged.Subscribe(
        (a, b) => SetWeaponRotationOffsets()
    );

    _handLoco = new();
    _headLoco = new();
    NooseHelper = new();
  }

  public override void OnUpdate() {
    if (!MarrowGame.IsInitialized || MarrowGame.xr == null)
      return;

    if (TrackerLeft == null && MarrowGame.xr.LeftController != null) {
      TrackerLeft = new(new() {
        isLeft = true,
        marrowController = MarrowGame.xr.LeftController,
        setMarrowController = c => MarrowGame.xr.LeftController = c,
        ovrController = OVRInput.Controller.LTouch,
        handRotationOffset = Quaternion.Euler(0f, 90f, 0f) *
            Quaternion.Euler(0f, 0f, 95f) * Quaternion.Euler(345f, 0f, 0f),
        handPositionOffset = new Vector3(0.04f, 0.02f, 0.1f),
        getLocoAxis = GetLocoAxis,
        getWeaponButtonPressed = () =>
            _weaponButtonLeft?.IsTriggered() ?? false,
      });
      SetWeaponRotationOffsets();
      _handLoco.Init(TrackerLeft);
      _headLoco.Init(TrackerLeft);
    }

    if (TrackerRight == null && MarrowGame.xr.RightController != null) {
      TrackerRight = new(new() {
        isLeft = false,
        marrowController = MarrowGame.xr.RightController,
        setMarrowController = c => MarrowGame.xr.RightController = c,
        ovrController = OVRInput.Controller.RTouch,
        handRotationOffset = Quaternion.Euler(275f, 0f, 0f) *
            Quaternion.Euler(0f, 270f, 0f) * Quaternion.Euler(345f, 0f, 0f),
        handPositionOffset = new Vector3(-0.04f, 0.02f, 0.1f),
        getLocoAxis = GetLocoAxis,
        getWeaponButtonPressed = () =>
            _weaponButtonRight?.IsTriggered() ?? false,
      });
      SetWeaponRotationOffsets();
      _handLoco.Init(TrackerRight);
      _headLoco.Init(TrackerRight);
    }

    if (_vehicles == null && TrackerLeft != null && TrackerRight != null) {
      _vehicles = new(TrackerLeft, TrackerRight);
      _weaponButtonLeft = new(TrackerLeft, TrackerRight);
      _weaponButtonRight = new(TrackerRight, TrackerLeft);
    }

    // TODO: Can we do the updates right before the inputs are used?
    TrackerLeft.OnUpdate();
    TrackerRight.OnUpdate();

    // TODO: Do this on fixed update or somewhere frame rate independent
    if (!IsLocoControllerTracked()) {
      if (Preferences.HandLoco.Value)
        _handLoco.Update();
      if (Preferences.HeadLoco.Value)
        _headLoco.Update();
    }

    if (_jumping == null) {
      if (TrackerLeft != null && TrackerRight != null) {
        _jumping = new Jumping();
        _jumping.Init();
      }
    } else {
      _jumping.Update();
    }

    _vehicles.Update();
    NooseHelper.Update();
  }

  private bool IsLocoControllerTracked() {
    if (TrackerLeft == null || TrackerRight == null)
      return false;
    var locoTracker = Utils.IsLocoControllerLeft() ? TrackerLeft : TrackerRight;
    return locoTracker.IsControllerConnected();
  }

  private Vector2 GetLocoAxis() {
    if (IsLocoControllerTracked())
      return Vector2.zero;

    var handLocoAxis =
        Preferences.HandLoco.Value ? _handLoco.Axis : Vector2.zero;
    var headLocoAxis =
        Preferences.HeadLoco.Value ? _headLoco.Axis : Vector2.zero;
    return Utils.Clamp01(handLocoAxis + headLocoAxis);
  }

  private void SetWeaponRotationOffsets() {
    foreach (var tracker in Trackers) {
      tracker?.SetWeaponRotationOffset(Preferences.WeaponRotationOffset.Value);
    }
  }

  public HandTracker GetTrackerOfHand(Handedness handedness
  ) => handedness == Handedness.LEFT   ? TrackerLeft
      : handedness == Handedness.RIGHT ? TrackerRight
                                       : null;

  internal HandTracker GetTrackerFromProxyController(XRController controller) {
    if (TrackerLeft.ProxyController.Equals(controller))
      return TrackerLeft;
    if (TrackerRight.ProxyController.Equals(controller))
      return TrackerRight;
    return null;
  }

#if DEBUG
  public override void OnSceneWasInitialized(int buildindex, string sceneName) {
    if (!sceneName.ToUpper().Contains("BOOTSTRAP"))
      return;

    AssetWarehouse.OnReady(new Action(() => {
      var crate = AssetWarehouse.Instance.GetCrates().ToArray().First(
          c => c.Barcode.ID == Levels.Barcodes.HUB
      );
      var bootstrapper =
          GameObject.FindObjectOfType<SceneBootstrapper_Bonelab>();
      var crateRef = new LevelCrateReference(crate.Barcode.ID);
      bootstrapper.VoidG114CrateRef = crateRef;
      bootstrapper.MenuHollowCrateRef = crateRef;
    }));
  }
#endif

  public bool IsControllerConnected() => TrackerLeft.IsControllerConnected() ||
      TrackerRight.IsControllerConnected();

  public enum LocomotionType { HEAD, HANDS }

  [HarmonyPatch(
      typeof(ControllerActionMap), nameof(ControllerActionMap.Refresh)
  )]
  internal static class ControllerActionMap_Refresh {
    [HarmonyPrefix]
    private static bool Prefix(ControllerActionMap __instance) {
      var tracker = Instance.GetTrackerFromProxyController(__instance);
      if (tracker == null || !tracker.IsTracking)
        return true;

      tracker.UpdateProxyController();
      return false;
    }
  }

  [HarmonyPatch(typeof(TutorialTrigger), nameof(TutorialTrigger.TUTORIALSEND))]
  internal static class TutorialTrigger_TUTORIALSEND {
    [HarmonyPrefix]
    private static bool
    Prefix() => (Instance.TrackerLeft?.IsControllerConnected() ?? true) ||
        (Instance.TrackerRight?.IsControllerConnected() ?? true);
  }
}
