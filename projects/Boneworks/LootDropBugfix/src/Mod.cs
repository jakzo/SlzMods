using System;
using System.Linq;
using System.Collections.Generic;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using StressLevelZero;
using StressLevelZero.Props;
using StressLevelZero.Pool;

namespace Sst.LootDropBugfix {
public class Mod : MelonMod {
  protected static HashSet<ObjectDestructable> _damagedObjects =
      new HashSet<ObjectDestructable>();
  protected static Il2CppSystem.Nullable<bool> _nullableTrue =
      new Utilities.Il2CppNullable<bool>(true);

  public override void OnApplicationStart() { Dbg.Init(BuildInfo.NAME); }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    _damagedObjects.Clear();
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
      var distSqr = (topLevelObject.position - spawnPosition).sqrMagnitude;
      if (distSqr < 0.0001f &&
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
                                        Quaternion.identity, _nullableTrue);
    MakeSaveable(obj, spawnedItem);
  }

  private static void MakeSaveable(ObjectDestructable obj,
                                   GameObject spawnedItem) {
    var saveItem = obj.GetComponent<SaveItem>();
    if (saveItem == null)
      return;
    var isAmmoCrate = obj.lootTable.name.StartsWith("AmmoCrateTable_");
    if (!isAmmoCrate)
      return;
    Dbg.Log("Calling saveItem.OnSpawn()");
    saveItem.OnSpawn(spawnedItem);
  }

  [HarmonyPatch(typeof(ObjectDestructable),
                nameof(ObjectDestructable.TakeDamage))]
  class ObjectDestructable_TakeDamage_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(ObjectDestructable __instance) {
      if (!__instance._isDead && __instance.lootTable?.items.Length > 0) {
        Dbg.Log("Lootable object damaged");
        _damagedObjects.Add(__instance);
      }
    }

    [HarmonyPostfix()]
    internal static void Postfix(ObjectDestructable __instance) {
      if (!_damagedObjects.Contains(__instance))
        return;
      _damagedObjects.Remove(__instance);

      if (!__instance._isDead)
        return;

      var spawnPosition = __instance.spawnTarget.position;
      Dbg.Log($"Lootable object destroyed at {spawnPosition.ToString()}");
      if (IsLootGuaranteed(__instance)) {
        // TODO: Does loot not spawn when percentages do not add up to 100?
        Dbg.Log("Loot chance is not 100% so not spawning replacement");
        return;
      }

      if (IsSpawnedItemPresent(__instance, spawnPosition)) {
        Dbg.Log("Spawned item found");
        return;
      }

      SpawnReplacement(__instance, spawnPosition);
    }
  }

  // ---
  // private int _sceneIdx;
  // private float _lastTime = 0;
  // private ObjectDestructable _spawnedCrate;
  // public override void OnSceneWasInitialized(int buildIndex, string
  // sceneName) {
  //   _sceneIdx = buildIndex;
  // }
  // public override void OnUpdate() {
  //   if (_sceneIdx <= 0 || _sceneIdx > 13)
  //     return;
  //   if (Time.time - _lastTime >= 0.5)
  //     return;
  //   _lastTime = Time.time;
  //   if (_spawnedCrate == null) {
  //     var head =
  //     GameObject.FindObjectOfType<StressLevelZero.Rig.RigManager>()
  //                    .physicsRig.m_head;
  //     _spawnedCrate =
  //         PoolManager
  //             .Spawn("Ammo Box Small",
  //                    head.position + head.rotation * new Vector3(0, 0, 2),
  //                    Quaternion.identity, Vector3.zero, _nullableTrue)
  //             .GetComponent<ObjectDestructable>();
  //   } else {
  //     _spawnedCrate.TakeDamage(Vector3.up, 100f, false,
  //                              StressLevelZero.Combat.AttackType.Piercing);
  //   }
  // }
  // void Snippet() {
  //   var _nullableTrue = Paste() as Il2CppSystem.Nullable<bool>;
  //   var head = UnityEngine.GameObject
  //                  .FindObjectOfType<StressLevelZero.Rig.RigManager>()
  //                  .physicsRig.m_head;
  //   var go =StressLevelZero.Pool.PoolManager.Spawn(
  //       "Ammo Box Small", head.position + head.rotation * new Vector3(0, 0,
  //       2), Quaternion.identity, Vector3.zero, _nullableTrue);
  //   go;
  // }
  // ---
}
}
