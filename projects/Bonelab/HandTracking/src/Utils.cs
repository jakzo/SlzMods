using System;
using UnityEngine;
using SLZ.Marrow.Utilities;
using Sst.Utilities;
using SLZ.Bonelab;
using HarmonyLib;
using SLZ.Marrow.Input;
using SLZ.Interaction;
using SLZ.Rig;
using SLZ.Marrow.Interaction;

namespace Sst.HandTracking;

public static class Utils {
  public static bool IsLocoControllerLeft() =>
      UIRig.Instance?.controlPlayer?.body_vitals?.isRightHanded ?? true;

  public static Vector3 FromFlippedXVector3f(OVRPlugin.Vector3f vector
  ) => new Vector3(-vector.x, vector.y, vector.z);

  public static Vector3 FromFlippedZVector3f(OVRPlugin.Vector3f vector
  ) => new Vector3(vector.x, vector.y, -vector.z);

  public static Quaternion FromFlippedXQuatf(OVRPlugin.Quatf quat
  ) => new Quaternion(quat.x, -quat.y, -quat.z, quat.w);

  public static Quaternion FromFlippedZQuatf(OVRPlugin.Quatf quat
  ) => new Quaternion(-quat.x, -quat.y, quat.z, quat.w);

  public static Quaternion FlipXY(Quaternion quat
  ) => new Quaternion(-quat.x, -quat.y, quat.z, quat.w);

  public static XRController XrControllerOf(BaseController controller
  ) => controller.handedness == Handedness.LEFT ? MarrowGame.xr.LeftController
      : controller.handedness == Handedness.RIGHT
      ? MarrowGame.xr.RightController
      : null;

  public static Vector2 Clamp01(Vector2 vector) => new Vector2(
      Mathf.Clamp(vector.x, -1f, 1f), Mathf.Clamp(vector.y, -1f, 1f)
  );

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
