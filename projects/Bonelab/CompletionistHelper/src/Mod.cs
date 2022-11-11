using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using MelonLoader;
using SLZ.Marrow.Warehouse;
using SLZ.Data;

namespace Sst {
public class Mod : MelonMod {
  private const string HUD_TEXT_NAME = "CompletionistHud";
  private const float REFRESH_FREQUENCY = 15;
  private const int NUM_HUD_SLOTS = 5;
  private const int NUM_DISPLAYED_ACHIEVEMENTS = 5;
  private const float MAX_PROGRESS = 0.95f;

  public static MelonPreferences_Category TypesPrefCategory;

  private (GameObject container, GameObject arrow,
           TextMeshPro text)[] _hudSlots;
  private TextMeshPro _completionText;
  private TextMeshPro _achievementText;
  private List<Collectible> _collectibles = new List<Collectible>();
  private HashSet<string> _unlockedAchievements = new HashSet<string>();
  private int _totalNumUnlocks = 0;
  private Progress _progress = new Progress();
  private Action[] _refreshActions;
  private float _refreshStart = 0;
  private int _refreshIndex = 0;

  public Mod() {
    _refreshActions =
        new Action[] {
          () => { _progress.Refresh(); },
          () => {
            var filter = new CrateFilters.UnlockableAndNotRedactedCrateFilter()
                             .Cast<ICrateFilter<Crate>>();
            _totalNumUnlocks =
                AssetWarehouseExtensions
                    .Filter(AssetWarehouse.Instance.GetCrates(), filter)
                    .Count;
          },
          // () => {
          //   Il2CppSystem.Collections.Generic
          //       .Dictionary<string, Il2CppSystem.Object> levelState = null;
          //   DataManager.ActiveSave?.Progression.LevelState.TryGetValue(
          //       Utilities.LevelHooks.CurrentLevel.name, out levelState);
          // },
        }
            .Concat(CollectibleType.CacheActions)
            .Concat(CollectibleType.ALL.Select<CollectibleType, Action>(
                collectibleType => () => {
                  _collectibles = _collectibles
                                      .Where(collectible => collectible.Type !=
                                                            collectibleType)
                                      .ToList();

                  foreach (var component in collectibleType.FindAll()) {
                    var collectible = new Collectible() {
                      Type = collectibleType,
                      GameObject = component.gameObject,
                    };
                    if (!CollectibleType.ShouldShow(collectible.GameObject))
                      continue;

                    // if (levelState != null) {
                    //   var saveable =
                    //       collectible.GameObject.GetComponent<SLZ.Bonelab.Saveable>();
                    //   if (saveable != null) {
                    //     Il2CppSystem.Object status = null;
                    //     levelState.TryGetValue(saveable.UniqueId, out
                    //     status); if (status != null)
                    //       continue;
                    //   }
                    // }

                    _collectibles.Add(collectible);
                  }
                }))
            .ToArray();
  }

  public void RollingRefresh() {
    if (Time.time >= _refreshStart + REFRESH_FREQUENCY &&
        _refreshIndex >= _refreshActions.Length) {
      _refreshStart = Time.time;
      _refreshIndex = 0;
    }
    var end = Math.Min(1, (Time.time - _refreshStart) / REFRESH_FREQUENCY) *
              _refreshActions.Length;
    while (_refreshIndex < end)
      _refreshActions[_refreshIndex++]();
  }

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);

    TypesPrefCategory =
        MelonPreferences.CreateCategory("TypesToShow", "Types to show");

    Utilities.LevelHooks.OnLevelStart += level => ShowHud();

