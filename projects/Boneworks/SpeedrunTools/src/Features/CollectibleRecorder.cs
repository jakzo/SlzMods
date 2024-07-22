using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using MelonLoader;
using HarmonyLib;
using StressLevelZero;
using StressLevelZero.Combat;
using StressLevelZero.Pool;
using StressLevelZero.Props.Weapons;
using StressLevelZero.Data;
using Sst.Common.Boneworks;

namespace Sst.Features {
class CollectibleRecorder : Feature {
  public static readonly string RECORDINGS_DIR =
      Path.Combine(Utils.DIR, "collectible_recordings");
  public static readonly string ORDER_DIR =
      Path.Combine(Utils.DIR, "collectible_order");

  public static CollectibleRecorder Instance;
  public static event OnCollectedHandler OnCollected;
  public delegate void
  OnCollectedHandler(HundredPercentState.Collectible collectible);

  public bool IsSceneWithCollectibleAmmo = false;
  public List<HundredPercentState.Collectible> CurrentLevelCollectedItems;

  public CollectibleRecorder() {
    IsAllowedInRuns = true;
    Instance = this;
  }

  public override void OnLevelStart(int sceneIdx) {
    IsSceneWithCollectibleAmmo = sceneIdx >= 2 && sceneIdx <= 10;
    CurrentLevelCollectedItems = new List<HundredPercentState.Collectible>();
  }

  public override void OnLoadingScreen(int nextSceneIdx, int prevSceneIdx) {
    if (CurrentLevelCollectedItems != null &&
        CurrentLevelCollectedItems?.Count > 0) {
      var items = CurrentLevelCollectedItems;
      Task.Run(() => SaveRecording(prevSceneIdx, items));
    }

    OnDisabled();
  }

  private string GetFilename(int sceneIdx) =>
      Utils.SCENE_NAME_BY_INDEX.TryGetValue(sceneIdx, out var sceneName)
          ? Regex.Replace(sceneName, "^(scene|sandbox)_", "") + ".txt"
          : null;

  public override void OnDisabled() {
    IsSceneWithCollectibleAmmo = false;
    CurrentLevelCollectedItems = null;
  }

  private void SaveRecording(int sceneIdx,
                             List<HundredPercentState.Collectible> items) {
    var filename = GetFilename(sceneIdx);
    if (filename == null) {
      MelonLogger.Warning("Scene name for index not known: " + sceneIdx);
      return;
    }

    Dbg.Log("CollectibleRecorder.SaveRecording", filename, items.Count);
    Directory.CreateDirectory(RECORDINGS_DIR);
    File.WriteAllLines(
        Path.Combine(RECORDINGS_DIR, filename),
        items.Select(item => $"{item.Type} {item.Uuid} {item.DisplayName}"));
  }

  public HundredPercentState.Collectible[] LoadRecording(int sceneIdx) {
    var filename = GetFilename(sceneIdx);
    if (filename == null) {
      MelonLogger.Warning("Scene name for index not known: " + sceneIdx);
      return null;
    }

    Dbg.Log("LoadRecording for " + filename);
    try {
      return File.ReadAllLines(Path.Combine(ORDER_DIR, filename))
          .Select(line => {
            var match = Regex.Match(line.Trim(), "^(\\S+)\\s+(\\S+)\\s+(.+)$");
            if (!match.Success)
              return null;
            return new HundredPercentState.Collectible() {
              Type = match.Groups[1].Value,
              Uuid = match.Groups[2].Value,
              DisplayName = match.Groups[3].Value,
            };
          })
          .Where(collectible => collectible != null)
          .ToArray();
    } catch (FileNotFoundException) {
      return null;
    }
  }

  public void SaveDefaultRecordingsIfNecessary() {
    if (Directory.Exists(ORDER_DIR))
      return;

    Directory.CreateDirectory(ORDER_DIR);
    Utilities.Resources.ExtractResource("DefaultCollectibleOrder.zip",
                                        ORDER_DIR);
  }

  private static void OnItemCollected(string type, string uuid,
                                      string displayName) {
    if (Instance.CurrentLevelCollectedItems == null)
      return;

    Dbg.Log("CollectibleRecorder.OnItemCollected", type, displayName, uuid);
    var collectible = new HundredPercentState.Collectible() {
      Type = type,
      Uuid = uuid,
      DisplayName = displayName,
    };
    Instance.CurrentLevelCollectedItems.Add(collectible);
    OnCollected?.Invoke(collectible);
  }

  private static void OnAmmoCollected(Weight weight, SaveItem saveItem) {
    var type = weight == Weight.LIGHT    ? HundredPercentState.TYPE_AMMO_LIGHT
               : weight == Weight.MEDIUM ? HundredPercentState.TYPE_AMMO_MEDIUM
                                         : null;
    if (type != null && Instance.IsSceneWithCollectibleAmmo)
      OnItemCollected(type, saveItem.UUID, saveItem.name);
  }

  [HarmonyPatch(typeof(AmmoPickup), nameof(AmmoPickup.OnTriggerEnter))]
  class AmmoPickup_OnTriggerEnter_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(AmmoPickup __instance, ref bool __state) {
      __state = __instance._isCollected;
    }

    [HarmonyPostfix()]
    internal static void Postfix(AmmoPickup __instance, ref bool __state) {
      if (!__state && __instance._isCollected)
        OnAmmoCollected(__instance.ammoWeight, __instance.saveItem);
    }
  }

  [HarmonyPatch(typeof(Magazine), nameof(Magazine.OnGrab))]
  class Magazine_OnGrab_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(Magazine __instance, ref bool __state) {
      __state = __instance.IsAmmoClaimed;
    }

    [HarmonyPostfix()]
    internal static void Postfix(Magazine __instance, ref bool __state) {
      if (!__state && __instance.IsAmmoClaimed)
        OnAmmoCollected(__instance.magazineData.weight, __instance.saveItem);
    }
  }

  [HarmonyPatch(typeof(ReclaimerData), nameof(ReclaimerData.AddObject))]
  class ReclaimerData_AddObject_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(string keycode) {
      OnItemCollected(
          HundredPercentState.TYPE_ITEM, keycode,
          PoolManager._registeredSpawnableObjects.ContainsKey(keycode)
              ? PoolManager._registeredSpawnableObjects[keycode].title
              : "UNKNOWN");
    }
  }
}
}
