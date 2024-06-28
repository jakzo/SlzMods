using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Valve.VR;
using StressLevelZero;
using StressLevelZero.Props;
using StressLevelZero.Pool;
using StressLevelZero.Rig;
using Sst.Utilities;

namespace Sst.FlatPlayer;

public class Mod : MelonMod {
  public static Mod Instance;

  private FlatMode _flatMode;

  public override void OnApplicationStart() {
    Dbg.Init(BuildInfo.NAME);
    Instance = this;

    var prefCategory = MelonPreferences.CreateCategory(BuildInfo.NAME);
    var prefCameraSpeed = prefCategory.CreateEntry("CameraSensitivity", 0.8f);

    _flatMode = new FlatMode(prefCameraSpeed);

    // TODO: Do we need to stop the app from initializing VR?
  }

  // [HarmonyPatch(typeof(ControllerRig), nameof(ControllerRig.OnEnable))]
  // internal static class ControllerRig_OnEnable {
  //   [HarmonyPrefix]
  //   private static void Prefix() { Instance._flatMode.Start(); }
  // }

  // public override void OnSceneWasInitialized(int buildIndex, string
  // sceneName) {
  //   _flatMode.Start();
  // }

  public override void BONEWORKS_OnLoadingScreen() { _flatMode.Stop(); }

  public override void OnUpdate() { _flatMode.OnUpdate(); }

  private static void StartIfNecessary() {
    if (!Instance._flatMode.isReady)
      Instance._flatMode.Start();
  }

  // [HarmonyPatch(typeof(SteamControllerRig),
  //               nameof(SteamControllerRig.OnEarlyUpdate))]
  // internal static class SteamControllerRig_OnEarlyUpdate {
  //   [HarmonyPrefix]
  //   private static void Prefix(SteamControllerRig __instance) {
  //     StartIfNecessary();
  //     __instance._runFixedUpdates = true;
  //   }
  // }
  // [HarmonyPatch(typeof(SteamVR_Input),
  //               nameof(SteamVR_Input.UpdateNonVisualActions))]
  // internal static class SteamVR_Input_UpdateNonVisualActions {
  //   [HarmonyPrefix]
  //   private static bool Prefix() { return false; }
  // }

  // [HarmonyPatch(typeof(SteamControllerRig),
  //               nameof(SteamControllerRig.OnUpdate))]
  // internal static class SteamControllerRig_OnUpdate {
  //   [HarmonyPrefix]
  //   private static bool Prefix(SteamControllerRig __instance) {
  //     CallBaseControllerRigMethod(__instance, "OnUpdate");
  //     return false;
  //   }
  // }
  // [HarmonyPatch(typeof(SteamVR_Input), "get_isStartupFrame")]
  // internal static class SteamVR_Input_isStartupFrame {
  //   [HarmonyPrefix]
  //   private static bool Prefix(out bool __result) {
  //     __result = false;
  //     return false;
  //   }
  // }

  // [HarmonyPatch(typeof(SteamControllerRig),
  //               nameof(SteamControllerRig.OnFixedUpdate))]
  // internal static class SteamControllerRig_OnFixedUpdate {
  //   [HarmonyPrefix]
  //   private static bool Prefix(SteamControllerRig __instance) {
  //     CallBaseControllerRigMethod(__instance, "OnFixedUpdate");
  //     return false;
  //   }
  // }

  // [HarmonyPatch(typeof(ControllerRig), nameof(ControllerRig.OnFixedUpdate))]
  // internal static class ControllerRig_OnFixedUpdate {
  //   [HarmonyPrefix]
  //   private static void Prefix() {
  //     StartIfNecessary();
  //     Instance._flatMode.UpdateHmd();
  //   }
  // }

  [HarmonyPatch(typeof(Controller), nameof(Controller.CacheInputs))]
  internal static class Controller_CacheInputs {
    [HarmonyPrefix]
    private static bool Prefix(Controller __instance) {
      StartIfNecessary();
      Instance._flatMode.UpdateController(__instance);
      return false;
    }
  }

  [HarmonyPatch(typeof(Controller), nameof(Controller.OnVrFixedUpdate))]
  internal static class Controller_OnVrFixedUpdate {
    [HarmonyPrefix]
    private static bool Prefix(Controller __instance) {
      StartIfNecessary();
      Instance._flatMode.UpdateController(__instance);
      return false;
    }
  }
}
