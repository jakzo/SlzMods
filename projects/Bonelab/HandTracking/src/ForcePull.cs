using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using SLZ.Interaction;
using Sst.Utilities;

namespace Sst.HandTracking;

public class ForcePull {
  private const float FLICK_MAX_DURATION_SECONDS = 0.25f;
  private const float FLICK_MIN_ROTATION_DEGREES = 40f;

  public HandTracker Tracker;

  private LinkedList<StartingState> _startingStates = new();
  private ForcePullGrip _grip;

  public bool IsPulling() => _grip?._pullToHand == Tracker.GetPhysicalHand();

  // Run after updating Tracker.IsGripping
  public void Update() {
    var hand = Tracker.GetPhysicalHand();
    if (hand == null)
      return;

    var hoveringGrip =
        hand.farHoveringReciever?.Host.TryCast<InteractableHost>()
            ?.GetForcePullGrip();
    if (hoveringGrip) {
      var angle = (int)GetRotationDifference(
          new Dictionary<ForcePullGrip, float>(), hoveringGrip);
      Tracker.Log($"Force pull angle = {angle}");
    }

    //   LogSpam(
    //       $"isFist={Tracker.IsGripping}, isfp={!!_grip},
    //       curgo={!!hand.m_CurrentAttachedGO},
    //       attrec={!!hand.AttachedReceiver},
    //       farhov={!!hand.farHoveringReciever},
    //       prim={hand.Controller._primaryInteractionButton},
    //       sec={hand.Controller._secondaryInteractionButton},
    //       grip={Tracker.ProxyController.GripButton}");

    if (_grip?._pullToHand == hand) {
      if (!Tracker.IsGripping) {
        Tracker.ProxyController.GripButton = false;
        _grip.CancelPull(hand);
        _grip = null;
        Tracker.Log("Cancelled force pull due to ungripping");
      }
      return;
    }

    if (_grip) {
      if (hand.m_CurrentAttachedGO) {
        Tracker.Log("Force pull complete");
      } else {
        Tracker.Log("Cancelling force pull due to hand not being set");
      }
      Tracker.ProxyController.GripButton = false;
      _grip = null;
    }

    var isAlreadyHolding = hand.AttachedReceiver || hand.m_CurrentAttachedGO;
    if (!Tracker.IsGripping || isAlreadyHolding)
      return;

    var earliest = Time.time - FLICK_MAX_DURATION_SECONDS;
    while (_startingStates.Count > 0 &&
           _startingStates.First.Value.time < earliest) {
      _startingStates.RemoveFirst();
    }

    var rotationCache = new Dictionary<ForcePullGrip, float>();
    var node = _startingStates.Last;
    while (node != null) {
      var handAngleDiff = Quaternion.Angle(node.Value.rotation,
                                           Tracker.ProxyController.Rotation);
      var angleFromObject =
          GetRotationDifference(rotationCache, node.Value.grip);
      if (handAngleDiff >= FLICK_MIN_ROTATION_DEGREES &&
          angleFromObject > node.Value.angleFromObject) {
        // Need to set these or the force pull will instantly cancel
        hand.farHoveringReciever = node.Value.reciever;
        hand.Controller._primaryInteractionButton = true;
        hand.Controller._secondaryInteractionButton = true;

        _grip = node.Value.grip;
        _grip._pullToHand = hand;
        Utils.ForcePullGrip_Pull.Call(_grip, hand);
        Tracker.Log(
            "Force pulling, handAngleDiff:", handAngleDiff.ToString("0.0f"),
            "angleFromObject:", angleFromObject.ToString("0.0f"),
            "fphand:", _grip._pullToHand,
            "attached:", hand.AttachedReceiver?.name);
        break;
      }
      node = node.Previous;
    }

    var hoveringForcePullGrip =
        hand.farHoveringReciever?.Host.TryCast<InteractableHost>()
            ?.GetForcePullGrip();
    if (hoveringForcePullGrip) {
      _startingStates.AddLast(new StartingState() {
        time = Time.time,
        grip = hoveringForcePullGrip,
        reciever = hand.farHoveringReciever,
        rotation = Tracker.ProxyController.Rotation,
        angleFromObject =
            GetRotationDifference(rotationCache, hoveringForcePullGrip),
      });
    }
  }

  private float GetRotationDifference(Dictionary<ForcePullGrip, float> cache,
                                      ForcePullGrip grip) {
    if (cache.TryGetValue(grip, out var cached))
      return cached;
    // TODO: Get IRL hand position/rotation in game world (not physics rig hand)
    // TODO: Is this it? (verify on pc)
    var controller = Utils.RigControllerOf(Tracker).transform;
    var direction = grip.transform.position - controller.position;
    var target = Quaternion.LookRotation(direction);
    var diff = Quaternion.Angle(target, controller.rotation);
    cache.Add(grip, diff);
    return diff;
  }

  private struct StartingState {
    public float time;
    public ForcePullGrip grip;
    public HandReciever reciever;
    public Quaternion rotation;
    public float angleFromObject;
  }
}
