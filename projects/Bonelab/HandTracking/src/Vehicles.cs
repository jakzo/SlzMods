using System;
using UnityEngine;
using Sst.Utilities;
using SLZ.Interaction;
using HarmonyLib;
using SLZ.Vehicle;
using SLZ.VRMK;
using SLZ.Marrow;

namespace Sst.HandTracking;

public class Vehicles {
  public HandTracker LeftTracker;
  public HandTracker RightTracker;

  public Vehicles(HandTracker leftTracker, HandTracker rightTracker) {
    LeftTracker = leftTracker;
    RightTracker = rightTracker;
  }

  public void Update() {
    // Not switching based on left-handed setting because real cars always have
    // brake pedal on the left, one handed mode works for both hands and it
    // shouldn't be inconvenient to use the opposite way
    var accelerator = GetVehiclePedalAmount(RightTracker);
    var brake = GetVehiclePedalAmount(LeftTracker);

    var nonDominantTracker =
        Utils.IsLocoControllerLeft() ? RightTracker : LeftTracker;
    nonDominantTracker.ProxyController.Joystick2DAxis = new Vector2(
        0f,
        (accelerator.HasValue
             ? accelerator.Value > 0f ? accelerator : brake - 1f
             : brake) ??
            0f
    );
  }

  // Returns a float between 0 and 1 with 1 meaning the index finger is pulled
  // in, or null if not seated and gripping a steering wheel
  private float? GetVehiclePedalAmount(HandTracker tracker) {
    if (LevelHooks.RigManager?.activeSeat == null ||
        !IsGrippingSteeringWheel(tracker)) {
      return null;
    }

    var indexPos = tracker.GetRelativeIndexTipPos();
    var min = -0.01f;
    var max = 0.01f;
    return Mathf.Clamp01((indexPos - min) / (max - min));
  }

  // TODO: Is this reliable enough to detect modded vehicle steering wheels?
  private bool IsGrippingSteeringWheel(HandTracker tracker) {
    var attachedHost =
        tracker.GetPhysicalHand()
            ?.AttachedReceiver?.Host?.TryCast<InteractableHost>();
    if (attachedHost == null)
      return false;
    var hingeHost = attachedHost?.GetComponent<HingeVirtualController>()
                        ?.host?.TryCast<InteractableHost>();
    return hingeHost?.GetInstanceID() == attachedHost.GetInstanceID();
  }

  [HarmonyPatch(typeof(Seat), nameof(Seat.OnTriggerStay))]
  internal static class Seat_OnTriggerStay {
    [HarmonyPrefix]
    private static bool Prefix(Seat __instance, Collider other) {
      var crouchHand = Utils.IsLocoControllerLeft() ? Mod.Instance.TrackerRight
                                                    : Mod.Instance.TrackerLeft;
      if (crouchHand.IsControllerConnected() || __instance.rigManager != null)
        return true;

      var physicsRig = other?.GetComponent<PhysGrounder>()?.physRig;
      if (physicsRig == null)
        return true;

      if (physicsRig.manager.activeSeat == null &&
          physicsRig.pelvisHeightMult < 0.6f) {
        __instance.IngressRig(physicsRig.manager);
      }
      return false;
    }
  }
}
