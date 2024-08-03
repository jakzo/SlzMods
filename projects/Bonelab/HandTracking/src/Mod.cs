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
using SLZ.Interaction;
using SLZ.Rig;
using System.Collections.Generic;
using SLZ.Marrow.Interaction;
using SLZ.Vehicle;
using SLZ.VRMK;

namespace Sst.HandTracking;

public class Mod : MelonMod {
  public static Mod Instance;

  private MelonPreferences_Entry<LocomotionType> _prefLoco;
  private MelonPreferences_Entry<bool> _prefForwardsOnly;
  private HandTracker[] _trackers = { null, null };
  private Locomotion _locoState;
  private Jumping _jumping;
  private Inventory _inventory;
  private HashSet<LaserCursor> _visibleLaserCursors = new();

  public HandTracker TrackerLeft {
    get => _trackers[0];
    set => _trackers[0] = value;
  }
  public HandTracker TrackerRight {
    get => _trackers[1];
    set => _trackers[1] = value;
  }

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);
    Instance = this;

    var category = MelonPreferences.CreateCategory(BuildInfo.NAME);
    _prefLoco = category.CreateEntry(
        "locomotion_type", LocomotionType.HEAD, "Locomotion type",
        "How running is performed (either \"HEAD\" or \"HANDS\")"
    );
    _prefForwardsOnly = category.CreateEntry(
        "forwards_only", true, "Locomotion forwards only",
        "Locks movement to forwards only (no strafing)"
    );
    _prefLoco.OnEntryValueChanged.Subscribe(
        (newValue, prevValue) => SetupLocomotion(newValue)
    );
    SetupLocomotion(_prefLoco.Value);

    LevelHooks.OnLoad += nextLevel => _visibleLaserCursors.Clear();
  }

  private void SetupLocomotion(LocomotionType type) {
    switch (type) {
    case LocomotionType.HANDS:
      _locoState = new HandLocomotion();
      break;

    case LocomotionType.HEAD:
    default:
      _locoState = new HeadLocomotion(_prefForwardsOnly);
      break;
    }

    if (TrackerLeft != null)
      _locoState.Init(TrackerLeft);
    if (TrackerRight != null)
      _locoState.Init(TrackerRight);
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
        ovrHand = OVRInput.Controller.LHand,
        handRotationOffset = Quaternion.Euler(0f, 90f, 0f) *
            Quaternion.Euler(0f, 0f, 95f) * Quaternion.Euler(345f, 0f, 0f),
        handPositionOffset = new Vector3(0.04f, 0.02f, 0.1f),
      });
      _locoState.Init(TrackerLeft);
    }

    if (TrackerRight == null && MarrowGame.xr.RightController != null) {
      TrackerRight = new(new() {
        isLeft = false,
        marrowController = MarrowGame.xr.RightController,
        setMarrowController = c => MarrowGame.xr.RightController = c,
        ovrController = OVRInput.Controller.RTouch,
        ovrHand = OVRInput.Controller.RHand,
        handRotationOffset = Quaternion.Euler(275f, 0f, 0f) *
            Quaternion.Euler(0f, 270f, 0f) * Quaternion.Euler(345f, 0f, 0f),
        handPositionOffset = new Vector3(-0.04f, 0.02f, 0.1f),
      });
      _locoState.Init(TrackerRight);
    }

    // TODO: Can we do the updates right before the inputs are used?
    TrackerLeft.OnUpdate();
    TrackerRight.OnUpdate();

    // TODO: Do this on fixed update or somewhere frame rate independent
    _locoState.Update();

    if (_jumping == null) {
      if (TrackerLeft != null && TrackerRight != null) {
        _jumping = new Jumping();
        _jumping.Init();
      }
    } else {
      _jumping.Update();
    }

    if (_inventory == null) {
      _inventory = new Inventory();
    }

    var nonDominantTracker =
        Utils.IsLocoControllerLeft() ? TrackerRight : TrackerLeft;
    nonDominantTracker.ProxyController.Joystick2DAxis = new Vector2(
        0f,
        TrackerRight.PedalInput.HasValue ? TrackerRight.PedalInput.Value > 0f
                ? TrackerRight.PedalInput ?? 0f
                : TrackerLeft.PedalInput - 1f ?? 0f
                                         : 0f
    );
  }

#if DEBUG
  public override void OnSceneWasInitialized(int buildindex, string sceneName) {
    if (!sceneName.ToUpper().Contains("BOOTSTRAP"))
      return;
    AssetWarehouse.OnReady(new Action(() => {
      var crate = AssetWarehouse.Instance.GetCrates().ToArray().First(
          c => c.Barcode.ID == Levels.Barcodes.MONOGON_MOTORWAY
      );
      var bootstrapper =
          GameObject.FindObjectOfType<SceneBootstrapper_Bonelab>();
      var crateRef = new LevelCrateReference(crate.Barcode.ID);
      bootstrapper.VoidG114CrateRef = crateRef;
      bootstrapper.MenuHollowCrateRef = crateRef;
    }));
  }
