using UnityEngine;
using MelonLoader;
using StressLevelZero.Props;
using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using StressLevelZero.Data;

namespace Sst.Features {
class LootChanceViewer : Feature {
  private Camera cam;
  private HashSet<TextMeshPro> tmps = new HashSet<TextMeshPro>();

  private GameObject wristText;
  private ObjectDestructable heldDestructable;

  public LootChanceViewer() { IsDev = true; }

  public override void OnLevelStart(int sceneIdx) {
    tmps = new HashSet<TextMeshPro>();
    cam = Camera.main;

    var destructables = GameObject.FindObjectsOfType<ObjectDestructable>();
    foreach (var destructable in destructables) {
      var items = destructable.lootTable?.items;
      if (items == null || items.Length == 0)
        continue;

      var go = new GameObject("LootChanceViewer_text");

      var tmp = go.AddComponent<TextMeshPro>();
      tmp.alignment = TextAlignmentOptions.Center;
      tmp.fontSize = 0.3f;
      tmp.color = Color.red;
      tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.8f);
      tmp.text = GetLootText(destructable.lootTable);
      go.transform.localPosition = Vector3.zero;
      go.transform.SetParent(destructable.transform, false);
      tmps.Add(tmp);
    }

    LogPositionsOfLoot(destructables, "Golf Club");
    LogPositionsOfLoot(destructables, "Baseball");
    LogPositionsOfLoot(destructables, "Baton");
  }

  private void LogPositionsOfLoot(IEnumerable<ObjectDestructable> destructables,
                                  string title) {
    var found = destructables.Select(
        destructable => destructable.lootTable?.items?.First(
            item => item.spawnable.title == title));
    foreach (var destructable in destructables) {
      var items = destructable.lootTable?.items;
      if (items == null)
        continue;
      var lootTotal = items.Aggregate(
          0f, (acc, item) =>
                  item.spawnable.title == title ? acc + item.percentage : acc);
      if (lootTotal == 0f)
        continue;
      var total = items.Aggregate(0f, (acc, item) => acc + item.percentage);
      var percentage = (lootTotal / total * 100f).ToString("F1");
      MelonLogger.Msg(
          $"Found loot [{title}] with {percentage}% chance at {destructable.transform.position.ToString()}");
    }
  }

  public override void OnLateUpdate() {
    foreach (var tmp in tmps) {
      if (tmp == null)
        continue;

      var toCamera = cam.transform.position - tmp.transform.position;
      toCamera.y = 0;
      tmp.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
      tmp.transform.localPosition = tmp.transform.rotation * Vector3.up;
    }

    var heldObject = GetHeldDestructable();
    if (heldObject != heldDestructable) {
      if (wristText)
        GameObject.Destroy(wristText);
      heldDestructable = heldObject;

      if (heldObject && heldObject.lootTable?.items != null) {
        wristText = new GameObject("LootChanceViewer_wristText");
        var tmp = wristText.AddComponent<TextMeshPro>();
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        tmp.fontSize = 0.2f;
        tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
        wristText.transform.SetParent(
            Mod.GameState.rigManager.ControllerRig.leftController.transform);
        tmp.rectTransform.localPosition = new Vector3(0.1f, 0.24f, 0f);
        tmp.rectTransform.localRotation = Quaternion.Euler(46f, 356f, 3f);
        tmp.text = GetLootText(heldDestructable.lootTable);
      }
    }
  }

  private ObjectDestructable GetHeldDestructable() {
    var attachedObject = Mod.GameState.rigManager?.physicsRig.leftHand
                             .m_CurrentAttachedObject?.transform;
    while (attachedObject) {
      var held = attachedObject.GetComponent<ObjectDestructable>();
      if (held)
        return held;
      attachedObject = attachedObject.parent;
    }
    return null;
  }

  private string GetLootText(LootTableData lootTable) {
    if (!lootTable)
      return "";
    var sortedItems =
        lootTable.items.OrderBy(item => item.percentage).ToArray();
    var total = sortedItems.Aggregate(0f, (acc, item) => acc + item.percentage);
    var lines = sortedItems.Select(item => {
      var percentage = (item.percentage / total * 100f).ToString("F1");
      return $"[{percentage}%] {item.spawnable.title}";
    });
    return string.Join("\n", lines);
  }
}
}
