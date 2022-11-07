using System.Linq;
using System.Collections.Generic;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using StressLevelZero.Props;
using StressLevelZero.Pool;
using StressLevelZero.Data;

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
                                                     0.001f)) {
        if (lootItems.Any(item => collider.gameObject.name.StartsWith(
                              item.spawnable.name))) {
          Dbg.Log("Spawned item found");
          return;
        }
      }

      var targetNum = Random.value * 100f;
      var currentNum = 0f;
      var replacement = lootItems
                            .First(item => {
                              currentNum += item.percentage;
                              return currentNum >= targetNum;
                            })
                            .spawnable;
      Dbg.Log(
          $"No spawned item found, spawning replacement now: {replacement.name}");
      PoolManager.Spawn(replacement.name, spawnTarget.position,
                        spawnTarget.rotation, spawnTarget.lossyScale,
                        _nullableTrue);
    }
  }
}
}
