using System;
using System.Linq;
using HarmonyLib;
using SLZ.Bonelab;
using Sst.Utilities;

namespace Sst.HandTracking;

public class Ui {
  private HandTracker _tracker;
  private UIControllerInput _uiControllerInput;

  public Ui(HandTracker tracker) { _tracker = tracker; }

  public void UpdateHand() {
    var menu = UIRig.Instance?.popUpMenu;
    var rigManager = LevelHooks.RigManager;
    if (menu == null || rigManager == null)
      return;

    if (_tracker.HandState.MenuDown) {
      if (menu.m_IsActivated) {
        menu.Deactivate();
      } else {
        menu.Activate(
            rigManager.ControllerRig.m_head, rigManager.physicsRig.m_chest,
            GetUiControllerInput(), _tracker.RigController
        );
      }
    } else if (menu.m_IsActivated && _tracker.HandState.PinchUp) {
      PopUpMenuView_Trigger.Bypass.Enable(
          () => menu.Trigger(true, true, GetUiControllerInput())
      );
    }
  }

  private UIControllerInput GetUiControllerInput() {
    if (_uiControllerInput == null) {
#if PATCH5
      _uiControllerInput = _tracker.Opts.isLeft
          ? UIRig.Instance.leftUIController
          : UIRig.Instance.rightUIController;
#elif PATCH4
      _uiControllerInput =
          _tracker.RigController.GetComponent<UIControllerInput>();
#endif
    }
    return _uiControllerInput;
  }

  [HarmonyPatch(typeof(LaserCursor), nameof(LaserCursor.Update))]
  internal static class LaserCursor_Update {
    [HarmonyPrefix]
    private static void Prefix(LaserCursor __instance) {
      var pinchedTracker = Mod.Instance.Trackers.FirstOrDefault(
          t => t?.HandState.PinchUp ?? false
      );
      if (pinchedTracker?.RigController == null)
        return;

      if (__instance.activeController != pinchedTracker.RigController) {
        // Show laser pointer from the hand which is pinching
        Control_UI_InGameData.SetActiveController(pinchedTracker.RigController);
        __instance.controllerFocused = true;
      } else {
        LaserCursor_Trigger.Bypass.Enable(() => __instance.Trigger());
      }
    }
  }

  // Disable cursor trigger (except for when we call it above on pinch) so that
  // curling hand (which presses the controller trigger button) does not cause a
  // click on menus
  [HarmonyPatch(typeof(LaserCursor), nameof(LaserCursor.Trigger))]
  internal static class LaserCursor_Trigger {
    public static PatchBypass Bypass = new();

    [HarmonyPrefix]
    private static bool Prefix() => Bypass.IsCallable();
  }

  [HarmonyPatch(typeof(PopUpMenuView), nameof(PopUpMenuView.Trigger))]
  internal static class PopUpMenuView_Trigger {
    public static PatchBypass Bypass = new();

    [HarmonyPrefix]
    private static bool Prefix() => Bypass.IsCallable();
  }
}
