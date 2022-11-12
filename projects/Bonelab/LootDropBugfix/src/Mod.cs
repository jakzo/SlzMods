using System.Linq;
using System.Collections.Generic;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using SLZ.Rig;
using SLZ.Props;
using SLZ.Bonelab;
using SLZ.Marrow.Data;
using SLZ.Marrow.Pool;

namespace Sst.LootDropBugfix {
public class Mod : MelonMod {
  private class DestroyedObject {
    public Vector3 SpawnPosition;
    public Quaternion SpawnRotation;
    public ObjectDestructable ObjectDestructable;
    public Spawnable OnlySpawnable;
    public PlacerSaver PlacerSaver;
    public int FramesWaited = 0;

    public string[] GetLootNamePrefixes() =>
        ObjectDestructable.lootTable.items
            .Select(item => item.spawnable.crateRef.Crate.MainAsset.Asset?.name)
            .Where(name => name != null)
            .Select(name => $"{name} [")
            .ToArray();
  }

  private const int FRAMES_TO_WAIT_AFTER_LOAD = 1;

  private static Il2CppSystem.Nullable<Vector3> _nullableVector =
      new Utilities.Il2CppNullable<Vector3>(null);
  private static Il2CppSystem.Nullable<int> _nullableInt =
      new Utilities.Il2CppNullable<int>(null);

  private static HashSet<ObjectDestructable> _damagedObjects =
      new HashSet<ObjectDestructable>();
  private static HashSet<DestroyedObject> _destroyedObjects =
      new HashSet<DestroyedObject>();

  public override void OnInitializeMelon() { Dbg.Init(BuildInfo.NAME); }

  public override void OnLateUpdate() {
    foreach (var destroyed in _destroyedObjects.ToArray()) {
      if (FindSpawnedItem(destroyed)) {
        Dbg.Log("Spawned item found");
        _destroyedObjects.Remove(destroyed);
        continue;
      }

      // TODO: Is there any way we can detect which loot item is being spawned?
      if (destroyed.OnlySpawnable != null) {
        var crate = destroyed.OnlySpawnable.crateRef.Crate;
        var isSpawnableLoaded = crate.MainAsset.IsDone;
        if (!isSpawnableLoaded) {
          // TODO: Is there a way we can identify the loot spawned after loading
          // (other than saveable ID)? Because position seems to not work after
          // load, though losing non-saveable loot isn't a huge deal
          Dbg.Log($"Loot asset not loaded yet: {crate.Title}");
          // Better to possibly miss replacing an unsaveable loot than always
          // spawn duplicates
          if (destroyed.PlacerSaver == null) {
            Dbg.Log(
                "Will not search for or replace dropped loot since we have no way to find it once it loads");
            _destroyedObjects.Remove(destroyed);
          }
          return;
        }

        if (destroyed.FramesWaited++ < FRAMES_TO_WAIT_AFTER_LOAD) {
          Dbg.Log("Waiting another frame for it to spawn after loading");
          return;
        }
      }

      var replacement = destroyed.ObjectDestructable.lootTable.GetLootItem();
      Dbg.Log(
          $"No spawned item found, spawning replacement now: {replacement.crateRef.Crate.Title}");
      _destroyedObjects.Remove(destroyed);
      AssetSpawner.Spawn(
          replacement, destroyed.SpawnPosition, destroyed.SpawnRotation,
          _nullableVector, false, _nullableInt,
          new System.Action<GameObject>(spawnedItem => {
            if (destroyed.PlacerSaver == null)
              return;
            var isAmmoCrate =
                destroyed.ObjectDestructable.lootTable.name.StartsWith(
                    "AmmoCrateTable_");
            if (!isAmmoCrate)
              return;
            Dbg.Log("Calling PlacerSaver.OnAmmoCrateLootSpawned()");
            destroyed.PlacerSaver.OnAmmoCrateLootSpawned(
                destroyed.ObjectDestructable, replacement, spawnedItem);
          }));
    }
  }

  private bool FindSpawnedItem(DestroyedObject destroyed) {
    foreach (var collider in Physics.OverlapSphere(destroyed.SpawnPosition,
                                                   0.5f)) {
      var topLevelObject = collider.transform;
      while (topLevelObject.parent)
        topLevelObject = topLevelObject.parent;
      var distSqr =
          (topLevelObject.position - destroyed.SpawnPosition).sqrMagnitude;
      if (distSqr < 1e-9 &&
          destroyed.GetLootNamePrefixes().Any(
              prefix => topLevelObject.name.StartsWith(prefix))) {
        return true;
      }
    }

    var saveableId = destroyed.PlacerSaver?.Saveable.UniqueId;
    if (saveableId != null && GameObject.FindObjectsOfType<Saveable>().Any(
                                  s => s.UniqueId == saveableId))
      return true;

    return false;
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

      var spawnTarget = __instance.spawnTarget;
      Dbg.Log(
          $"Lootable object destroyed at {spawnTarget.position.ToString()}");
      var totalPercentage = __instance.lootTable.items.Aggregate(
          0f, (total, item) => total + item.percentage);
      if (totalPercentage < 100f) {
        // TODO: Does loot not spawn when percentages do not add up to 100?
        Dbg.Log("Loot chance is not 100% so not spawning replacement");
        return;
      }

      var firstLootItemBarcode =
          __instance.lootTable.items[0].spawnable.crateRef.Barcode.ID;
      var hasOneLootItem = __instance.lootTable.items.All(
          item => item.spawnable.crateRef.Barcode.ID == firstLootItemBarcode);

      _destroyedObjects.Add(new DestroyedObject() {
        SpawnPosition = spawnTarget.position,
        SpawnRotation = spawnTarget.rotation,
        ObjectDestructable = __instance,
        OnlySpawnable =
            hasOneLootItem ? __instance.lootTable.GetLootItem() : null,
        PlacerSaver = __instance.GetComponent<PlacerSaver>(),
      });
    }
  }

  [HarmonyPatch(typeof(RigManager), nameof(RigManager.Awake))]
  class RigManager_Awake_Patch {
    [HarmonyPostfix()]
    internal static void Postfix() {
      _damagedObjects.Clear();
      _destroyedObjects.Clear();
    }
  }
}
}
