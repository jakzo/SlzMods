using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using StressLevelZero;
using StressLevelZero.Props;
using StressLevelZero.Pool;

namespace Sst.LootDropBugfix {
public class Mod : MelonMod {
  private static bool? _nullableTrue = true;
  private static Il2CppSystem.Nullable<bool> _spawnAutoEnable =
      new Utilities.Il2CppNullable<bool>(_nullableTrue);

  private static Queue<(ObjectDestructable, string, Vector3)>
      _replacementsToSpawn = new Queue<(ObjectDestructable, string, Vector3)>();
  private static bool _isLoading = false;

  public override void OnApplicationStart() { Dbg.Init(BuildInfo.NAME); }

  public override void BONEWORKS_OnLoadingScreen() {
    _isLoading = true;
    Dbg.Log("_isLoading = true");
  }
  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    _isLoading = false;
    Dbg.Log("_isLoading = false");
  }

  public override void OnLateUpdate() {
    if (_isLoading)
      return;

    while (_replacementsToSpawn.Count > 0) {
      var (obj, title, position) = _replacementsToSpawn.Dequeue();
      try {
        SpawnReplacement(obj, title, position);
      } catch (Exception ex) {
        MelonLogger.Error(ex);
      }
    }

    // ---
    if (Time.time - _lastUpdate > 0.5f) {
      _lastUpdate = Time.time;
      var head = GameObject.FindObjectOfType<StressLevelZero.Rig.RigManager>()
                     .physicsRig.m_head;
      var ammoCrates =
          GameObject.FindObjectsOfType<ObjectDestructable>()
              .Where(obj =>
                         obj.lootTable != null && IsAmmoCrate(obj) &&
                         (obj.transform.position - head.position).sqrMagnitude >
                             25)
              .ToArray();
      if (ammoCrates.Length > 0) {
        ammoCrates[0].transform.position =
            head.position + head.rotation * new Vector3(0, 0, 2);
      }
    }
    // ---
  }
  private float _lastUpdate = 0;

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

    QueueSpawnReplacement(obj, spawnPosition);
  }

  private static void QueueSpawnReplacement(ObjectDestructable obj,
                                            Vector3 spawnPosition) {
    var replacement = obj.lootTable.GetLootItem();
    if (replacement == null)
      throw new Exception("GetLootItem returned null");
    MelonLogger.Warning(
        $"No spawned item found, spawning replacement now: {replacement.title}");
    _replacementsToSpawn.Enqueue((obj, replacement.title, spawnPosition));
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
      if (isCorrectPosition &&
          obj.lootTable.items.Any(item => topLevelObject.name.StartsWith(
                                      item.spawnable.prefab.name))) {
        return true;
      }
    }
    return false;
  }

  private static void SpawnReplacement(ObjectDestructable obj, string title,
                                       Vector3 spawnPosition) {
    Dbg.Log($"Spawning replacement: {title} @ {spawnPosition}");
    var spawnedItem = PoolManager.Spawn(title, spawnPosition,
                                        Quaternion.identity, _spawnAutoEnable);
    MakeSaveable(obj, spawnedItem);
  }

  private static void MakeSaveable(ObjectDestructable obj,
                                   GameObject spawnedItem) {
    if (obj == null) {
      MelonLogger.Warning(
          "Destructable became null before the replacement could be made saveable");
      return;
    }
    var saveItem = obj.GetComponent<SaveItem>();
    if (saveItem == null || !IsAmmoCrate(obj))
      return;
    Dbg.Log("Calling saveItem.OnSpawn()");
    saveItem.OnSpawn(spawnedItem);
  }

  private static bool IsAmmoCrate(ObjectDestructable obj) =>
      obj.lootTable.name.StartsWith("AmmoCrateTable_");
}
}
