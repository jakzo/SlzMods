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
  private const float WAIT_AFTER_LOAD = 5f;

  private static bool? _nullableTrue = true;
  private static Il2CppSystem.Nullable<bool> _spawnAutoEnable =
      new Utilities.Il2CppNullable<bool>(_nullableTrue);

  private static HashSet<Replacement> _replacementsToSpawn =
      new HashSet<Replacement>();
  private static bool _isLoading = false;

  public override void OnApplicationStart() {
    Dbg.Init(BuildInfo.NAME);
    AmmoDebugger.Initialize();
  }

  public override void BONEWORKS_OnLoadingScreen() {
    _isLoading = true;
    _replacementsToSpawn.Clear();
    Dbg.Log("_isLoading = true");
  }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    _isLoading = false;
    Dbg.Log("_isLoading = false");
  }

  public override void OnLateUpdate() {
    if (_isLoading)
      return;

    var replacements =
        _replacementsToSpawn
            .Where(replacement => Time.time >= replacement.timeToSpawnAt)
            .ToArray();
    foreach (var replacement in replacements) {
      _replacementsToSpawn.Remove(replacement);
      try {
        SpawnReplacement(replacement);
      } catch (Exception ex) {
        MelonLogger.Error(ex);
      }
    }

    AmmoDebugger.OnUpdate();
  }

  [HarmonyPatch(typeof(ObjectDestructable),
                nameof(ObjectDestructable.TakeDamage))]
  class ObjectDestructable_TakeDamage_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(ObjectDestructable __instance, float damage,
                                out float __state) {
      if (_isLoading) {
        if (damage > __instance._health)
          Dbg.Log(
              $"Item would have broken but is indestructible before load: {__instance.name}");
        __state = __instance._health;
        __instance._health = float.PositiveInfinity;
      } else if (!__instance._isDead &&
                 __instance.lootTable?.items.Length > 0) {
        __state = 1f;
        Dbg.Log("Lootable object damaged");
      } else {
        __state = 0f;
      }
    }

    [HarmonyFinalizer()]
    internal static void Finalizer(ObjectDestructable __instance,
                                   float __state) {
      if (_isLoading) {
        __instance._health = __state;
      } else if (__state != 0 && __instance._isDead) {
        HandleDestroyedLootable(__instance);
      }
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

    var prefabNames =
        obj.lootTable.items.Select(item => item.spawnable.prefab.name);
    // TODO: Why is the position only correct for ammo? We could miss some
    // bugged loot spawns if another of the loot objects are nearby
    if (IsSpawnedItemPresent(prefabNames, spawnPosition, IsAmmoCrate(obj))) {
      Dbg.Log("Spawned item found");
      return;
    }

    QueueSpawnReplacement(obj, spawnPosition);
  }

  private static void RetryIfNotSpawned(Replacement replacement) {
    if (IsSpawnedItemPresent(new string[] { replacement.prefabName },
                             replacement.spawnPosition, true))
      return;

    Dbg.Log(
        $"{replacement.title} not found @ {replacement.spawnPosition.ToString()}, queueing for respawn again");
    replacement.delay *= Replacement.BACKOFF;
    replacement.timeToSpawnAt = Time.time + replacement.delay;
    _replacementsToSpawn.Add(replacement);
  }

  private static void QueueSpawnReplacement(ObjectDestructable obj,
                                            Vector3 spawnPosition) {
    var item = obj.lootTable.GetLootItem();
    if (item == null)
      throw new Exception("GetLootItem returned null");
    MelonLogger.Warning(
        $"No spawned item found, spawning replacement now: {item.title}");
    _replacementsToSpawn.Add(new Replacement() {
      obj = obj,
      title = item.title,
      prefabName = item.prefab.name,
      spawnPosition = spawnPosition,
    });
  }

  private static bool IsLootGuaranteed(ObjectDestructable obj) {
    var totalPercentage = obj.lootTable.items.Aggregate(
        0f, (total, item) => total + item.percentage);
    return totalPercentage >= 100f;
  }

  private static bool IsSpawnedItemPresent(IEnumerable<string> prefabNames,
                                           Vector3 spawnPosition,
                                           bool checkPosition) {
    // TODO: Ignore if confetti? (no collider to find)
    foreach (var collider in Physics.OverlapSphere(spawnPosition, 0.5f)) {
      var topLevelObject = collider.transform;
      while (topLevelObject.parent)
        topLevelObject = topLevelObject.parent;
      Dbg.Log(
          $"Nearby object: {topLevelObject.name} @ {topLevelObject.position.ToString()}");
      var isCorrectPosition =
          !checkPosition ||
          (topLevelObject.position - spawnPosition).sqrMagnitude < 1e-9;
      if (isCorrectPosition &&
          prefabNames.Any(prefabName =>
                              topLevelObject.name.StartsWith(prefabName))) {
        return true;
      }
    }
    return false;
  }

  private static void SpawnReplacement(Replacement replacement) {
    Dbg.Log(
        $"Spawning replacement: {replacement.title} @ {replacement.spawnPosition.ToString()}");
    var spawnedItem =
        PoolManager.Spawn(replacement.title, replacement.spawnPosition,
                          Quaternion.identity, _spawnAutoEnable);
    MakeSaveable(replacement.obj, spawnedItem);

    if (spawnedItem != null)
      Dbg.Log(
          $"Replacement: {spawnedItem.name}, active={spawnedItem.active}, pool={spawnedItem.transform.parent?.name.StartsWith("Pool") ?? false}");
    else
      Dbg.Log("Spawned replacement is null!");

    // TODO: Are we sure we will never need this?
    // RetryIfNotSpawned(replacement);

    AmmoDebugger.OnAmmoReplaced();
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

  public static bool IsAmmoCrate(ObjectDestructable obj) =>
      obj.lootTable.name.StartsWith("AmmoCrateTable_");
}

class Replacement {
  public ObjectDestructable obj;
  public string title;
  public string prefabName;
  public Vector3 spawnPosition;
  public float timeToSpawnAt = Time.time;
  public float delay = 0.2f;

  public const float BACKOFF = 1.5f;
}
}
