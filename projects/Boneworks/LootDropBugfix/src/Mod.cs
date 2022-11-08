using System.Linq;
using System.Collections.Generic;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using StressLevelZero.Props;
using StressLevelZero.Pool;

namespace Sst.LootDropBugfix {
public class Mod : MelonMod {
  protected static HashSet<ObjectDestructable> _damagedObjects =
      new HashSet<ObjectDestructable>();
  protected static Il2CppSystem.Nullable<bool> _nullableTrue =
      new Utilities.Il2CppNullable<bool>(true);

  public override void OnApplicationStart() { Dbg.Init(BuildInfo.NAME); }

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

      var spawnTarget = __instance.spawnTarget;
      Dbg.Log(
          $"Lootable object destroyed at {spawnTarget.position.ToString()}");
      var lootItems = __instance.lootTable.items;
      var totalPercentage =
          lootItems.Aggregate(0f, (total, item) => total + item.percentage);
      if (totalPercentage < 100f) {
        // TODO: Does loot not spawn when percentages do not add up to 100?
        Dbg.Log("Loot chance is not 100% so not spawning replacement");
        return;
      }

      foreach (var collider in Physics.OverlapSphere(spawnTarget.position,
                                                     0.5f)) {
        var topLevelObject = collider.transform;
        while (topLevelObject.parent)
          topLevelObject = topLevelObject.parent;
        var distSqr =
            (topLevelObject.position - spawnTarget.position).sqrMagnitude;
        if (distSqr < 0.0001f &&
            lootItems.Any(item => topLevelObject.name.StartsWith(
                              item.spawnable.prefab.name))) {
          Dbg.Log("Spawned item found");
          return;
        }
      }

      var replacement = __instance.lootTable.GetLootItem();
      Dbg.Log(
          $"No spawned item found, spawning replacement now: {replacement.title}");
      PoolManager.Spawn(replacement.title, spawnTarget.position,
                        Quaternion.identity, _nullableTrue);
    }
  }
}
}
