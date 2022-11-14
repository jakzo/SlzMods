using System;
using System.Linq;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using StressLevelZero;
using StressLevelZero.Props;
using StressLevelZero.Pool;
using StressLevelZero.Combat;

namespace Sst.LootDropBugfix {
public class Mod : MelonMod {
  protected static bool? _nullableTrue = true;
  protected static Il2CppSystem.Nullable<bool> _spawnAutoEnable =
      new Utilities.Il2CppNullable<bool>(_nullableTrue);

  public override void OnApplicationStart() { Dbg.Init(BuildInfo.NAME); }

  private static void HandleDestroyedLootable(ObjectDestructable obj) {
    var spawnPosition = obj.spawnTarget.position;
    Dbg.Log($"Lootable object destroyed at {spawnPosition.ToString()}");
    if (!IsLootGuaranteed(obj)) {
      // TODO: Does loot not spawn when percentages do not add up to 100?
      Dbg.Log("Loot chance is not 100% so not spawning replacement");
      return;
    }

    if (IsSpawnedItemPresent(obj, spawnPosition)) {
      Dbg.Log("Spawned item found");
      return;
    }

    SpawnReplacement(obj, spawnPosition);
  }

  private static bool IsLootGuaranteed(ObjectDestructable obj) {
    var totalPercentage = obj.lootTable.items.Aggregate(
        0f, (total, item) => total + item.percentage);
    return totalPercentage >= 100f;
  }

  private static bool IsSpawnedItemPresent(ObjectDestructable obj,
                                           Vector3 spawnPosition) {
    foreach (var collider in Physics.OverlapSphere(spawnPosition, 0.5f)) {
      var topLevelObject = collider.transform;
      while (topLevelObject.parent)
        topLevelObject = topLevelObject.parent;
      Dbg.Log(
          $"Nearby object: {topLevelObject.name} @ {topLevelObject.position.ToString()}");
      var isCorrectPosition =
          // TODO: Why is the position only correct for ammo? We could miss some
          // bugged loot spawns if another of the loot objects are nearby
          IsAmmoCrate(obj)
              ? (topLevelObject.position - spawnPosition).sqrMagnitude < 1e-9
              : true;
      var distSqr = (topLevelObject.position - spawnPosition).sqrMagnitude;
      if (isCorrectPosition &&
          obj.lootTable.items.Any(item => topLevelObject.name.StartsWith(
                                      item.spawnable.prefab.name))) {
        return true;
      }
    }
    return false;
  }

  private static void SpawnReplacement(ObjectDestructable obj,
                                       Vector3 spawnPosition) {
    var replacement = obj.lootTable.GetLootItem();
    if (replacement == null)
      throw new Exception("GetLootItem returned null");
    MelonLogger.Warning(
        $"No spawned item found, spawning replacement now: {replacement.title}");
    var spawnedItem = PoolManager.Spawn(replacement.title, spawnPosition,
                                        Quaternion.identity, _spawnAutoEnable);
    MakeSaveable(obj, spawnedItem);
  }

  private static void MakeSaveable(ObjectDestructable obj,
                                   GameObject spawnedItem) {
    var saveItem = obj.GetComponent<SaveItem>();
    if (saveItem == null || !IsAmmoCrate(obj))
      return;
    Dbg.Log("Calling saveItem.OnSpawn()");
    saveItem.OnSpawn(spawnedItem);
  }

  private static bool IsAmmoCrate(ObjectDestructable obj) =>
      obj.lootTable.name.StartsWith("AmmoCrateTable_");

  [HarmonyPatch(typeof(ObjectDestructable),
                nameof(ObjectDestructable.TakeDamage))]
  class ObjectDestructable_TakeDamage_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(ObjectDestructable __instance,
                                out bool __state) {
      __state = !__instance._isDead && __instance.lootTable?.items.Length > 0;
      if (__state)
        Dbg.Log("Lootable object damaged");
    }

    [HarmonyFinalizer()]
    internal static void Finalizer(ObjectDestructable __instance,
                                   bool __state) {
      if (__state && __instance._isDead)
        HandleDestroyedLootable(__instance);
    }
  }
}
}
