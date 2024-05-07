using MelonLoader;
using System;
using System.Collections.Generic;
using SLZ.Marrow.Warehouse;
using SLZ.Rig;
using UnityEngine;
using HarmonyLib;

namespace Sst.PancakeMode {
public class Mod : MelonMod {
  private static RigManager _rigManager;

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);

    Utilities.LevelHooks.OnLevelStart += OnLevelStart;
  }

  private void OnLevelStart(LevelCrate level) {
    _rigManager = Utilities.Bonelab.GetRigManager();
  }

  public override void OnUpdate() {}

  private static bool
  GetKey(KeyCode key) => Input.GetKey(key) || Input.GetKeyDown(key);

  enum Handedness { LEFT, RIGHT }

  private static void MoveThumbstick(Handedness handedness, Vector2 delta) {
    var controller = handedness == Handedness.LEFT
                         ? _rigManager.ControllerRig.leftController
                         : _rigManager.ControllerRig.rightController;
    controller._thumbstickAxis += delta;
  }

  [HarmonyPatch(typeof(OpenController), nameof(OpenController.CacheInputs))]
  class OpenController_CacheInputs_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(OpenController __instance) {
      if (!_rigManager)
        return;

      if (GetKey(KeyCode.W)) {
        MoveThumbstick(Handedness.LEFT, Vector2.up);
      }
      if (GetKey(KeyCode.S)) {
        MoveThumbstick(Handedness.LEFT, Vector2.down);
      }
      if (GetKey(KeyCode.A)) {
        MoveThumbstick(Handedness.LEFT, Vector2.left);
      }
      if (GetKey(KeyCode.D)) {
        MoveThumbstick(Handedness.LEFT, Vector2.right);
      }

      if (GetKey(KeyCode.Space)) {
        var cr = _rigManager.ControllerRig.rightController;
        cr._aButton = true;
        cr._aButtonDown = Input.GetKeyDown(KeyCode.Space);
        cr._aButtonUp = Input.GetKeyUp(KeyCode.Space);
      }

      if (Input.GetKey(KeyCode.LeftShift)) {
        var cl = _rigManager.ControllerRig.leftController;
        cl._thumbstick = true;
        cl._thumbstickDown = Input.GetKeyDown(KeyCode.LeftShift);
        cl._thumbstickUp = Input.GetKeyUp(KeyCode.LeftShift);
      }

      if (Input.GetKey(KeyCode.LeftControl)) {
        // _rigManager.ControllerRig._crouchTarget = 0.5f;
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
}
}
