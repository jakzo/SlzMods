using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SLZ.Bonelab;
using SLZ.Interaction;
using SLZ.Marrow;
using SLZ.Marrow.Utilities;
using Sst.Utilities;
using UnityEngine;

namespace Sst.HandTracking;

public class Inventory {
  public LastTrackedHandState LastState = new();

  private HandTracker _tracker;
  private bool? _zoneGrabbedFromLastFrame;

  public Inventory(HandTracker tracker) { _tracker = tracker; }

  public void Reset() { LastState.Reset(); }

  // TODO: Handle noose
  public void UpdateHand() {
    if (!_tracker.IsTracking) {
      LastState.Reset();
      return;
    }

    if (!_tracker.HandState.HasState ||
        _tracker.HandState.HandConfidence !=
            OVRPlugin.TrackingConfidence.High) {
      LastState.HasLostTracking = true;
      return;
    }

    bool? isLeftZone =
        IsInProximityOfInventorySlot(_tracker.ProxyController.Position, true)
        ? true
        : IsInProximityOfInventorySlot(_tracker.ProxyController.Position, false)
        ? false
        : null;
    var hand = _tracker.GetPhysicalHand();
    if (hand == null)
      return;
    var heldReceiver = hand.AttachedReceiver;

    // Attempts to fix bug where item gets put in hand but hand is open
    if (_zoneGrabbedFromLastFrame.HasValue) {
      if (!_tracker.IsGripping && heldReceiver != null) {
        var slot =
            GetBackInventorySlotReceiver(_zoneGrabbedFromLastFrame.Value);
        slot.InsertInSlot(heldReceiver.Host.Cast<InteractableHost>());
      }
      _zoneGrabbedFromLastFrame = null;
    }

    if (LastState.HasLostTracking) {
      LastState.HasLostTracking = false;

      if (LastState.IsInLeftZone.HasValue) {
        var startedGripping = LastState.IsGripping && !_tracker.IsGripping;
        var wasHolding = LastState.HeldReceiver != null &&
            heldReceiver == LastState.HeldReceiver;
        // TODO: Do nothing if too much time has passed
        if (startedGripping && wasHolding && hand.HasAttachedObject()) {
          var slot = GetBackInventorySlotReceiver(LastState.IsInLeftZone.Value);
          if (slot._weaponHost == null) {
            Dbg.Log("Inserting into inventory via proximity zone");
            // TODO: What if the gripped item cannot go in inventory?
            // Answer: It doesn't go in, but it does hide the slot visual from
            // the popup inventory so we should check before inserting anyway
            slot.InsertInSlot(heldReceiver.Host.Cast<InteractableHost>());
          }
        } else {
          var startedUngripping = !LastState.IsGripping && _tracker.IsGripping;
          if (startedUngripping && !hand.HasAttachedObject()) {
            var slot =
                GetBackInventorySlotReceiver(LastState.IsInLeftZone.Value);
            if (slot._weaponHost != null) {
              Dbg.Log("Removing from inventory via proximity zone");
              var item = slot._weaponHost;
              slot.DropWeapon();
              // TODO: Is this the right logic to get the grip? (seems to work)
              var grip =
                  item.TryCast<InteractableHost>()?.GetForcePullGrip()?._grip ??
                  item.GetGrip();
              grip.OnGrabConfirm(hand, true);
              _zoneGrabbedFromLastFrame = LastState.IsInLeftZone.Value;
            }
          }
        }
      }
    }

    LastState.IsInLeftZone = isLeftZone;
    LastState.IsGripping = _tracker.IsGripping;
    LastState.HeldReceiver = heldReceiver;
  }

  private bool IsInProximityOfInventorySlot(Vector3 pos, bool left) {
    var d = left ? -1f : 1f;
    var hmd = MarrowGame.xr.HMD.Position;
    return pos.x * d > hmd.x * d + 0.1f && pos.x * d < hmd.x * d + 0.6f &&
        pos.y > hmd.y - 0.1f && pos.y < hmd.y + 0.3f && pos.z > hmd.z - 0.3f &&
        pos.z < hmd.z + 0.4f;
  }

  private InventorySlotReceiver GetBackInventorySlotReceiver(bool left
  ) => LevelHooks.RigManager.physicsRig.m_chest
           .FindChild(left ? "BackLf" : "BackRt")
           .GetComponent<SlotContainer>()
           .inventorySlotReceiver;

  public class LastTrackedHandState {
    public bool? IsInLeftZone;
    public bool IsGripping;
    public HandReciever HeldReceiver;
    public bool HasLostTracking = false;
    public Vector3 Position;
    public float LastUpdate;

    public void Reset() {
      IsInLeftZone = null;
      IsGripping = false;
      HeldReceiver = null;
      HasLostTracking = false;
      Position = Vector3.zero;
      LastUpdate = 0f;
    }
  }
}

public class NooseHelper {
  private const float NOOSE_DIST_THRESHOLD = 0.4f;
  private const float NOOSE_DIST_THRESHOLD_SQR =
      NOOSE_DIST_THRESHOLD * NOOSE_DIST_THRESHOLD;

  private NooseBonelabIntro _noose;
  private List<HandTracker> _trackersGrippingNoose = new();
  private bool _updateNextFrame;

  public void Update() {
    if (_noose == null ||
        _noose._nooseStage != NooseBonelabIntro.NooseStage.Initial)
      return;

    if (_updateNextFrame) {
      UpdateTrackersGrippingNoose();
      _updateNextFrame = false;
    }

    if (_trackersGrippingNoose.Count == 0 ||
        !_trackersGrippingNoose.All(
            tracker => tracker.Inventory.LastState.HasLostTracking
        ))
      return;

    var head = LevelHooks.RigManager.physicsRig.m_head;
    var nooseRbs = new[] {
      _noose.knotRb, _noose.nooseLeftRb, _noose.nooseRightRb,
      _noose.nooseBottomRb
    };
    var maxDistSqr =
        nooseRbs
            .Select(rb => (rb.worldCenterOfMass - head.position).sqrMagnitude)
            .Max();
    if (maxDistSqr > NOOSE_DIST_THRESHOLD_SQR)
      return;

    _noose.AttachNeck();
  }

  private void UpdateTrackersGrippingNoose() {
    var trackersGrippingNoose = Mod.Instance.NooseHelper._trackersGrippingNoose;
    trackersGrippingNoose.Clear();

    foreach (var host in _noose._hostManager.grabbedHosts) {
      foreach (var hand in host._hands) {
        var tracker = Mod.Instance.GetTrackerOfHand(hand.handedness);
        if (!trackersGrippingNoose.Contains(tracker))
          trackersGrippingNoose.Add(tracker);
      }
    }
  }

  [HarmonyPatch(
      typeof(NooseBonelabIntro), nameof(NooseBonelabIntro.OnHandAttached)
  )]
  internal static class NooseBonelabIntro_OnHandAttached {
    [HarmonyPostfix]
    private static void Postfix(NooseBonelabIntro __instance) {
      Mod.Instance.NooseHelper._noose = __instance;
      Mod.Instance.NooseHelper._updateNextFrame = true;
    }
  }

  [HarmonyPatch(
      typeof(NooseBonelabIntro), nameof(NooseBonelabIntro.OnHandDetached)
  )]
  internal static class NooseBonelabIntro_OnHandDetached {
    [HarmonyPostfix]
    private static void Postfix() {
      Mod.Instance.NooseHelper._updateNextFrame = true;
    }
  }
}
