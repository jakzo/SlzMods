using System;
using MelonLoader;
using HarmonyLib;
using StressLevelZero.Pool;
using System.Collections.Generic;
using StressLevelZero.Data;
using System.Reflection;
using UnityEngine;

namespace Sst.Randomizer.Features;

public class ItemSpawns : Feature {
  public static ItemSpawns Instance;

  private Dictionary<CategoryFilters, List<SpawnableObject>>
      _spawnablesByCategory;
  private MelonPreferences_Entry<Mode> _prefMode;

  public override void Initialize() {
    Instance = this;

    _prefMode = Mod.PrefCategory.CreateEntry(
        "itemSpawnMode", Mode.SAME_CATEGORY, "Item spawn mode"
    );

    // PatchAllOverloads(
    //     typeof(PoolManager), nameof(PoolManager.Spawn),
    //     typeof(ItemSpawns)
    //         .GetMethod(nameof(PoolManagerSpawnPrefix), AccessTools.all));
  }

  private void
  PatchAllOverloads(Type type, string methodName, MethodInfo prefix) {
    foreach (var method in type.GetMethods(AccessTools.all)) {
      if (method.Name == methodName) {
        Mod.HarmonyInstance.Patch(method, prefix: new HarmonyMethod(prefix));
      }
    }
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

  private string GetRandomizedSpawnName(string name) {
    if (_prefMode.Value == Mode.NONE)
      return null;

    UpdateSpawnablesListIfNecessary();

    var spawnable = PoolManager.GetRegisteredSpawnable(name);
    if (spawnable == null) {
      MelonLogger.Warning("Spawnable not in pool: " + name);
      return null;
    }

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

    var choice =
        spawnableChoices[new System.Random().Next(spawnableChoices.Count)];
    return choice.title;
  }

  private static bool PoolManagerSpawnPrefix(ref string name) {
    var randomName = name;
    // var randomName = Instance.GetRandomizedSpawnName(name);
    if (randomName != null) {
      Dbg.Log("Randomizing PoolManager.Spawn from", name, "to", randomName);
      // name = randomName;
    }
    return false;
  }

  // [HarmonyPatch(typeof(Pool), nameof(Pool.Spawn))]
  // class Pool_Spawn_Patch {
  //   private static bool _isDisabled = false;

  //   [HarmonyPrefix()]
  //   internal static bool Prefix(Pool __instance, ref GameObject __result) {
  //     if (_isDisabled)
  //       return true;
  //     _isDisabled = true;
  //     Dbg.Log("Pool.Spawn", __instance.name);
  //     try {
  //       foreach (var item in PoolManager.DynamicPools) {
  //         __result = item.value.Spawn();
  //         break;
  //       }
  //     } catch (Exception ex) {
  //       MelonLogger.Error("Pool.Spawn failed", ex);
  //     }
  //     _isDisabled = false;
  //     return false;
  //   }
  // }

  public enum Mode {
    PURE_RANDOM,
    SAME_CATEGORY,
    NONE,
  }
}
