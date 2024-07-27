using MelonLoader;
using HarmonyLib;
using UnityEngine;
using StressLevelZero.Data;
using Valve.VR;
using StressLevelZero.Props;
using StressLevelZero.Utilities;
using StressLevelZero.Rig;

namespace Sst.LootDropBugfix;

public class Mod : MelonMod {
  public static Mod Instance;

  private MelonPreferences_Entry<bool> _prefEnabled;
  private AmmoDebugger _ammoDebugger;
  private bool _isLoading = false;

  public Mod() { Instance = this; }

  public override void OnApplicationStart() {
    var prefCategory = MelonPreferences.CreateCategory(BuildInfo.NAME);
    _prefEnabled = prefCategory.CreateEntry(
        "enabled", true, "Enabled", "Activates the fix for loot drops"
    );
    _ammoDebugger = new AmmoDebugger(prefCategory);
  }

  // TODO: Find way which works in Oculus too
  [HarmonyPatch(typeof(CVRCompositor), nameof(CVRCompositor.FadeGrid))]
  class CVRCompositor_FadeGrid_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(bool bFadeIn) {
      if (bFadeIn) {
        Instance._isLoading = true;
      } else {
        Instance._isLoading = false;
        Instance._ammoDebugger.OnLevelStart();
      }
    }
  }

  public static SpawnableObject GetLootItemFixed(LootTableData lootTable) {
    var n = Random.RandomRange(0, 100); // updated in Bonelab patch 4 to 1/100
    var lower = 0f;
    for (var i = 0; i < lootTable.items.Length; i++) {
      var item = lootTable.items[i];
      var upper = lower + item.percentage;
      if (lower <= n && n < upper) { // changed, used to be lower exclusive
        return item.spawnable;
      }
      lower = upper;
    }
    return null;
  }

  [HarmonyPatch(typeof(LootTableData), nameof(LootTableData.GetLootItem))]
  class LootTableData_GetLootItem_Patch {
    [HarmonyPrefix()]
    internal static bool
    Prefix(LootTableData __instance, ref SpawnableObject __result) {
      if (!Instance._prefEnabled.Value)
        return true;

      __result = GetLootItemFixed(__instance);
      return false;
    }

    [HarmonyPostfix()]
    internal static void
    Postfix(LootTableData __instance, SpawnableObject __result) {
      Instance._ammoDebugger.OnGetLootItem(__instance, __result);
    }
  }

  // Fixes missing bonebox in runoff
  // name = dest_Crate_Lite_1m Boneworks (2)
  // save item uuid = 13cc9af0-32e7-44a1-a91c-ba2ae3bc1717
  // spawnable title = Capsule Omni Turret
  // spawnable uuid = af0d2c47-f9c9-4323-be80-48ff071eeb37
  [HarmonyPatch(typeof(ObjectDestructable),
                nameof(ObjectDestructable.TakeDamage))]
  class ObjectDestructable_TakeDamage_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(ObjectDestructable __instance, float damage,
                                ref float __state) {
      if (!Instance._isLoading)
        return;
      if (damage > __instance._health)
        Dbg.Log(
            $"Item would have broken but is indestructible before load: {__instance.name}");
      __state = __instance._health;
      __instance._health = float.PositiveInfinity;
    }

    [HarmonyFinalizer()]
    internal static void Finalizer(ObjectDestructable __instance,
                                   float __state) {
      if (!Instance._isLoading)
        return;
      __instance._health = __state;
    }
  }

// RUNOFF BONEBOX TESTING
#if DEBUG
  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    if (buildIndex == 1) {
      Dbg.Log("Menu loaded");
      var timer = new System.Timers.Timer(2000);
      timer.Elapsed += (x, y) => {
        Dbg.Log("Loading runoff");
        BoneworksSceneManager.LoadScene("Runoff");
      };
      timer.AutoReset = false;
      timer.Enabled = true;
    } else if (buildIndex == 6) {
      Dbg.Log("Runoff loaded");
      var timer = new System.Timers.Timer(1000);
      timer.Elapsed += (x, y) => {
        Dbg.Log("Teleporting");
        GameObject.FindObjectOfType<RigManager>().Teleport(
            new Vector3(-20.1f, 19.0f, -64.6f), Vector3.right);
      };
      timer.AutoReset = false;
      timer.Enabled = true;
    }
  }
#endif
}
