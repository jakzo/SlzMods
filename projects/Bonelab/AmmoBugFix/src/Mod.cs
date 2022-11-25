using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using SLZ.Rig;
using SLZ.Props;
using SLZ.Bonelab;
using SLZ.Marrow.Data;
using SLZ.Marrow.Pool;

namespace Sst.AmmoBugFix {
public class Mod : MelonMod {
  protected const int FRAMES_TO_WAIT = 1;

  protected static Il2CppSystem.Nullable<Vector3> _nullableVector =
      new Utilities.Il2CppNullable<Vector3>(null);
  protected static Il2CppSystem.Nullable<int> _nullableInt =
      new Utilities.Il2CppNullable<int>(null);

  protected static HashSet<ObjectDestructable> _damagedAmmoCrates =
      new HashSet<ObjectDestructable>();
  protected static HashSet<DestroyedAmmoCrate> _destroyedAmmoCrates =
      new HashSet<DestroyedAmmoCrate>();

  protected class DestroyedAmmoCrate {
    public float TimeDestroyed;
    public Vector3 SpawnPosition;
    public Quaternion SpawnRotation;
    public ObjectDestructable ObjectDestructable;
    public Spawnable Spawnable;
    public PlacerSaver PlacerSaver;
    public int FramesWaited = 0;
  }

  public override void OnInitializeMelon() { Dbg.Init(BuildInfo.NAME); }

  public override void OnLateUpdate() {
    foreach (var dac in _destroyedAmmoCrates.ToArray()) {
      var saveableId = dac.PlacerSaver?.Saveable.UniqueId;
      var spawnedAmmoFound =
          GameObject.FindObjectsOfType<AmmoPickupProxy>().Any(ap => {
            if (saveableId != null &&
                ap.GetComponent<Saveable>()?.UniqueId == saveableId)
              return true;

            // TODO: Will the position always exactly match or could there be
            // float rounding errors?
            var distSqr =
                (ap.transform.position - dac.SpawnPosition).sqrMagnitude;
            // Dbg.Log($"Checking ammo {ap.name} at distSqr = {distSqr}");
            return distSqr < 1e-9;
          });
      if (spawnedAmmoFound) {
        Dbg.Log("Spawned ammo found");
        _destroyedAmmoCrates.Remove(dac);
        return;
      }

      var isSpawnableLoaded = dac.Spawnable.crateRef.Crate.MainAsset.IsDone;
      if (!isSpawnableLoaded) {
        // TODO: Is there a way we can identify the ammo spawned after loading
        // (other than saveable ID)? Because position seems to not work after
        // load, though losing non-saveable ammo isn't a huge deal
        Dbg.Log("Ammo asset not loaded");
        // Better to possibly miss replacing an unsaveable ammo than always
        // spawn duplicates
        if (dac.PlacerSaver == null)
          _destroyedAmmoCrates.Remove(dac);
        return;
      }

      if (dac.FramesWaited++ < FRAMES_TO_WAIT) {
        Dbg.Log("Waiting another frame");
        return;
      }

      MelonLogger.Warning(
          $"No spawned ammo found, spawning replacement now: {dac.Spawnable.crateRef.Crate.Title}");
      _destroyedAmmoCrates.Remove(dac);
      AssetSpawner.Spawn(
          dac.Spawnable, dac.SpawnPosition, dac.SpawnRotation, _nullableVector,
          false, _nullableInt, new System.Action<GameObject>(ammoBox => {
            if (dac.PlacerSaver != null) {
              Dbg.Log("Calling PlacerSaver.OnAmmoCrateLootSpawned");
              dac.PlacerSaver.OnAmmoCrateLootSpawned(dac.ObjectDestructable,
                                                     dac.Spawnable, ammoBox);
            }
          }));
    }
  }

  [HarmonyPatch(typeof(ObjectDestructable),
                nameof(ObjectDestructable.TakeDamage))]
  class ObjectDestructable_TakeDamage_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(ObjectDestructable __instance) {
      Dbg.Log("ObjectDestructable_TakeDamage_Patch");
      if (__instance.lootTable == null ||
          !__instance.lootTable.name.StartsWith("AmmoCrateTable_"))
        return;

      Dbg.Log("Ammo crate took damage");
      if (__instance._isDead) {
        Dbg.Log("Ammo crate is already dead");
        return;
      }

      _damagedAmmoCrates.Add(__instance);
    }

    [HarmonyPostfix()]
    internal static void Postfix(ObjectDestructable __instance) {
      if (!_damagedAmmoCrates.Contains(__instance))
        return;
      _damagedAmmoCrates.Remove(__instance);

      if (!__instance._isDead)
        return;

      Dbg.Log(
          $"Ammo crate destroyed at {__instance.spawnTarget.position.ToString()}");

      _destroyedAmmoCrates.Add(new DestroyedAmmoCrate() {
        TimeDestroyed = Time.time,
        SpawnPosition = __instance.spawnTarget.position,
        SpawnRotation = __instance.spawnTarget.rotation,
        ObjectDestructable = __instance,
        Spawnable = __instance.lootTable.GetLootItem(),
        PlacerSaver = __instance.GetComponent<PlacerSaver>(),
      });
    }
  }

  [HarmonyPatch(typeof(RigManager), nameof(RigManager.Awake))]
  class RigManager_Awake_Patch {
    [HarmonyPostfix()]
    internal static void Postfix() {
      _damagedAmmoCrates.Clear();
      _destroyedAmmoCrates.Clear();
    }
  }
}
}
