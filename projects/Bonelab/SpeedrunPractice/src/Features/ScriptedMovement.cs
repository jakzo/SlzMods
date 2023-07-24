using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using SLZ.Rig;

namespace Sst.SpeedrunPractice.Features {
class ScriptedMovement : Feature {
  public abstract class Movement {
    public abstract IEnumerator Start();

    public float time = 0f;
  }

  public static Func<Movement> ActiveMovement = () => new SuperJump();

  public static ScriptedMovement Instance;

  private static Movement CurrentMovement;
  private static IEnumerator CurrentMovementEnumerator;
  private static float startTime;

  public ScriptedMovement() {
    Instance = this;
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) =>
          ActiveMovement != null && Utils.State.rigManager && cl.GetAButton(),
      Handler =
          () => {
            MelonLogger.Msg("hotkeyyy");
            PerformMovement(ActiveMovement());
          },
    });
  }

  public static void PerformMovement(Movement movement) {
    if (CurrentMovement != null)
      return;
    CurrentMovement = movement;
  }

  [HarmonyPatch(typeof(OpenController), nameof(OpenController.CacheInputs))]
  class OpenController_CacheInputs_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(OpenController __instance) {
      var leftController = Utils.State.rigManager.ControllerRig.leftController;
      leftController._aButton = false;
      leftController._aButtonDown = false;
      leftController._aButtonUp = false;

      if (CurrentMovement == null)
        return;

      if (CurrentMovementEnumerator == null) {
        startTime = Time.time;
        CurrentMovement.time = 0f;
        CurrentMovementEnumerator = CurrentMovement.Start();
      } else {
        CurrentMovement.time = Time.time - startTime;
        if (!CurrentMovementEnumerator.MoveNext()) {
          CurrentMovement = null;
          CurrentMovementEnumerator = null;
        }
      }
    }
  }

  public class SuperJump : Movement {
    public override IEnumerator Start() {
      var rightController =
          Utils.State.rigManager.ControllerRig.rightController;
      rightController._aButton = true;
      rightController._aButtonDown = true;
      rightController._aButtonUp = false;

      do {
        MelonLogger.Msg("Wait");
        yield return null;

        rightController._aButton = true;
        rightController._aButtonDown = false;
        rightController._aButtonUp = false;
      } while (time < 1f);
      MelonLogger.Msg("Done!");

      rightController._aButton = false;
      rightController._aButtonDown = false;
      rightController._aButtonUp = true;
    }
  }
}
}