#endif

  internal HandTracker GetTrackerFromProxyController(XRController controller) {
    if (TrackerLeft.ProxyController.Equals(controller))
      return TrackerLeft;
    if (TrackerRight.ProxyController.Equals(controller))
      return TrackerRight;
    return null;
  }

  public enum LocomotionType { HEAD, HANDS }

  // TODO: Is there no way to make a ProxyController class with its own
  // Refresh?
  [HarmonyPatch(
      typeof(ControllerActionMap), nameof(ControllerActionMap.Refresh)
  )]
  internal static class ControllerActionMap_Refresh {
    [HarmonyPrefix]
    private static bool Prefix(ControllerActionMap __instance) {
      var tracker = Instance.GetTrackerFromProxyController(__instance);
      if (tracker == null || !tracker.IsTracking)
        return true;

      tracker.UpdateProxyController(Instance._locoState.Axis);
      Instance._inventory?.OnHandUpdate(tracker);
      return false;
    }
  }

  [HarmonyPatch(typeof(OpenController), nameof(OpenController.ProcessFingers))]
  internal static class OpenController_ProcessFingers {
    [HarmonyPrefix]
    private static bool Prefix(OpenController __instance) {
      var tracker = Mod.Instance.GetTrackerFromProxyController(
          Utils.XrControllerOf(__instance)
      );
      if (tracker == null || !tracker.IsTracking)
        return true;

      tracker.OnOpenControllerProcessFingers(__instance);
      return false;
    }
  }

  [HarmonyPatch(typeof(LaserCursor), nameof(LaserCursor.ShowCursor))]
  internal static class LaserCursor_ShowCursor {
    [HarmonyPostfix]
    private static void Postfix(LaserCursor __instance) {
      // TODO: Point laser pointer in direction of hand
      if (!__instance.cursorHidden)
        Instance._visibleLaserCursors.Add(__instance);
    }
  }

  [HarmonyPatch(typeof(LaserCursor), nameof(LaserCursor.HideCursor))]
  internal static class LaserCursor_HideCursor {
    [HarmonyPostfix]
    private static void Postfix(LaserCursor __instance) {
      if (__instance.cursorHidden)
        Instance._visibleLaserCursors.Remove(__instance);
    }
  }

  [HarmonyPatch(typeof(LaserCursor), nameof(LaserCursor.Update))]
  internal static class LaserCursor_Update {
    [HarmonyPrefix]
    private static void Prefix(LaserCursor __instance, ref bool __state) {
      var pinchedTracker =
          Instance._trackers.FirstOrDefault(t => t?.PinchUp ?? false);
      if (pinchedTracker == null)
        return;

      // Show laser pointer from the hand which is pinching
      var controller = Utils.RigControllerOf(pinchedTracker);
      if (!controller)
        return;

      Control_UI_InGameData.SetActiveController(controller);
      __instance.controllerFocused = true;
      __state = true;
    }

    [HarmonyPostfix]
    private static void Postfix(LaserCursor __instance, ref bool __state) {
      if (!__state)
        return;

      __instance.Trigger();
      UIRig.Instance?.popUpMenu?.Trigger(true);
    }
  }

  [HarmonyPatch(typeof(Seat), nameof(Seat.OnTriggerStay))]
  internal static class Seat_OnTriggerStay {
    [HarmonyPrefix]
    private static bool Prefix(Seat __instance, Collider other) {
      var crouchHand = Utils.IsLocoControllerLeft() ? Instance.TrackerRight
                                                    : Instance.TrackerLeft;
      if (crouchHand.IsControllerConnected() || __instance.rigManager)
        return true;

      var physicsRig = other?.GetComponent<PhysGrounder>()?.physRig;
      if (physicsRig == null)
        return true;

      if (!physicsRig.manager.activeSeat &&
          physicsRig.pelvisHeightMult < 0.6f) {
        __instance.IngressRig(physicsRig.manager);
      }
      return false;
    }
  }
}

public static class Utils {
  public static bool IsLocoControllerLeft() =>
      UIRig.Instance?.controlPlayer?.body_vitals?.isRightHanded ?? true;

  public static Vector3 FromFlippedXVector3f(OVRPlugin.Vector3f vector
  ) => new Vector3(-vector.x, vector.y, vector.z);

  public static Vector3 FromFlippedZVector3f(OVRPlugin.Vector3f vector
  ) => new Vector3(vector.x, vector.y, -vector.z);

  public static Quaternion FromFlippedXQuatf(OVRPlugin.Quatf quat
  ) => new Quaternion(quat.x, -quat.y, -quat.z, quat.w);

  public static XRController XrControllerOf(BaseController controller
  ) => controller.handedness == Handedness.LEFT ? MarrowGame.xr.LeftController
      : controller.handedness == Handedness.RIGHT
      ? MarrowGame.xr.RightController
      : null;

  public static BaseController RigControllerOf(HandTracker tracker) {
    var controllerRig = LevelHooks.RigManager?.ControllerRig;
    if (controllerRig == null)
      return null;
    return tracker.Opts.isLeft ? controllerRig.leftController
                               : controllerRig.rightController;
  }

  [HarmonyPatch(typeof(ForcePullGrip), nameof(ForcePullGrip.Pull))]
  internal static class ForcePullGrip_Pull {
    private static bool _enableForcePull = false;

    public static void Call(ForcePullGrip grip, Hand hand) {
      _enableForcePull = true;
      grip.Pull(hand);
      _enableForcePull = false;
    }

    [HarmonyPrefix]
    private static bool Prefix(ForcePullGrip __instance, Hand hand) {
      if (_enableForcePull ||
          Mod.Instance.GetTrackerFromProxyController(
              XrControllerOf(hand.Controller)
          ) == null)
        return true;

      Dbg.Log("ForcePullGrip.Pull was called but is disabled");
      __instance._pullToHand = null;
      return _enableForcePull;
    }
  }
}
