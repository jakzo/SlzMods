using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MelonLoader;

namespace Sst {
class Collectible {
  public CollectibleType Type;
  public GameObject GameObject;
  public float Distance;

  public bool
  ShouldShow() => !GameObject.WasCollected &&
                  !IsInPool(GameObject) && GameObject.scene.isLoaded;

  private static bool IsInPool(GameObject gameObject) =>
      gameObject.transform.parent
      && gameObject.transform.parent.name.StartsWith("Pool -");
}

class CollectibleType {
  public static CollectibleType GACHA_CAPSULE = new CollectibleType(
      "Gacha capsule", false, () => _cachedGachaCapsules.Where(gc => !gc.used));
  public static CollectibleType GACHA_PLACER = new CollectibleType(
      "Gacha placer", false,
      () => _cachedGachaPlacers.Where(gp => !gp.onlyPlaceIfBeatGame &&
                                            !gp.cratePlacer.placed));
  public static CollectibleType GACHA_PLACER_FINISHED = new CollectibleType(
      "Gacha placer (only if finished)", false,
      () => _cachedGachaPlacers.Where(gp => gp.onlyPlaceIfBeatGame &&
                                            !gp.cratePlacer.placed));
  public static CollectibleType AMMO_LIGHT = new CollectibleType(
      "Light ammo", true,
      () => _cachedAmmoPickups.Where(ap => ap.ammoGroup.KeyName == "light" &&
                                           !ap._isCollected));
  public static CollectibleType AMMO_LIGHT_CRATE = new CollectibleType(
      "Light ammo crate", true,
      () => _cachedAssetPoolees.Where(ap => ap.spawnableCrate.name ==
                                            "Ammo - Dest Box Light"));
  public static CollectibleType AMMO_MEDIUM = new CollectibleType(
      "Medium ammo", true,
      () => _cachedAmmoPickups.Where(ap => ap.ammoGroup.KeyName == "medium" &&
                                           !ap._isCollected));
  public static CollectibleType AMMO_MEDIUM_CRATE = new CollectibleType(
      "Medium ammo crate", true,
      () => _cachedAssetPoolees.Where(ap => ap.spawnableCrate.name ==
                                            "Ammo - Dest Box Medium"));
  public static CollectibleType AMMO_HEAVY = new CollectibleType(
      "Heavy ammo", true,
      () => _cachedAmmoPickups.Where(ap => ap.ammoGroup.KeyName == "heavy" &&
                                           !ap._isCollected));
  public static CollectibleType AMMO_HEAVY_CRATE = new CollectibleType(
      "Heavy ammo crate", true,
      () => _cachedAssetPoolees.Where(ap => ap.spawnableCrate.name ==
                                            "Ammo - Dest Box Heavy"));

  private static GachaCapsule[] _cachedGachaCapsules;
  private static GachaPlacer[] _cachedGachaPlacers;
  private static SLZ.Marrow.Pool.AssetPoolee[] _cachedAssetPoolees;
  private static SLZ.AmmoPickup[] _cachedAmmoPickups;

  public static CollectibleType[] ALL = {
    GACHA_CAPSULE,     AMMO_LIGHT, AMMO_LIGHT_CRATE, AMMO_MEDIUM,
    AMMO_MEDIUM_CRATE, AMMO_HEAVY, AMMO_HEAVY_CRATE,
  };

  public static CollectibleType[] EnabledTypes = {};

  public static Action[] CacheActions = {
    () => { EnabledTypes = ALL.Where(type => type.Pref.Value).ToArray(); },
    () => {
      _cachedGachaCapsules = Resources.FindObjectsOfTypeAll<GachaCapsule>();
    },
    () => {
      _cachedGachaPlacers = Resources.FindObjectsOfTypeAll<GachaPlacer>();
    },
    () => {
      _cachedAssetPoolees =
          Resources.FindObjectsOfTypeAll<SLZ.Marrow.Pool.AssetPoolee>();
    },
    () => {
      _cachedAmmoPickups = Resources.FindObjectsOfTypeAll<SLZ.AmmoPickup>();
    },
  };

  public static void Initialize(MelonPreferences_Category prefsCategory) {
    foreach (var type in ALL)
      type.Pref = prefsCategory.CreateEntry(
          type.Name.ToLower().Replace(' ', '_'), true, type.Name);
  }

  public string Name;
  public Func<MonoBehaviour[]> FindAll;
  public MelonPreferences_Entry<bool> Pref;
  public CollectibleType(string name, bool onlyDisplayInCampaign,
                         Func<IEnumerable<MonoBehaviour>> findAll) {
    Name = name;
    FindAll = () => onlyDisplayInCampaign && !IsInCampaignLevel()
                        ? new MonoBehaviour[] {}
                        : findAll().ToArray();
  }

  private static bool IsInCampaignLevel() {
    var barcode = Utilities.LevelHooks.CurrentLevel?.Barcode.ID;
    if (barcode == null)
      return false;
    return Utilities.Levels.CAMPAIGN_LEVEL_BARCODES.Contains(barcode);
  }
}
}
