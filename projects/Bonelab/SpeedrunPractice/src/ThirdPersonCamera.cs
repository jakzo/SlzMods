using UnityEngine;
using HarmonyLib;
using SLZ.Bonelab;

namespace Sst.SpeedrunPractice {
#if DEBUG
// This is only active in debug builds (and when spectator camera is
// enabled) to ease development
public class ThirdPersonCamera {
  private const float CAMERA_FOLLOW_DIST = 2f;
  private static int CAMERA_COLLISION_LAYER_MASK =
      LayerMask.GetMask(new string[] {
        "Default", "Static",
        "NoSelfCollide", // has things like doors but also some npc body parts
      });

  public static Vector3? _cameraNormalPos;

  [HarmonyPatch(typeof(SmoothFollower),
                nameof(SmoothFollower.MoveCameraUpdate))]
  class SmoothFollower_MoveCameraUpdate_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(SmoothFollower __instance) {
      __instance.RotationalSmoothTime = 10f;
      __instance.TranslationSmoothTime = 10f;
      if (_cameraNormalPos.HasValue)
        __instance.transform.localPosition = _cameraNormalPos.Value;
    }

    [HarmonyPostfix()]
    internal static void Postfix(SmoothFollower __instance) {
      var headPos = __instance.transform.position;
      _cameraNormalPos = __instance.transform.localPosition;

      __instance.transform.localPosition += __instance.transform.localRotation *
                                            Vector3.back * CAMERA_FOLLOW_DIST;
      if (Physics.Linecast(headPos, __instance.transform.position, out var hit,
                           CAMERA_COLLISION_LAYER_MASK)) {
        __instance.transform.position = hit.point;
      }
    }
  }
}
#endif
}
