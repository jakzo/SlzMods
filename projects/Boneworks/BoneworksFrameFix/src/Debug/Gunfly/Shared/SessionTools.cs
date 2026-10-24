using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using StressLevelZero.Rig;
using StressLevelZero.Utilities;
using Valve.VR;

namespace Sst.BoneworksFrameFix.Debug.Gunfly
{
  // Scene loading, controller-role compatibility and read-only inspection.
  public static class SessionTools
  {
    static long invalidDeviceQueries;
    static string FormatVector(Vector3 p) => p.x.ToString("R") + "," + p.y.ToString("R") + "," + p.z.ToString("R");
    public static string Inspect()
    {
      var s = new StringBuilder();
      try
      {
        s.AppendLine("scene=" + SceneManager.GetActiveScene().name);
        s.AppendLine("mods=" + string.Join(",", MelonLoader.MelonHandler.Mods.Select(m => m.Info.Name)));
        s.AppendLine("oldBenchmarkAssemblies=" + string.Join(",", AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).Where(n => n.StartsWith("GunflyMeasurements"))));
        s.AppendLine("experimentalHooks=" + string.Join(",", HarmonyLib.Harmony.GetAllPatchedMethods().SelectMany(m => HarmonyLib.Harmony.GetPatchInfo(m).Owners).Distinct().Where(o => o.StartsWith("jakzo.gunfly."))));
        s.AppendLine("XR=" + UnityEngine.XR.XRSettings.enabled + " device=" + UnityEngine.XR.XRSettings.loadedDeviceName + " pause=" + SteamVR_Render.pauseRendering);
        var rig = UnityEngine.Object.FindObjectOfType<RigManager>();
        if (!rig)
        {
          s.AppendLine("No active RigManager yet.");
          return s.ToString();
        }

        s.AppendLine("physicsHead=" + FormatVector(rig.physicsRig.m_head.position) + " FordSkull=" + FormatVector(rig.gameWorldSkeletonRig.body.references.skull.position));
        foreach (var c in rig.GetComponentsInChildren<Camera>(true))
        {
          if (c.enabled && c.gameObject.activeInHierarchy)
          {
            s.AppendLine("camera=" + c.name + " position=" + FormatVector(c.transform.position) + " eye=" + c.stereoTargetEye + " stereo=" + c.stereoEnabled);
            if (c.stereoEnabled)
            {
              var l = c.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse.MultiplyPoint(Vector3.zero);
              var r = c.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse.MultiplyPoint(Vector3.zero);
              s.AppendLine("renderedLeftEye=" + FormatVector(l) + " rightEye=" + FormatVector(r) + " stereoCenterOffsetFromCamera=" + FormatVector((l + r) * .5f - c.transform.position));
            }
          }
        }

        var lctrl = rig.ControllerRig.leftController.TryCast<StressLevelZero.Rig.Controller>();
        var rctrl = rig.ControllerRig.rightController.TryCast<StressLevelZero.Rig.Controller>();
        if (lctrl && rctrl)
        {
          s.AppendLine("nativeLeftAxis=" + lctrl._thumbstickAxis.x + "," + lctrl._thumbstickAxis.y + " leftClick=" + lctrl._thumbstick + " nativeRightAxis=" + rctrl._thumbstickAxis.x + "," + rctrl._thumbstickAxis.y);
        }
      } catch (Exception e)
      {
        s.AppendLine(e.ToString());
      }

      return s.ToString();
    }

    public static string LoadSandbox()
    {
      BoneworksSceneManager.LoadScene("sandbox_blankBox");
      return "Requested normal BlankBox through the native scene loader.";
    }

