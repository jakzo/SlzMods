using System;
using SLZ.Interaction;
using SLZ.Marrow.Utilities;
using Sst.Utilities;
using UnityEngine;

namespace Sst.HandTracking;

public class Inventory {
  private HandState _lastStateLeft = new();
  private HandState _lastStateRight = new();

  public void Reset() {
    _lastStateLeft.Reset();
    _lastStateRight.Reset();
  }

  public void OnHandUpdate(HandTracker tracker) {
    var state = tracker.Opts.isLeft ? _lastStateLeft : _lastStateRight;
    if (!tracker.IsTracking) {
      state.Reset();
      return;
    }

    if (!tracker.HandState.HasState ||
        tracker.HandState.HandConfidence != OVRPlugin.TrackingConfidence.High) {
      state.HasLostTracking = true;
      return;
    }

    bool? isLeftZone =
        IsInProximityOfInventorySlot(tracker.ProxyController.Position, true)
        ? true
        : IsInProximityOfInventorySlot(tracker.ProxyController.Position, false)
        ? false
        : null;
    var hand = tracker.GetPhysicalHand();
    if (hand == null)
      return;
    var heldReceiver = hand.AttachedReceiver;

    if (state.HasLostTracking) {
      state.HasLostTracking = false;

      if (state.IsLeftZone.HasValue) {
        var startedGripping = state.IsGripping && !tracker.IsGripping;
        var wasHolding =
            state.HeldReceiver != null && heldReceiver == state.HeldReceiver;
        // TODO: Do nothing if too much time has passed
        if (startedGripping && wasHolding && hand.HasAttachedObject()) {
          var slot = GetBackInventorySlotReceiver(state.IsLeftZone.Value);
          if (slot._weaponHost == null) {
            Dbg.Log("Inserting into inventory via proximity zone");
            // TODO: What if the gripped item cannot go in inventory?
            slot.InsertInSlot(hand.AttachedReceiver.Host.Cast<InteractableHost>(
            ));
          }
        } else {
          var startedUngripping = !state.IsGripping && tracker.IsGripping;
          if (startedUngripping && !hand.HasAttachedObject()) {
            var slot = GetBackInventorySlotReceiver(state.IsLeftZone.Value);
            if (slot._weaponHost != null) {
              Dbg.Log("Removing from inventory via proximity zone");
              var item = slot._weaponHost;
              slot.DropWeapon();
              // TODO: Is this the right logic to get the grip? (seems to work)
              var grip =
                  item.TryCast<InteractableHost>()?.GetForcePullGrip()?._grip ??
                  item.GetGrip();
              grip.OnGrabConfirm(hand, true);
            }
          }
        }
      }
    }

    state.IsLeftZone = isLeftZone;
    state.IsGripping = tracker.IsGripping;
    state.HeldReceiver = heldReceiver;
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

  public class HandState {
    public bool? IsLeftZone;
    public bool IsGripping;
    public HandReciever HeldReceiver;
    public bool HasLostTracking = false;
    public Vector3 Position;
    public float LastUpdate;

    public void Reset() {
      IsLeftZone = null;
      IsGripping = false;
      HeldReceiver = null;
      HasLostTracking = false;
      Position = Vector3.zero;
      LastUpdate = 0f;
    }
  }
}
