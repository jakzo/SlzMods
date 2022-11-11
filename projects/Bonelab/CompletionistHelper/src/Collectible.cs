using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MelonLoader;
using SLZ.Bonelab;

namespace Sst {
class Collectible<T>
    where T : MonoBehaviour {
  public CollectibleType<T> Type;
  public GameObject GameObject;
  public float Distance;
}

class CollectibleType<T>
    where T : MonoBehaviour {
  public static CollectibleType<GachaCapsule> GACHA_CAPSULE =
      new CollectibleType<GachaCapsule>(
          "Gacha capsule", false,
          () => _cachedGachaCapsules.Where(gc => ShouldShow(gc) && !gc.used));
  public static CollectibleType<GachaPlacer> GACHA_PLACER =
      new CollectibleType<GachaPlacer>(
          "Gacha placer", false,
          () => _cachedGachaPlacers.Where(gp => ShouldShow(gp) &&
                                                !gp.onlyPlaceIfBeatGame &&
                                                !gp.cratePlacer.placed));
  public static CollectibleType<GachaPlacer> GACHA_PLACER_FINISHED =
      new CollectibleType<GachaPlacer>(
          "Gacha placer (only if finished)", false,
          () => _cachedGachaPlacers.Where(gp => ShouldShow(gp) &&
                                                gp.onlyPlaceIfBeatGame &&
                                                !gp.cratePlacer.placed));
  public static CollectibleType<Saveable> AMMO_LIGHT =
      new CollectibleType<Saveable>("Light ammo", true,
                                    FindSaveables("prop_ammoBox_light"));
  public static CollectibleType<Saveable> AMMO_LIGHT_CRATE =
      new CollectibleType<Saveable>("Light ammo crate", true,
                                    FindSaveables("dest_ammoBoxLight Variant"));
  public static CollectibleType<Saveable> AMMO_MEDIUM =
      new CollectibleType<Saveable>("Medium ammo", true,
                                    FindSaveables("prop_ammoBox_med"));
  public static CollectibleType<Saveable> AMMO_MEDIUM_CRATE =
      new CollectibleType<Saveable>(
          "Medium ammo crate", true,
          FindSaveables("dest_ammoBoxMedium Variant"));
  public static CollectibleType<Saveable> AMMO_HEAVY =
      new CollectibleType<Saveable>("Heavy ammo", true,
                                    FindSaveables("prop_ammoBox_hvy"));
  public static CollectibleType<Saveable> AMMO_HEAVY_CRATE =
      new CollectibleType<Saveable>("Heavy ammo crate", true,
                                    FindSaveables("dest_ammoBoxHeavy Variant"));

  private static Func<IEnumerable<Saveable>> FindSaveables(string name) {
    var prefix = $"{name} [";
    return () => _cachedSaveables.Where(obj => ShouldShow(obj) &&
                                               obj.name.StartsWith(prefix) &&
                                               obj.Data != "yoinked");
  }

  private static GachaCapsule[] _cachedGachaCapsules;
  private static GachaPlacer[] _cachedGachaPlacers;
  private static Saveable[] _cachedSaveables;

  public static CollectibleType<MonoBehaviour>[] ALL = {
    GACHA_CAPSULE,     GACHA_PLACER,     GACHA_PLACER_FINISHED,
    AMMO_LIGHT,        AMMO_LIGHT_CRATE, AMMO_MEDIUM,
    AMMO_MEDIUM_CRATE, AMMO_HEAVY,       AMMO_HEAVY_CRATE,
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
    () => { _cachedSaveables = Resources.FindObjectsOfTypeAll<Saveable>(); },
  };

  public static void Initialize(MelonPreferences_Category prefsCategory) {
    foreach (var type in ALL)
      type.Pref = prefsCategory.CreateEntry(
          type.Name.ToLower().Replace(' ', '_'), true, type.Name);
  }

  public static bool ShouldShow(MonoBehaviour obj) =>
      obj.gameObject != null &&
      !IsInPool(obj.gameObject) && obj.gameObject.scene.isLoaded;

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
