using System.Linq;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using StressLevelZero.Props;
using StressLevelZero.Pool;
using StressLevelZero.Data;

namespace Sst.LootDropBugfix;

public class AmmoDebugger {
  private static Il2CppSystem.Nullable<bool> _emptyNullableBool =
      new Utilities.Il2CppNullable<bool>(null);
  private static Il2CppSystem.Nullable<Color> _emptyNullableColor =
      new Utilities.Il2CppNullable<Color>(null);

  private MelonPreferences_Entry<bool> _prefTest;
  private MelonPreferences_Entry<float> _prefTestSpeed;

  public AmmoDebugger(MelonPreferences_Category prefCategory) {
    _prefTest = prefCategory.CreateEntry(
        "test", false, "Test", "Spawns and breaks ammo boxes to test the fix");
    _prefTestSpeed = prefCategory.CreateEntry(
        "test_speed", 1f, "Test speed",
        "Rate at which the test ammo boxes spawn and break");
    _prefTest.OnValueChanged += (a, b) => OnLevelStart();
  }

  public void OnLevelStart() {
    if (_prefTest.Value)
      MelonCoroutines.Start(SpawnAndBreakAmmoCrates());
  }

  public IEnumerator SpawnAndBreakAmmoCrates() {
    var head = Object.FindObjectOfType<StressLevelZero.Rig.RigManager>()
                   ?.physicsRig?.m_head;
    // TODO: Could use the hover junkers ship ammo box as a prefab because it
    // is always loaded
    var cratePrefab = Object.FindObjectsOfType<ObjectDestructable>()
                          .FirstOrDefault(obj => obj.lootTable?.name.StartsWith(
                                                     "AmmoCrateTable_") ??
                                                 false)
                          ?.gameObject;

    var numDestroyed = 0;
    var numMissingLootItem = 0;
    while (head != null && cratePrefab != null && _prefTest.Value) {

      var crate = Object.Instantiate(cratePrefab.gameObject,
                                     head.position +
                                         head.rotation * new Vector3(0, 0, 2),
                                     Quaternion.identity);
      yield return new WaitForSeconds(0.2f / _prefTestSpeed.Value);

      if (crate == null) {
        yield return new WaitForSeconds(0.5f / _prefTestSpeed.Value);
        continue;
      }

      var obj = crate.GetComponent<ObjectDestructable>();
      var prefabNames =
          obj.lootTable.items.Select(item => item.spawnable.prefab.name);
      var spawnPosition = obj.spawnTarget.position;
      obj.TakeDamage(Vector3.back, 100, false,
                     StressLevelZero.Combat.AttackType.Piercing);

      numDestroyed++;
      var spawnedLootItem =
          FindSpawnedLootItem(prefabNames, spawnPosition, false);
      if (spawnedLootItem == null)
        numMissingLootItem++;
      MelonLogger.Msg($"Loot bugs: {numMissingLootItem} / {numDestroyed}");
      yield return new WaitForSeconds(0.5f / _prefTestSpeed.Value);

      spawnedLootItem?.GetComponent<Poolee>().Despawn(_emptyNullableBool,
                                                      _emptyNullableColor);
      yield return new WaitForSeconds(0.2f / _prefTestSpeed.Value);
    }
  }

  private GameObject FindSpawnedLootItem(IEnumerable<string> prefabNames,
                                         Vector3 spawnPosition,
                                         bool checkExactPosition) {
    foreach (var collider in Physics.OverlapSphere(spawnPosition, 0.5f)) {
      var topLevelObject = collider.transform;
      while (topLevelObject.parent)
        topLevelObject = topLevelObject.parent;
      var isCorrectPosition =
          !checkExactPosition ||
          (topLevelObject.position - spawnPosition).sqrMagnitude < 1e-9;
      if (isCorrectPosition &&
          prefabNames.Any(topLevelObject.name.StartsWith)) {
        return topLevelObject.gameObject;
      }
    }
    return null;
  }

  public static bool IsLootGuaranteed(LootTableData lootTable) {
    var totalPercentage =
        lootTable.items.Aggregate(0f, (total, item) => total + item.percentage);
    return totalPercentage >= 100f;
  }

  public void OnGetLootItem(LootTableData lootTable, SpawnableObject result) {
    if (_prefTest.Value && IsLootGuaranteed(lootTable) && result == null) {
      MelonLogger.Warning(
          "LootTableData.GetLootItem just returned null when loot chances add up to 100%!");
    }
  }
}
