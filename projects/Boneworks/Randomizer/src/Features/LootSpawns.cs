using System;
using MelonLoader;
using HarmonyLib;
using StressLevelZero.Pool;
using System.Collections.Generic;
using StressLevelZero.Data;
using System.Reflection;
using UnityEngine;
using StressLevelZero.Props;

namespace Sst.Randomizer.Features;

public class LootSpawns : Feature {
  public static LootSpawns Instance;

  private Dictionary<CategoryFilters, List<SpawnableObject>>
      _spawnablesByCategory;
  private MelonPreferences_Entry<Mode> _prefMode;

  public override void Initialize() {
    Instance = this;

    _prefMode = Mod.PrefCategory.CreateEntry(
        "lootSpawnMode", Mode.PURE_RANDOM, "Loot spawn mode"
    );
  }

  private void UpdateSpawnablesListIfNecessary() {
    // TODO: Get list of all spawnables and create pool if it doesn't already
    // exist when spawning
    // if (PoolManager.GetRegisteredSpawnable(spawnable.UUID) == null)
    //   PoolManager.RegisterPool(spawnable);

    if (_spawnablesByCategory == null ||
        PoolManager._registeredSpawnableObjects.Count !=
            _spawnablesByCategory[CategoryFilters.All].Count) {
      _spawnablesByCategory = new() { [CategoryFilters.All] = new() };
      foreach (var spawnable in PoolManager._registeredSpawnableObjects
                   .Values) {
        _spawnablesByCategory[CategoryFilters.All].Add(spawnable);
        if (spawnable.category == CategoryFilters.All)
          continue;
        if (_spawnablesByCategory.TryGetValue(
                spawnable.category, out var categorySpawnables
            )) {
          categorySpawnables.Add(spawnable);
        } else {
          _spawnablesByCategory.Add(
              spawnable.category, new List<SpawnableObject>() { spawnable }
          );
        }
      }
    }
  }

  private SpawnableObject GetRandomizedLoot(SpawnableObject spawnable) {
    if (_prefMode.Value == Mode.NONE)
      return null;

    UpdateSpawnablesListIfNecessary();

    var category = _prefMode.Value == Mode.SAME_CATEGORY ? spawnable.category
        : _prefMode.Value == Mode.PURE_RANDOM            ? CategoryFilters.All
                                                         : CategoryFilters.All;
    _spawnablesByCategory.TryGetValue(category, out var spawnableChoices);
    if (spawnableChoices == null) {
      MelonLogger.Warning(
          "Spawnable category does not exist: " + spawnable.category
      );
      return null;
    }

    return spawnableChoices[new System.Random().Next(spawnableChoices.Count)];
  }

  [HarmonyPatch(typeof(LootTableData), nameof(LootTableData.GetLootItem))]
  class LootTableData_GetLootItem_Patch {
    [HarmonyPostfix()]
    internal static void
    Postfix(LootTableData __instance, ref SpawnableObject __result) {
      var randomLoot = Instance.GetRandomizedLoot(__result);
      if (randomLoot != null) {
        Dbg.Log(
            "Randomizing LootTableData.GetLootItem from", __result.title, "to",
            randomLoot.title
        );
        __result = randomLoot;
      }
    }
  }

  public enum Mode {
    PURE_RANDOM,
    SAME_CATEGORY,
    NONE,
  }
}