    CollectibleType.Initialize(TypesPrefCategory);
    AchievementTracker.Initialize();
  }

  private string GetCompletionText() {
    var activeSave = DataManager.ActiveSave;
    if (activeSave == null)
      return "";

    var numUnlocked = activeSave.Unlocks.Unlocks.Count;
    var isGameBeat = activeSave.Progression.BeatGame;
    var hasBodyLog = activeSave.Progression.HasBodyLog;
    var isComplete = isGameBeat && hasBodyLog &&
                     numUnlocked >= _totalNumUnlocks &&
                     AchievementTracker.Unlocked.Count >=
                         AchievementTracker.AllAchievements.Count &&
                     _progress.IsComplete;

    return string.Join("\n", new[] {
      $"Arena: {(_progress.Arena * 100):N1}%",
      $"Avatar: {(_progress.Avatar * 100):N1}%",
      $"Campaign: {(_progress.Campaign * 100):N1}%",
      $"Experimental: {(_progress.Experimental * 100):N1}%",
      $"Parkour: {(_progress.Parkour * 100):N1}%",
      $"Sandbox: {(_progress.Sandbox * 100):N1}%",
      $"TacTrial: {(_progress.TacTrial * 100):N1}%",
      $"Unlocks: {(_progress.Unlocks * 100):N1}%",
      $"Total: {(_progress.Total * 100):N1}% / {(Progress.MAX_PROGRESS * 100):N1}%",
      "",
      $"100% complete: {isComplete}",
      $"Beat game: {isGameBeat}",
      $"Has body log: {hasBodyLog}",
      $"Achievements: {AchievementTracker.Unlocked.Count} / {AchievementTracker.AllAchievements.Count}",
      $"Unlocks: {numUnlocked} / {_totalNumUnlocks}",
    });
  }

  private string GetAchievementText() {
    var lockedAchievements = string.Join(
        "",
        AchievementTracker.AllAchievements
            .Where(entry => !AchievementTracker.Unlocked.Contains(entry.Key))
            .Take(NUM_DISPLAYED_ACHIEVEMENTS)
            .Reverse()
            .Select(entry => $"\n{entry.Value}"));
    var unlockedAchievements = string.Join(
        "", AchievementTracker.Unlocked.Reverse()
                .Take(NUM_DISPLAYED_ACHIEVEMENTS)
                .Reverse()
                .Select(id => {
                  string name;
                  AchievementTracker.AllAchievements.TryGetValue(id, out name);
                  return $"\n{name ?? "UNKNOWN"}";
                }));
    return $"Locked achievements:{lockedAchievements}\n\nUnlocked achievements:{unlockedAchievements}";
  }

  public override void OnUpdate() {
    if (Utilities.LevelHooks.IsLoading || _hudSlots == null)
      return;

    RollingRefresh();

    foreach (var collectible in _collectibles)
      collectible.Distance =
          CollectibleType.ShouldShow(collectible.GameObject)
              ? Vector3.Distance(Utilities.LevelHooks.RigManager.ControllerRig
                                     .leftController.transform.position,
                                 collectible.GameObject.transform.position)
              : float.PositiveInfinity;
    _collectibles.Sort((x, y) => {
      var delta = x.Distance - y.Distance;
      return delta > 0 ? 1 : delta < 0 ? -1 : 0;
    });

    var displayedCollectibles =
        _collectibles
            .Where(collectible => collectible.Distance < float.PositiveInfinity)
            .Take(NUM_HUD_SLOTS)
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
      text.SetText($"{collectible.Distance:N1}m {collectible.Type.Name}");
    }

    _completionText.SetText(GetCompletionText());
    _achievementText.SetText(GetAchievementText());
  }

  private void ShowHud() {
    _refreshStart = 0;
    _refreshIndex = 0;
    var hud = new GameObject("CompletionistHud");
    Utilities.Bonelab.DockToWrist(hud, Utilities.LevelHooks.RigManager);
    _hudSlots =
        Enumerable.Range(0, NUM_HUD_SLOTS)
            .Select(i => {
              var hudSlot = new GameObject($"CompletionistHudSlot{i}");
              hudSlot.active = false;
              hudSlot.transform.SetParent(hud.transform);
              hudSlot.transform.localPosition = new Vector3(0, i * 0.04f, 0);
              hudSlot.transform.localRotation = Quaternion.identity;

              var arrowContainer =
                  new GameObject($"CompletionistHudArrowContainer{i}");
              arrowContainer.transform.SetParent(hudSlot.transform);
              arrowContainer.transform.localPosition =
                  new Vector3(0.33f, -0.13f, 0);
              arrowContainer.transform.localRotation = Quaternion.identity;
              var arrowGo = new GameObject($"CompletionistHudArrow{i}");
              arrowGo.transform.SetParent(arrowContainer.transform);
              var arrow = arrowGo.AddComponent<TextMeshPro>();
              arrow.alignment = TextAlignmentOptions.Center;
              arrow.fontSize = 0.2f;
              arrow.rectTransform.sizeDelta = new Vector2(0, 0);
              arrow.rectTransform.localPosition = Vector3.zero;
              arrow.rectTransform.localRotation = Quaternion.Euler(0, 270, 0);
              arrow.SetText("»");

              var textGo = new GameObject($"CompletionistHudText{i}");
              textGo.transform.SetParent(hudSlot.transform);
              var text = textGo.AddComponent<TextMeshPro>();
              text.alignment = TextAlignmentOptions.BottomLeft;
              text.fontSize = 0.2f;
              text.rectTransform.sizeDelta = new Vector2(0.3f, 0.08f);
              text.rectTransform.localPosition = new Vector3(0.5f, -0.1f, 0);
              text.rectTransform.localRotation = Quaternion.identity;

              return (hudSlot, arrowContainer, text);
            })
            .ToArray();

    var completionTextGo = new GameObject("CompletionistHudCompletionText");
    completionTextGo.transform.SetParent(hud.transform);
    _completionText = completionTextGo.AddComponent<TextMeshPro>();
    _completionText.alignment = TextAlignmentOptions.BottomLeft;
    _completionText.fontSize = 0.2f;
    _completionText.rectTransform.sizeDelta = new Vector2(0.3f, 0.3f);
    _completionText.transform.localPosition = new Vector3(0.2f, 0, 0);
    _completionText.transform.localRotation = Quaternion.identity;

    var achievementTextGo = new GameObject("CompletionistHudAchievementText");
    achievementTextGo.transform.SetParent(hud.transform);
    _achievementText = achievementTextGo.AddComponent<TextMeshPro>();
    _achievementText.alignment = TextAlignmentOptions.BottomLeft;
    _achievementText.fontSize = 0.2f;
    _achievementText.rectTransform.sizeDelta = new Vector2(0.3f, 0.3f);
    _achievementText.transform.localPosition = new Vector3(-0.1f, 0, 0);
    _achievementText.transform.localRotation = Quaternion.identity;
  }
}
}
