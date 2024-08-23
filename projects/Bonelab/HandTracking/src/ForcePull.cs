using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using SLZ.Interaction;
using SLZ.Marrow;
using SLZ.Marrow.Utilities;

namespace Sst.HandTracking;

public class ForcePull {
  private const float FLICK_MAX_DURATION_SECONDS = 0.2f;
  private const float FLICK_MIN_ROTATION_DEGREES = 40f;
  private const float FLICK_MAX_DIST = 0.06f;
  private const float FLICK_MAX_DIST_SQR = FLICK_MAX_DIST * FLICK_MAX_DIST;

  private HandTracker _tracker;
  private LinkedList<StartingState> _startingStates = new();
  private ForcePullGrip _grip;

  public ForcePull(HandTracker tracker) { _tracker = tracker; }

  public bool IsPulling() => _grip?._pullToHand == _tracker.GetPhysicalHand();

  // Run after updating Tracker.IsGripping
  public void UpdateHand() {
    var hand = _tracker.GetPhysicalHand();
    if (hand == null)
      return;

    if (_grip?._pullToHand == hand) {
      if (!_tracker.IsGripping) {
        _tracker.ProxyController.GripButton = false;
        _grip.CancelPull(hand);
        _grip = null;
        _tracker.Log("Cancelled force pull due to ungripping");
      }
      return;
    }

    if (_grip) {
      if (hand.m_CurrentAttachedGO) {
        _tracker.Log("Force pull complete");
      } else {
        _tracker.Log("Cancelling force pull due to hand not being set");
      }
      _tracker.ProxyController.GripButton = false;
      _grip = null;
    }

    var isAlreadyHolding = hand.AttachedReceiver || hand.m_CurrentAttachedGO;
    if (!_tracker.IsGripping || isAlreadyHolding)
      return;

    var earliest = Time.time - FLICK_MAX_DURATION_SECONDS;
    while (_startingStates.Count > 0 &&
           _startingStates.First.Value.time < earliest) {
      _startingStates.RemoveFirst();
    }

    var rotationCache = new Dictionary<ForcePullGrip, float>();
    var rigController = _tracker.RigController?.transform;
    if (rigController == null)
      return;

    var rigRotAngles = rigController.rotation.eulerAngles;
    var rigPosRotNoZ = new SimpleTransform() {
      position = rigController.position,
      rotation = Quaternion.Euler(rigRotAngles.x, rigRotAngles.y, 0f),
    };
    var localRot = rigController.localEulerAngles;
    var localRotXY = Quaternion.Euler(localRot.x, localRot.y, 0f);

    var node = _startingStates.Last;
    while (node != null) {
      var handRotDiff = Quaternion.Angle(node.Value.localRotXY, localRotXY);
      var angleFromObject =
          GetRotationDifference(rotationCache, rigPosRotNoZ, node.Value.grip);
      var distSqr =
          (rigController.localPosition - node.Value.localPos).sqrMagnitude;
      if (handRotDiff >= FLICK_MIN_ROTATION_DEGREES &&
          angleFromObject > node.Value.angleFromObject &&
          distSqr <= FLICK_MAX_DIST_SQR) {
        // Need to set these or the force pull will instantly cancel
        hand.farHoveringReciever = node.Value.reciever;
        hand.Controller._primaryInteractionButton = true;
        hand.Controller._secondaryInteractionButton = true;

        _grip = node.Value.grip;
        _grip._pullToHand = hand;
        ForcePullGrip_Pull.Bypass.Enable(() => _grip.Pull(hand));
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
        localPos = rigController.localPosition,
        localRotXY = localRotXY,
        angleFromObject = GetRotationDifference(
            rotationCache, rigPosRotNoZ, hoveringForcePullGrip
        ),
      });
    }
  }

  private float GetRotationDifference(
      Dictionary<ForcePullGrip, float> cache, SimpleTransform rigPosRotNoZ,
      ForcePullGrip grip
  ) {
    if (cache.TryGetValue(grip, out var cached))
      return cached;
    var direction = grip.transform.position - rigPosRotNoZ.position;
    var target = Quaternion.LookRotation(direction);
    var diff = Quaternion.Angle(target, rigPosRotNoZ.rotation);
    cache.Add(grip, diff);
    return diff;
  }

  private struct StartingState {
    public float time;
    public ForcePullGrip grip;
    public HandReciever reciever;
    public Vector3 localPos;
    public Quaternion localRotXY;
    public float angleFromObject;
  }

  [HarmonyPatch(typeof(ForcePullGrip), nameof(ForcePullGrip.Pull))]
  internal static class ForcePullGrip_Pull {
    public static PatchBypass Bypass = new();

    [HarmonyPrefix]
    private static bool Prefix(ForcePullGrip __instance, Hand hand) {
      if (Bypass.IsCallable() ||
          Mod.Instance.GetTrackerFromProxyController(
              Utils.XrControllerOf(hand.Controller)
          ) == null) {
        return true;
      }

      __instance._pullToHand = null;
      return false;
    }
  }
}
