using MelonLoader;
using System;
using System.Collections.Generic;
using SLZ;
using SLZ.Marrow.Warehouse;
using SLZ.Rig;
using UnityEngine;
using HarmonyLib;
using SLZ.Marrow.Input;
using SLZ.Marrow.Utilities;
using Sst.Utilities;

namespace Sst.PancakeMode {
public class Mod : MelonMod {
  public override void OnInitializeMelon() { Dbg.Init(BuildInfo.NAME); }

  public override void OnUpdate() {}

  [HarmonyPatch(typeof(OpenController), nameof(OpenController.CacheInputs))]
  class OpenController_CacheInputs_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(OpenController __instance) {
      var cl = __instance.handedness == Handedness.LEFT ? __instance : null;
      var cr = __instance.handedness == Handedness.RIGHT ? __instance : null;

      if (cl) {
        if (Input.GetKey(KeyCode.W))
          cl._thumbstickAxis += Vector2.up;
        if (Input.GetKey(KeyCode.S))
          cl._thumbstickAxis += Vector2.down;
        if (Input.GetKey(KeyCode.A))
          cl._thumbstickAxis += Vector2.left;
        if (Input.GetKey(KeyCode.D))
          cl._thumbstickAxis += Vector2.right;

        if (Input.GetKey(KeyCode.LeftShift))
          cl._thumbstick = true;
        if (Input.GetKeyDown(KeyCode.LeftShift))
          cl._thumbstickDown = true;
        if (Input.GetKeyUp(KeyCode.LeftShift))
          cl._thumbstickUp = true;
      }

      if (cr) {
        if (Input.GetKey(KeyCode.Space))
          cr._aButton = true;
        if (Input.GetKeyDown(KeyCode.Space))
          cr._aButtonDown = true;
        if (Input.GetKeyUp(KeyCode.Space))
          cr._aButtonUp = true;

        if (LevelHooks.RigManager) {
          cr._thumbstickAxis +=
              Input.GetKey(KeyCode.LeftControl) ? Vector2.down
              : LevelHooks.RigManager.remapHeptaRig._crouchTarget < 0f
                  ? Vector2.up
                  : Vector2.zero;
        }
      }

      // if (CurrentMovement == null || !Instance.IsEnabled)
      //   return;

      // var leftController =
      // Utils.State.rigManager.ControllerRig.leftController;
      // leftController._aButton = false;
      // leftController._aButtonDown = false;
      // leftController._aButtonUp = false;

      // if (CurrentMovementEnumerator == null) {
      //   startTime = Time.time;
      //   CurrentMovement.time = 0f;
      //   CurrentMovementEnumerator = CurrentMovement.Start();
      // } else {
      //   CurrentMovement.time = Time.time - startTime;
      //   if (!CurrentMovementEnumerator.MoveNext()) {
      //     CurrentMovement = null;
      //     CurrentMovementEnumerator = null;
      //   }
      // }
    }
  }

  [HarmonyPatch(typeof(Control_GlobalTime), nameof(Control_GlobalTime.PAUSE))]
  class Control_GlobalTime_PAUSE_Patch {
    [HarmonyPostfix()]
    internal static bool Prefix() => false;
  }

  [HarmonyPatch(typeof(Control_GlobalTime), nameof(Control_GlobalTime.UNPAUSE))]
  class Control_GlobalTime_UNPAUSE_Patch {
    [HarmonyPostfix()]
    internal static bool Prefix() => false;
  }

  // TODO: Fake XR init which happens in these methods below
  // MarrowGame.Initialize() -> MarrowGame.TryInitializeXRApi() ->
  //   XRApi.Initialize() -> XRApi.InitializeXRLoader() -> "XR init failed"

  // [HarmonyPatch(typeof(XRApi), nameof(XRApi.Initialize))]
  // class XRApi_Initialize_Patch {
  //   [HarmonyPrefix()]
  //   internal static void Prefix() { Dbg.Log("XRApi.Initialize Prefix"); }

  //   [HarmonyPostfix()]
  //   internal static void Postfix() { Dbg.Log("XRApi.Initialize Postfix"); }

  //   [HarmonyFinalizer()]
  //   internal static void Finalizer(Exception __exception) {
  //     Dbg.Log(
  //         $"XRApi.Initialize Finalizer {__exception != null}:
  //         {__exception}");
  //   }
  // }

  // [HarmonyPatch(typeof(XRApi), nameof(XRApi.InitializeXRLoader))]
  // class XRApi_InitializeXRLoader_Patch {
  //   [HarmonyPrefix()]
  //   internal static void Prefix() { MarrowGame.xr.Settings.m_Loaders.Clear();
  //   }
  // }

  // [HarmonyPatch(typeof(MarrowGame), nameof(MarrowGame.TryInitializeXRApi))]
  // class MarrowGame_TryInitializeXRApi_Patch {
  //   [HarmonyPrefix()]
  //   internal static void Prefix() {
  //     Dbg.Log("MarrowGame.TryInitializeXRApi Prefix");
  //   }

  //   [HarmonyPostfix()]
  //   internal static void Postfix() {
  //     Dbg.Log("MarrowGame.TryInitializeXRApi Postfix");
  //   }

  //   [HarmonyFinalizer()]
  //   internal static void Finalizer(Exception __exception) {
  //     Dbg.Log(
  //         $"MarrowGame.TryInitializeXRApi Finalizer {__exception != null}:
  //         {__exception}");
  //     // MarrowGame.xr.Initialize();
  //   }
  // }

  [HarmonyPatch(typeof(MarrowGame), nameof(MarrowGame.Initialize))]
  class MarrowGame_Initialize_Patch {
    [HarmonyPrefix()]
    internal static void Prefix() { Dbg.Log("MarrowGame.Initialize Prefix"); }

    [HarmonyPostfix()]
    internal static void Postfix() { Dbg.Log("MarrowGame.Initialize Postfix"); }

    [HarmonyFinalizer()]
    internal static void Finalizer(Exception __exception) {
      Dbg.Log(
          $"MarrowGame.Initialize Finalizer {__exception != null}: {__exception}");
    }
  }
}
}
