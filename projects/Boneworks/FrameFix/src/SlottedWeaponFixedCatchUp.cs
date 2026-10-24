using System;

using HarmonyLib;
using MelonLoader;
using StressLevelZero.Props.Weapons;
using StressLevelZero.Rig;
using UnityEngine;

namespace Sst.BoneworksFrameFix;

internal sealed partial class SlottedWeaponFixedCatchUp {
  private readonly bool _enabled;
  private SlotAnchor[] _slots = Array.Empty<SlotAnchor>();
  private RigManager _rig;
  private Transform _physicsChest;
  private int _lastFixedFrame = -1;
  private float _receiverSearchAt;

  public SlottedWeaponFixedCatchUp(bool enabled) { _enabled = enabled; }

  public void ResetScene() {
    RestoreAll();
    _slots = Array.Empty<SlotAnchor>();
    _rig = null;
    _physicsChest = null;
    _lastFixedFrame = -1;
    _receiverSearchAt = Time.realtimeSinceStartup + 1f;
#if DEBUG
    DebugResetDiagnostics();
#endif
  }

  public void OnUpdate() {
    if (!_enabled || _slots.Length != 0 ||
        Time.realtimeSinceStartup < _receiverSearchAt)
      return;
    _receiverSearchAt = Time.realtimeSinceStartup + 1f;
    var rig = UnityEngine.Object.FindObjectOfType<RigManager>();
    if (!rig || !rig.physicsRig || !rig.physicsRig.m_chest ||
        !rig.gameWorldSkeletonRig)
      return;
    var allReceivers =
        UnityEngine.Object.FindObjectsOfType<HandWeaponSlotReciever>();
    var count = 0;
    for (var i = 0; i < allReceivers.Length; i++) {
      if (allReceivers[i] &&
          allReceivers[i].transform.IsChildOf(rig.gameWorldSkeletonRig.transform
          ))
        count++;
    }
    if (count == 0)
      return;
    _slots = new SlotAnchor[count];
    for (int source = 0, target = 0; source < allReceivers.Length; source++) {
      var receiver = allReceivers[source];
      if (!receiver ||
          !receiver.transform.IsChildOf(rig.gameWorldSkeletonRig.transform))
        continue;
      _slots[target++] = new SlotAnchor { Receiver = receiver };
    }
    _rig = rig;
    _physicsChest = rig.physicsRig.m_chest;
#if DEBUG
    MelonLogger.Msg(
        "Fixed-tick weapon-slot catch-up found " + count + " body slots."
    );
#endif
  }

  public void Shutdown() => RestoreAll();

  private void OnFixedUpdatePrefix(RigManager rig) {
    if (!_enabled || _slots.Length == 0 || rig != _rig)
      return;
    var frame = Time.frameCount;
    if (frame != _lastFixedFrame) {
      _lastFixedFrame = frame;
      return;
    }
#if DEBUG
    var started = DebugCatchUpStarted();
#endif
    var moved = 0;
    for (var i = 0; i < _slots.Length; i++) {
      var slot = _slots[i];
      if (!slot.Host || !IsStillSlotted(slot))
        continue;
      slot.Host.SetPositionAndRotation(
          _physicsChest.TransformPoint(slot.PositionInChest),
          _physicsChest.rotation * slot.RotationInChest
      );
      slot.Overridden = true;
      moved++;
    }
    if (moved > 0)
      Physics.SyncTransforms();
#if DEBUG
    DebugRecordCatchUp(started, moved);
#endif
  }

  private void OnLateUpdatePrefix(RigManager rig) {
    if (!_enabled || rig != _rig)
      return;
    RestoreAll();
  }

  private void OnLateUpdatePostfix(RigManager rig) {
    if (!_enabled || _slots.Length == 0 || rig != _rig || !_physicsChest)
      return;
    for (var i = 0; i < _slots.Length; i++) {
      var slot = _slots[i];
      var receiver = slot.Receiver;
      if (!receiver || receiver.isInUIMode) {
        Invalidate(slot);
        continue;
      }
      var host = receiver.m_WeaponHost;
      var hostTransform = host ? host.transform : null;
      var expectedParent = receiver.enableUpdateVolume
          ? receiver.transform.parent
          : receiver.transform;
      if (!hostTransform || hostTransform.parent != expectedParent) {
        Invalidate(slot);
        continue;
      }
      slot.Host = hostTransform;
      slot.Parent = expectedParent;
      slot.LocalPosition = hostTransform.localPosition;
      slot.LocalRotation = hostTransform.localRotation;
      slot.PositionInChest =
          _physicsChest.InverseTransformPoint(hostTransform.position);
      slot.RotationInChest =
          Quaternion.Inverse(_physicsChest.rotation) * hostTransform.rotation;
    }
  }

