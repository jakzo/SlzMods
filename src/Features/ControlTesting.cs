using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace SpeedrunTools.Features {
class ControlTesting : Feature {
  private HashSet<string> _pressedKeys = new HashSet<string>();

  public ControlTesting() { IsDev = true; }

  public override void OnUpdate() {
    if (Mod.GameState.rigManager == null)
      return;
    var controller = Mod.GameState.rigManager.ControllerRig.leftController;
    var keys = new(string, bool)[] {
      ("GetAButton", controller.GetAButton()),
      ("GetBButton", controller.GetBButton()),
      ("GetThumbStick", controller.GetThumbStick()),
      ("GetMenuButton", controller.GetMenuButton()),
      ("GetSecondaryMenuButton", controller.GetSecondaryMenuButton()),
      ("GetPrimaryInteractionButton", controller.GetPrimaryInteractionButton()),
      ("GetSecondaryInteractionButton",
       controller.GetSecondaryInteractionButton()),
      ("GetGrabbedState", controller.GetGrabbedState()),
      ("GetReleasedState", controller.GetReleasedState()),
      ("IsGrabbed", controller.IsGrabbed()),
      ("IsReleased", controller.IsReleased()),
      ("GetThumbStickAxisX", controller.GetThumbStickAxis().x > 0.5f),
      ("GetThumbStickAxisY", controller.GetThumbStickAxis().y > 0.5f),
      ("GetGripAxis", controller.GetGripAxis() > 0.5f),
      ("GetGripForce", controller.GetGripForce() > 0.5f),
      ("GetGripVelocity", controller.GetGripVelocity() > 0.5f),
      ("GetThumbCurlAxis", controller.GetThumbCurlAxis() > 0.5f),
      ("GetIndexCurlAxis", controller.GetIndexCurlAxis() > 0.5f),
      ("GetMiddleCurlAxis", controller.GetMiddleCurlAxis() > 0.5f),
      ("GetRingCurlAxis", controller.GetRingCurlAxis() > 0.5f),
      ("GetPinkyCurlAxis", controller.GetPinkyCurlAxis() > 0.5f),
    };
    foreach (var (name, isDown) in keys) {
      if (isDown) {
        if (!_pressedKeys.Contains(name)) {
          _pressedKeys.Add(name);
          MelonLogger.Msg($"Key down: {name}");
        }
      } else if (_pressedKeys.Contains(name)) {
        _pressedKeys.Remove(name);
        MelonLogger.Msg($"Key up: {name}");
      }
    }
  }
}
}
