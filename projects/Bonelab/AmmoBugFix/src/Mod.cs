using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using SLZ.Props;
using SLZ.Marrow.Warehouse;
using SLZ;
using SLZ.Marrow.Pool;
using SLZ.Marrow.SceneStreaming;

namespace Sst.AmmoBugFix {
public class Mod : MelonMod {
  protected static HashSet<ObjectDestructable> _damagedAmmoCrates =
      new HashSet<ObjectDestructable>();
  protected static Il2CppSystem.Nullable<Vector3> _nullableVector =
      new Utilities.Il2CppNullable<Vector3>(new Vector3());
  protected static Il2CppSystem.Nullable<int> _nullableInt =
      new Utilities.Il2CppNullable<int>(null);

  public override void OnInitializeMelon() { Dbg.Init(BuildInfo.NAME); }

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

      // TODO: Will the position always exactly match or could there be float
      // rounding errors?
      var ammoPickups = GameObject.FindObjectsOfType<AmmoPickupProxy>().Where(
          ap => (ap.transform.position - __instance.spawnTarget.position)
                    .sqrMagnitude < 0.000001f);
      if (ammoPickups.Count() > 0) {
        Dbg.Log("Spawned ammo found");
        return;
      }

      Dbg.Log("No spawned ammo found, spawning replacement now");
      _nullableVector.value = __instance.spawnTarget.lossyScale;
      AssetSpawner.Spawn(__instance.lootTable.GetLootItem(),
                         __instance.spawnTarget.position,
                         __instance.spawnTarget.rotation, _nullableVector,
                         false, _nullableInt);
    }
  }

  [HarmonyPatch(typeof(SceneStreamer), nameof(SceneStreamer.Load),
                new System.Type[] { typeof(LevelCrateReference),
                                    typeof(LevelCrateReference) })]
  class SceneStreamer_Load_Patch {
    [HarmonyPrefix()]
    internal static void Prefix() { _damagedAmmoCrates.Clear(); }
  }
}
}