  private void RestoreAll() {
    for (var i = 0; i < _slots.Length; i++) {
      var slot = _slots[i];
      Restore(slot);
    }
  }

  private void OnInventoryTransitionPrefix(HandWeaponSlotReciever receiver) {
    if (!_enabled)
      return;
    var slot = FindSlot(receiver);
    if (slot != null)
      Restore(slot);
  }

  private void OnInventoryTransitionPostfix(HandWeaponSlotReciever receiver) {
    if (!_enabled)
      return;
    var slot = FindSlot(receiver);
    if (slot != null)
      Invalidate(slot);
  }

  private SlotAnchor FindSlot(HandWeaponSlotReciever receiver) {
    for (var i = 0; i < _slots.Length; i++) {
      if (_slots[i].Receiver == receiver)
        return _slots[i];
    }
    return null;
  }

  private static void Restore(SlotAnchor slot) {
    if (!slot.Overridden)
      return;
    if (IsStillSlotted(slot)) {
      slot.Host.localPosition = slot.LocalPosition;
      slot.Host.localRotation = slot.LocalRotation;
    }
    slot.Overridden = false;
  }

  private static void Invalidate(SlotAnchor slot) {
    slot.Overridden = false;
    slot.Host = null;
    slot.Parent = null;
  }

  private static bool IsStillSlotted(SlotAnchor slot) {
    var receiver = slot.Receiver;
    var currentHost = receiver ? receiver.m_WeaponHost : null;
    return currentHost && currentHost.transform == slot.Host && slot.Host &&
        slot.Host.parent == slot.Parent;
  }

  private sealed class SlotAnchor {
    public HandWeaponSlotReciever Receiver;
    public Transform Host;
    public Transform Parent;
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;
    public Vector3 PositionInChest;
    public Quaternion RotationInChest;
    public bool Overridden;
  }

  [HarmonyPatch(typeof(RigManager), "FixedUpdate")]
  private static class RigManagerFixedUpdatePatch {
    [HarmonyPrefix]
    private static void Prefix(RigManager __instance) {
      Mod.Instance?.SlottedWeaponCatchUp?.OnFixedUpdatePrefix(__instance);
    }
  }

  [HarmonyPatch(typeof(RigManager), "LateUpdate")]
  private static class RigManagerLateUpdatePatch {
    [HarmonyPrefix]
    private static void Prefix(RigManager __instance) {
      Mod.Instance?.SlottedWeaponCatchUp?.OnLateUpdatePrefix(__instance);
    }

    [HarmonyPostfix]
    private static void Postfix(RigManager __instance) {
      Mod.Instance?.SlottedWeaponCatchUp?.OnLateUpdatePostfix(__instance);
    }
  }

  [HarmonyPatch(
      typeof(HandWeaponSlotReciever), nameof(HandWeaponSlotReciever.SendToUI)
  )]
  private static class HandWeaponSlotReceiverSendToUiPatch {
    [HarmonyPrefix]
    private static void Prefix(HandWeaponSlotReciever __instance) {
      Mod.Instance?.SlottedWeaponCatchUp?.OnInventoryTransitionPrefix(__instance
      );
    }

    [HarmonyPostfix]
    private static void Postfix(HandWeaponSlotReciever __instance) {
      Mod.Instance?.SlottedWeaponCatchUp?.OnInventoryTransitionPostfix(
          __instance
      );
    }
  }

  [HarmonyPatch(
      typeof(HandWeaponSlotReciever),
      nameof(HandWeaponSlotReciever.ReturnFromUI)
  )]
  private static class HandWeaponSlotReceiverReturnFromUiPatch {
    [HarmonyPrefix]
    private static void Prefix(HandWeaponSlotReciever __instance) {
      Mod.Instance?.SlottedWeaponCatchUp?.OnInventoryTransitionPrefix(__instance
      );
    }

    [HarmonyPostfix]
    private static void Postfix(HandWeaponSlotReciever __instance) {
      Mod.Instance?.SlottedWeaponCatchUp?.OnInventoryTransitionPostfix(
          __instance
      );
    }
  }
}