    public static string Devices()
    {
      var s = new StringBuilder();
      var rig = UnityEngine.Object.FindObjectOfType<RigManager>();
      var vr = OpenVR.System;
      s.AppendLine("roleLeft=" + vr.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand) + " roleRight=" + vr.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand));
      s.AppendLine("nativePoseArrayLength=" + rig.steamControllerRig.poses.Length);
      foreach (var c in rig.GetComponentsInChildren<StressLevelZero.Rig.Controller>(true))
      {
        s.AppendLine("controller=" + c.name + " hand=" + c.handedness + " cachedIndex=" + c.deviceIndex + " connected=" + (c.deviceIndex < 64 && vr.IsTrackedDeviceConnected(c.deviceIndex)));
      }

      for (uint i = 0; i < 64; i++)
      {
        if (vr.IsTrackedDeviceConnected(i))
        {
          var error = ETrackedPropertyError.TrackedProp_Success;
          var hint = vr.GetInt32TrackedDeviceProperty(i, ETrackedDeviceProperty.Prop_ControllerRoleHint_Int32, ref error);
          s.AppendLine("device=" + i + " class=" + vr.GetTrackedDeviceClass(i) + " role=" + vr.GetControllerRoleForTrackedDeviceIndex(i) + " roleHint=" + hint + " hintError=" + error);
        }
      }

      return s.ToString();
    }

    public static bool InvalidDevicePrefix(uint __0, ref bool __result)
    {
      if (__0 < 64)
      {
        return true;
      }

      invalidDeviceQueries++;
      __result = false;
      return false;
    }

    public static string GuardInvalidDevice()
    {
      var h = new HarmonyLib.Harmony("jakzo.boneworks.openvr-role-compatibility");
      h.Patch(HarmonyLib.AccessTools.Method(typeof(CVRSystem), "IsTrackedDeviceConnected"), prefix: new HarmonyLib.HarmonyMethod(typeof(SessionTools).GetMethod("InvalidDevicePrefix")));
      h.Patch(HarmonyLib.AccessTools.Method(typeof(CVRSystem), "GetTrackedDeviceIndexForControllerRole"), postfix: new HarmonyLib.HarmonyMethod(typeof(SessionTools).GetMethod("RoleIndexPostfix")));
      h.Patch(HarmonyLib.AccessTools.Method(typeof(CVRSystem), "GetControllerRoleForTrackedDeviceIndex"), postfix: new HarmonyLib.HarmonyMethod(typeof(SessionTools).GetMethod("DeviceRolePostfix")));
      return "Session-only OpenVR compatibility installed: invalid index guard and actual controller role-hint fallback. Poses, action data, camera and rig remain native.";
    }

    public static void RoleIndexPostfix(CVRSystem __instance, ETrackedControllerRole __0, ref uint __result)
    {
      if (__result < 64 || (__0 != ETrackedControllerRole.LeftHand && __0 != ETrackedControllerRole.RightHand))
      {
        return;
      }

      for (uint i = 0; i < 64; i++)
      {
        if (__instance.IsTrackedDeviceConnected(i) && __instance.GetTrackedDeviceClass(i) == ETrackedDeviceClass.Controller)
        {
          var error = ETrackedPropertyError.TrackedProp_Success;
          int hint = __instance.GetInt32TrackedDeviceProperty(i, ETrackedDeviceProperty.Prop_ControllerRoleHint_Int32, ref error);
          if (error == ETrackedPropertyError.TrackedProp_Success && hint == (int)__0)
          {
            __result = i;
            return;
          }
        }
      }
    }

    public static void DeviceRolePostfix(CVRSystem __instance, uint __0, ref ETrackedControllerRole __result)
    {
      if (__result != ETrackedControllerRole.Invalid || __0 >= 64 || !__instance.IsTrackedDeviceConnected(__0) || __instance.GetTrackedDeviceClass(__0) != ETrackedDeviceClass.Controller)
      {
        return;
      }

      var error = ETrackedPropertyError.TrackedProp_Success;
      int hint = __instance.GetInt32TrackedDeviceProperty(__0, ETrackedDeviceProperty.Prop_ControllerRoleHint_Int32, ref error);
      if (error == ETrackedPropertyError.TrackedProp_Success && (hint == (int)ETrackedControllerRole.LeftHand || hint == (int)ETrackedControllerRole.RightHand))
      {
        __result = (ETrackedControllerRole)hint;
      }
    }

    public static string GuardStatus() => "rejectedInvalidDeviceQueries=" + invalidDeviceQueries;
  }
}
