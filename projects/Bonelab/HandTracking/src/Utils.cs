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

  public static Vector3 FromFlippedZVector3f(OVRPlugin.Vector3f vector
  ) => new Vector3(vector.x, vector.y, -vector.z);

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
}

public class PatchBypass {
  private bool _isEnabled = false;
  public bool
  IsCallable() => _isEnabled || Mod.Instance.IsControllerConnected();

  public void Enable(Action callback) {
    _isEnabled = true;
    try {
      callback();
    } finally {
      _isEnabled = false;
    }
  }
}
