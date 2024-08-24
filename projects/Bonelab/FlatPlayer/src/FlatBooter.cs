using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Unity.XR.MockHMD;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR;
using Sst.Utilities;

#if PATCH5 && ML6
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Input;
using MarrowXrDevice = Il2CppSLZ.Marrow.Input.XRDevice;
#elif PATCH3 && ML5
using SLZ.Marrow.Input;
using SLZ.Marrow.Utilities;
using SLZ.Rig;
using SLZ.Marrow.Warehouse;
using SLZ.SaveData;
using MarrowXrDevice = SLZ.Marrow.Input.XRDevice;
#endif

namespace Sst.FlatPlayer;

public class FlatBooter : MelonMod {
  public static FlatBooter Instance;

  private FlatMode _flatMode;

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);
    Instance = this;

    var prefCategory = MelonPreferences.CreateCategory(BuildInfo.NAME);
    var prefCameraSpeed = prefCategory.CreateEntry("CameraSensitivity", 0.8f);

    _flatMode = new FlatMode(prefCameraSpeed);
    // LevelHooks.OnLevelStart += level => _flatMode.Start();
    // LevelHooks.OnLoad += nextLevel => _flatMode.Stop();

    var loaders = XRGeneralSettings.Instance.Manager.loaders;
    loaders.Clear();
    loaders.Add(ScriptableObject.CreateInstance<MockHMDLoader>());
  }

  public override void OnUpdate() { _flatMode.OnUpdate(); }

  public override void OnLateUpdate() {
    _flatMode.UpdateHmd();
    _flatMode.UpdateLeftController();
    _flatMode.UpdateRightController();
  }

  [HarmonyPatch(typeof(HmdActionMap), nameof(HmdActionMap.Refresh))]
  internal static class HmdActionMap_Refresh {
    [HarmonyPrefix]
    private static bool Prefix() => false;
  }

  [HarmonyPatch(
      typeof(ControllerActionMap), nameof(ControllerActionMap.Refresh)
  )]
  internal static class ControllerActionMap_Refresh {
    [HarmonyPrefix]
    private static bool Prefix() => false;
  }

  [HarmonyPatch(typeof(OpenControllerRig), nameof(OpenControllerRig.OnAwake))]
  internal static class OpenControllerAwake {
    [HarmonyPrefix]
    private static void Prefix(OpenControllerRig __instance) {
      // if (__instance.transform.parent.gameObject.name != "[RigManager
      // (Blank)]")
      //   return;

      Instance._flatMode.Start();
    }
  }

  [HarmonyPatch(typeof(OpenControllerRig), nameof(OpenControllerRig.OnDestroy))]
  internal static class OpenControllerDestroy {
    [HarmonyPrefix]
    private static void Prefix(OpenControllerRig __instance) {
      // if (__instance.transform.parent.gameObject.name == "[RigManager
      // (Blank)]")
      Instance._flatMode.Stop();
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
#if PATCH5 && ML6
      return typeof(XRApi.__c__DisplayClass60_0)
          .GetMethod(nameof(XRApi.__c__DisplayClass60_0._InitializeXRLoader_b__0
          ));
#elif PATCH3 && ML5
      var xrApi = typeof(XRApi);
      return xrApi.GetNestedType(STEAM_CLASS_NAME)
                 ?.GetMethod(STEAM_METHOD_NAME) ??
          xrApi.GetNestedType(OCULUS_CLASS_NAME)?.GetMethod(OCULUS_METHOD_NAME);
#endif
    }

    [HarmonyPrefix]
    public static bool Prefix(ref bool __result) {
      __result = true;
      return false;
    }
  }

  [HarmonyPatch(
      typeof(InputDevice), nameof(InputDevice.TryGetFeatureValue),
      new Type[] { typeof(InputFeatureUsage<bool>), typeof(bool) },
      new ArgumentType[] { ArgumentType.Normal, ArgumentType.Out }
  )]
  internal static class XRDevice_IsPresent {
    [HarmonyPrefix]
    private static bool Prefix(InputFeatureUsage<bool> usage, out bool value) {
#if PATCH5 && ML6
      value = true;
#elif PATCH3 && ML5
      value = usage.name == "UserPresence";
#endif
      return false;
    }
  }

  [HarmonyPatch(
      typeof(MarrowXrDevice), nameof(MarrowXrDevice.IsTracking),
      MethodType.Getter
  )]
  internal static class XRDevice_IsTracking {
    [HarmonyPrefix]
    public static bool Prefix(ref bool __result) {
      __result = true;
      return false;
    }
  }

  [HarmonyPatch(
      typeof(InputSubsystemManager), nameof(InputSubsystemManager.HasFocus)
  )]
  internal static class InputSubsystemManager_HasFocus {
    [HarmonyPrefix]
    public static bool Prefix(ref bool __result) {
      __result = true;
      return false;
    }
  }
}
