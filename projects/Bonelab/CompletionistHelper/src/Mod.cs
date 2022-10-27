using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using MelonLoader;
using SLZ.Marrow.Warehouse;
using SLZ.Rig;

namespace Sst {
public class Mod : MelonMod {
  private const string HUD_TEXT_NAME = "CompletionistHud";
  private const float REFRESH_FREQUENCY = 5;
  private const int NUM_HUD_SLOTS = 5;

  private static MelonPreferences_Category _typesPrefCategory;

  private (GameObject container, TextMeshPro arrow,
           TextMeshPro text)[] _hudSlots;
  private RigManager _rigManager;
  private float _lastRefresh = 0;
  private List<Collectible> _collectibles = new List<Collectible>();

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);

    _typesPrefCategory =
        MelonPreferences.CreateCategory("TypesToShow", "Types to show");

    Utilities.LevelHooks.OnLevelStart.AddListener(
        new System.Action<LevelCrate>(level => ShowHud()));
  }

  public override void OnUpdate() {
    Utilities.LevelHooks.OnUpdate();

    if (!Utilities.LevelHooks.CurrentLevel || _hudSlots == null)
      return;

    if (Time.time - _lastRefresh > REFRESH_FREQUENCY)
      RefreshLocations();

    foreach (var collectible in _collectibles)
      collectible.Distance =
          collectible.GameObject.WasCollected
              ? float.MaxValue
              : Vector3.Distance(
                    _rigManager.ControllerRig.leftController.transform.position,
                    collectible.GameObject.transform.position);
    _collectibles.Sort((x, y) => {
      var delta = x.Distance - y.Distance;
      return delta > 0 ? 1 : delta < 0 ? -1 : 0;
    });

    var displayedCollectibles =
        _collectibles.Where(collectible => !collectible.GameObject.WasCollected)
            .Take(5)
            .ToArray();
    for (var i = 0; i < _hudSlots.Length; i++) {
      if (i >= displayedCollectibles.Length) {
        _hudSlots[i].container.active = false;
        continue;
      }

      var collectible = displayedCollectibles[i];
      var (container, arrow, text) = _hudSlots[i];
      container.active = true;
      arrow.transform.LookAt(collectible.GameObject.transform);
      text.SetText(collectible != null
                       ? $"{collectible.Distance:N1}m {collectible.Type.Name}"
                       : "");
    }
  }

  public void RefreshLocations() {
    _lastRefresh = Time.time;

    CollectibleType.EnabledTypes =
        CollectibleType.ALL.Where(type => type.Pref.Value).ToArray();

    _collectibles = new List<Collectible>();
    foreach (var collectibleType in CollectibleType.ALL) {
      foreach (var gameObject in collectibleType.FindAll()) {
        _collectibles.Add(new Collectible() {
          Type = collectibleType,
          GameObject = gameObject,
        });
      }
    }
  }

  private void ShowHud() {
    _lastRefresh = 0;
    _rigManager = Utilities.Bonelab.GetRigManager();
    var hud = new GameObject("CompletionistHud");
    _hudSlots =
        Enumerable.Range(0, NUM_HUD_SLOTS)
            .Select(i => {
              var hudSlot = new GameObject($"CompletionistHudSlot {i}");
              hudSlot.active = false;
              Utilities.Bonelab.DockToWrist(
                  hudSlot, new Vector3(0, i * 0.2f, 0), _rigManager);

              var arrowGo = new GameObject($"CompletionistHudArrow {i}");
              arrowGo.transform.SetParent(hudSlot.transform);
              var arrow = arrowGo.AddComponent<TextMeshPro>();
              arrow.alignment = TextAlignmentOptions.BottomRight;
              arrow.fontSize = 0.2f;
              arrow.rectTransform.sizeDelta = new Vector2(0.2f, 0.2f);
              arrow.rectTransform.localPosition = new Vector3(-0.2f, 0, 0);
              arrow.rectTransform.localRotation = Quaternion.identity;
              arrow.SetText("⬆");

              var textGo = new GameObject($"CompletionistHudText {i}");
              textGo.transform.SetParent(hudSlot.transform);
              var text = textGo.AddComponent<TextMeshPro>();
              text.alignment = TextAlignmentOptions.BottomLeft;
              text.fontSize = 0.2f;
              text.rectTransform.sizeDelta = new Vector2(0.8f, 0.2f);
              text.rectTransform.localPosition = Vector3.zero;
              text.rectTransform.localRotation = Quaternion.identity;

              return (hudSlot, arrow, text);
            })
            .ToArray();
  }

  class Collectible {
    public CollectibleType Type;
    public GameObject GameObject;
    public float Distance;
  }

  class CollectibleType {
    public static CollectibleType GACHA_CAPSULE = new CollectibleType(
        "Gacha capsule", () => GameObject.FindObjectsOfType<SLZ.GachaShot>()
                                   .Select(test => test.gameObject)
                                   .ToList());
    public static CollectibleType LIGHT_AMMO_CRATE = new CollectibleType(
        "Light ammo crate", () => GameObject.FindObjectsOfType<SLZ.AmmoPickup>()
                                      .Select(test => test.gameObject)
                                      .ToList());
    public static CollectibleType LIGHT_AMMO = new CollectibleType(
        "Light ammo", () => GameObject.FindObjectsOfType<SLZ.AmmoPickup>()
                                .Select(test => test.gameObject)
                                .ToList());
    public static CollectibleType HEAVY_AMMO_CRATE = new CollectibleType(
        "Heavy ammo crate", () => GameObject.FindObjectsOfType<SLZ.AmmoPickup>()
                                      .Select(test => test.gameObject)
                                      .ToList());
    public static CollectibleType HEAVY_AMMO = new CollectibleType(
        "Heavy ammo", () => GameObject.FindObjectsOfType<SLZ.AmmoPickup>()
                                .Select(test => test.gameObject)
                                .ToList());
    public static CollectibleType SHOTGUN_AMMO_CRATE =
        new CollectibleType("Shotgun ammo crate",
                            () => GameObject.FindObjectsOfType<SLZ.AmmoPickup>()
                                      .Select(test => test.gameObject)
                                      .ToList());
    public static CollectibleType SHOTGUN_AMMO = new CollectibleType(
        "Shotgun ammo", () => GameObject.FindObjectsOfType<SLZ.AmmoPickup>()
                                  .Select(test => test.gameObject)
                                  .ToList());

    public static CollectibleType[] ALL = {
      GACHA_CAPSULE,
      LIGHT_AMMO_CRATE,
    };

    public static CollectibleType[] EnabledTypes = {};

    public string Name;
    public Func<List<GameObject>> FindAll;
    public MelonPreferences_Entry<bool> Pref;
    public CollectibleType(string name, Func<List<GameObject>> findAll) {
      Name = name;
      FindAll = findAll;
      Pref = _typesPrefCategory.CreateEntry(name.ToLower().Replace(' ', '_'),
                                            true, name);
    }
  }
}
}
