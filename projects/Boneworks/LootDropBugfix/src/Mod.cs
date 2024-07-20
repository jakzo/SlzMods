using MelonLoader;
using HarmonyLib;
using UnityEngine;
using StressLevelZero.Data;
using System.Linq;

namespace Sst.LootDropBugfix;

public class Mod : MelonMod {
  public static Mod Instance;

  private MelonPreferences_Entry<bool> _prefEnabled;
  private AmmoDebugger _ammoDebugger;

  public Mod() { Instance = this; }

  public override void OnApplicationStart() {
    var prefCategory = MelonPreferences.CreateCategory(BuildInfo.NAME);
    _prefEnabled = prefCategory.CreateEntry("enabled", true, "Enabled",
                                            "Activates the fix for loot drops");
    _ammoDebugger = new AmmoDebugger(prefCategory);
  }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    _ammoDebugger.OnLevelStart();
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
    internal static bool Prefix(LootTableData __instance,
                                ref SpawnableObject __result) {
      if (!Instance._prefEnabled.Value)
        return true;

      __result = GetLootItemFixed(__instance);
      return false;
    }

    [HarmonyPostfix()]
    internal static void Postfix(LootTableData __instance,
                                 SpawnableObject __result) {
      Instance._ammoDebugger.OnGetLootItem(__instance, __result);
    }
  }
}
