using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MelonLoader;
using SLZ.Bonelab;
using SLZ.Data;

namespace Sst {
class Collectible {
  public CollectibleType Type;
  public GameObject GameObject;
  public float Distance;
}

class CollectibleType {
  public static CollectibleType GACHA_CAPSULE = new CollectibleType(
      "Gacha capsule", false,
      () => _cachedGachaCapsules.Where(
          gc => ShouldShow(gc.gameObject) && !gc.used &&
                !_unlockedCrateBarcodes.Contains(gc.selectedCrate.Barcode.ID)));
  public static CollectibleType GACHA_PLACER = new CollectibleType(
      "Gacha placer", false,
      () => _cachedGachaPlacers.Where(
          gp => ShouldShow(gp.gameObject) && !gp.onlyPlaceIfBeatGame &&
                !gp.cratePlacer.placed &&
                !_unlockedCrateBarcodes.Contains(gp.unlockCrate.Barcode.ID)));
  public static CollectibleType GACHA_PLACER_FINISHED = new CollectibleType(
      "Gacha placer (after beating game)", false,
      () => _cachedGachaPlacers.Where(
          gp => ShouldShow(gp.gameObject) && gp.onlyPlaceIfBeatGame &&
                !gp.cratePlacer.placed &&
                !_unlockedCrateBarcodes.Contains(gp.unlockCrate.Barcode.ID)));
  public static CollectibleType AMMO_LIGHT = new CollectibleType(
      "Light ammo", true, FindSaveables("prop_ammoBox_light"));
  public static CollectibleType AMMO_LIGHT_CRATE = new CollectibleType(
      "Light ammo crate", true, FindSaveables("dest_ammoBoxLight Variant"));
  public static CollectibleType AMMO_MEDIUM = new CollectibleType(
      "Medium ammo", true, FindSaveables("prop_ammoBox_med"));
  public static CollectibleType AMMO_MEDIUM_CRATE = new CollectibleType(
      "Medium ammo crate", true, FindSaveables("dest_ammoBoxMedium Variant"));
  public static CollectibleType AMMO_HEAVY = new CollectibleType(
      "Heavy ammo", true, FindSaveables("prop_ammoBox_hvy"));
  public static CollectibleType AMMO_HEAVY_CRATE = new CollectibleType(
      "Heavy ammo crate", true, FindSaveables("dest_ammoBoxHeavy Variant"));

  private static Func<IEnumerable<Saveable>> FindSaveables(string name) {
    var prefix = $"{name} [";
    return () => _cachedSaveables.Where(obj => ShouldShow(obj.gameObject) &&
                                               obj.name.StartsWith(prefix) &&
                                               obj.Data != "yoinked");
  }

  private static GachaCapsule[] _cachedGachaCapsules;
  private static GachaPlacer[] _cachedGachaPlacers;
  private static Saveable[] _cachedSaveables;
  private static HashSet<string> _unlockedCrateBarcodes;

  public static CollectibleType[] ALL = {
    GACHA_CAPSULE,     GACHA_PLACER,     GACHA_PLACER_FINISHED,
    AMMO_LIGHT,        AMMO_LIGHT_CRATE, AMMO_MEDIUM,
    AMMO_MEDIUM_CRATE, AMMO_HEAVY,       AMMO_HEAVY_CRATE,
  };

  public static CollectibleType[] EnabledTypes = {};

  public static Action[] CacheActions = {
    () => { EnabledTypes = ALL.Where(type => type.Pref.Value).ToArray(); },
    () => {
      _unlockedCrateBarcodes = DataManager.ActiveSave.Unlocks.Unlocks._entries
                                   .Select(entry => entry.key)
                                   .ToHashSet();
    },
    () => {
      _cachedGachaCapsules = Resources.FindObjectsOfTypeAll<GachaCapsule>();
    },
    () => {
      _cachedGachaPlacers = Resources.FindObjectsOfTypeAll<GachaPlacer>();
    },
    () => { _cachedSaveables = Resources.FindObjectsOfTypeAll<Saveable>(); },
  };

  public static void Initialize(MelonPreferences_Category prefsCategory) {
    foreach (var type in ALL)
      type.Pref = prefsCategory.CreateEntry(
          type.Name.ToLower().Replace(' ', '_'), true, type.Name);
  }

  public static bool ShouldShow(GameObject gameObject) =>
      gameObject != null && !IsInPool(gameObject) && gameObject.scene.isLoaded;

  private static bool IsInPool(GameObject gameObject) =>
      gameObject.transform.parent
      && gameObject.transform.parent.name.StartsWith("Pool -");

  public string Name;
  public Func<MonoBehaviour[]> FindAll;
  public MelonPreferences_Entry<bool> Pref;
  public CollectibleType(string name, bool mustBeSaveable,
                         Func<IEnumerable<MonoBehaviour>> findAll) {
    Name = name;
    FindAll = () => findAll().ToArray();
  }
}
}
