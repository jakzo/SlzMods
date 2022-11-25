using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using MelonLoader;
using SLZ.Bonelab;
using SLZ.Props;
using SLZ.SaveData;
using SLZ.Interaction;

namespace Sst.CompletionistHelper {
class Collectible {
  public CollectibleType Type;
  public GameObject GameObject;
  public float Distance;
}

class CollectibleType {
  public static CollectibleType GACHA_CAPSULE = new CollectibleType(
      "Gacha capsule", false,
      () => _cachedGachaCapsules.Where(
          gc => ShouldShow(gc) && !gc.used &&
                !_unlockedCrateBarcodes.Contains(gc.selectedCrate.Barcode.ID)));
  public static CollectibleType GACHA_PLACER = new CollectibleType(
      "Gacha spawn point", false,
      () => _cachedGachaPlacers.Where(
          gp => ShouldShow(gp) && !gp.onlyPlaceIfBeatGame &&
                !gp.cratePlacer.placed &&
                !_unlockedCrateBarcodes.Contains(gp.unlockCrate.Barcode.ID)));
  public static CollectibleType GACHA_PLACER_FINISHED = new CollectibleType(
      "Gacha spawn point (after beating game)", false,
      () => _cachedGachaPlacers.Where(
          gp => ShouldShow(gp) && gp.onlyPlaceIfBeatGame &&
                !gp.cratePlacer.placed &&
                !_unlockedCrateBarcodes.Contains(gp.unlockCrate.Barcode.ID)));
  public static CollectibleType AMMO_LIGHT =
      new CollectibleType("Light ammo", true, FindAmmo("prop_ammoBox_light"));
  public static CollectibleType AMMO_LIGHT_CRATE = new CollectibleType(
      "Light ammo crate", true, FindAmmo("dest_ammoBoxLight Variant"));
  public static CollectibleType AMMO_MEDIUM =
      new CollectibleType("Medium ammo", true, FindAmmo("prop_ammoBox_med"));
  public static CollectibleType AMMO_MEDIUM_CRATE = new CollectibleType(
      "Medium ammo crate", true, FindAmmo("dest_ammoBoxMedium Variant"));
  public static CollectibleType AMMO_HEAVY =
      new CollectibleType("Heavy ammo", true, FindAmmo("prop_ammoBox_hvy"));
  public static CollectibleType AMMO_HEAVY_CRATE = new CollectibleType(
      "Heavy ammo crate", true, FindAmmo("dest_ammoBoxHeavy Variant"));
  public static CollectibleType KEYCARD = new CollectibleType(
      "Keycard", false,
      () => _cachedKeycards.Where(
          kc => ShouldShow(kc) &&
                !(kc.GetComponent<InteractableHost>()?._lastHand != null)));
  public static CollectibleType KEYCARD_READER = new CollectibleType(
      "Keycard reader", false,
      () => _cachedKeycardReceivers.Where(
          kr =>
              ShouldShow(kr) && kr._State != KeycardReciever._States.INSERTED));

  private static Func<IEnumerable<AmmoPickupProxy>> FindAmmo(string name) {
    var prefix = $"{name} [";
    return () => _cachedAmmoPickupProxys.Where(
               obj => Utilities.Levels.CAMPAIGN_LEVEL_BARCODES_SET.Contains(
                          Utilities.LevelHooks.CurrentLevel.Barcode.ID) &&
                      ShouldShow(obj) && obj.name.StartsWith(prefix));
  }

  private static GachaCapsule[] _cachedGachaCapsules;
  private static GachaPlacer[] _cachedGachaPlacers;
  private static AmmoPickupProxy[] _cachedAmmoPickupProxys;
  private static Keycard[] _cachedKeycards;
  private static KeycardReciever[] _cachedKeycardReceivers;
  private static HashSet<string> _unlockedCrateBarcodes;

  public static CollectibleType[] ALL = {
    GACHA_CAPSULE,    GACHA_PLACER, GACHA_PLACER_FINISHED, AMMO_LIGHT,
    AMMO_LIGHT_CRATE, AMMO_MEDIUM,  AMMO_MEDIUM_CRATE,     AMMO_HEAVY,
    AMMO_HEAVY_CRATE, KEYCARD,      KEYCARD_READER,
  };

  public static CollectibleType[] EnabledTypes = {};

  public static Action[] CacheActions = {
    () => { EnabledTypes = ALL.Where(type => type.Pref.Value).ToArray(); },
    () => {
      _unlockedCrateBarcodes = DataManager.ActiveSave.Unlocks.Unlocks._entries
                                   ?.Select(entry => entry.key)
                                   .ToHashSet() ??
                               new HashSet<string>();
    },
    () => {
      _cachedGachaCapsules = Resources.FindObjectsOfTypeAll<GachaCapsule>();
    },
    () => {
      _cachedGachaPlacers = Resources.FindObjectsOfTypeAll<GachaPlacer>();
    },
    () => {
      _cachedAmmoPickupProxys =
          Resources.FindObjectsOfTypeAll<AmmoPickupProxy>();
    },
    () => { _cachedKeycards = Resources.FindObjectsOfTypeAll<Keycard>(); },
    () => {
      _cachedKeycardReceivers =
          Resources.FindObjectsOfTypeAll<KeycardReciever>();
    },
  };

  public static void Initialize(MelonPreferences_Category prefsCategory) {
    foreach (var type in ALL)
      type.Pref = prefsCategory.CreateEntry(
          Regex.Replace(type.Name.ToLower().Replace(' ', '_'), @"[()]", ""),
          true, type.Name);
  }

  public static bool ShouldShow(Component obj) => obj != null &&
                                                  ShouldShow(obj.gameObject);
  public static bool
  ShouldShow(GameObject obj) => obj != null &&
                                !IsInPool(obj) && obj.scene.isLoaded;

  private static bool IsInPool(GameObject gameObject) {
    var parent = gameObject.transform.parent;
    return parent && parent.name.StartsWith("Pool -");
  }

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
