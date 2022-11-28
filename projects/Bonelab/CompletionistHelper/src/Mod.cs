using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using MelonLoader;
using Sst.Common.Bonelab.HundredPercent;

namespace Sst.CompletionistHelper {
public class Mod : MelonMod {
  private const string HUD_TEXT_NAME = "CompletionistHud";
  private const float REFRESH_FREQUENCY = 1f;
  private const int NUM_HUD_SLOTS = 5;
  private const int NUM_DISPLAYED_ACHIEVEMENTS = 5;

  public static MelonPreferences_Category TypesPrefCategory;

  private (GameObject container, GameObject arrow,
           TextMeshPro text)[] _hudSlots;
  private TextMeshPro _completionTmp;
  private TextMeshPro _achievementTmp;
  private List<Collectible> _collectibles = new List<Collectible>();
  private Action[] _refreshActions;
  private float _refreshStart = 0f;
  private int _refreshIndex = 0;
  private Server _server;

  public Mod() {
    _refreshActions =
        CollectibleType.CacheActions
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

    Utilities.LevelHooks.OnLoad += level => { _server?.SendStateIfChanged(); };
    Utilities.LevelHooks.OnLevelStart += level => {
      if (_server == null) {
        _server = new Server(REFRESH_FREQUENCY);
      } else {
        _server.SendStateIfChanged();
      }
      ShowHud();
    };

    CollectibleType.Initialize(TypesPrefCategory);
    AchievementTracker.Initialize();
    CapsuleTracker.Initialize();
  }

  public override void OnDeinitializeMelon() {
    CapsuleTracker.Deinitialize();
    _server?.Dispose();
    _server = null;
  }

  private string GetCompletionText() {
    var progress = _server?.Progress;
    if (progress == null)
      return null;
    return string.Join("\n", new[] {
      $"Arena: {(progress.Arena * 100):N1}%",
      $"Avatar: {(progress.Avatar * 100):N1}%",
      $"Campaign: {(progress.Campaign * 100):N1}%",
      $"Experimental: {(progress.Experimental * 100):N1}%",
      $"Parkour: {(progress.Parkour * 100):N1}%",
      $"Sandbox: {(progress.Sandbox * 100):N1}%",
      $"Tac Trial: {(progress.TacTrial * 100):N1}%",
      $"Easter Eggs: {(progress.EasterEggs * 100):N1}%",
      $"Unlocks: {(progress.Unlocks * 100):N1}%",
      $"Total: {(progress.Total * 100):N1}%",
      "",
      $"100% complete: {progress.IsComplete}",
      $"Beat game: {progress.HasBeatGame}",
      $"Has body log: {progress.HasBodyLog}",
      $"Achievements: {AchievementTracker.Unlocked.Count} / {AchievementTracker.AllAchievements.Count}",
      $"Unlocks: {CapsuleTracker.Unlocked.Count} / {CapsuleTracker.NumTotalUnlocks}",
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
    SortCollectibles();
    DisplayCollectibles();

    _completionTmp.SetText(GetCompletionText());
    _achievementTmp.SetText(GetAchievementText());
  }

  private void SortCollectibles() {
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
  }

  private void DisplayCollectibles() {
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
  }

  private void ShowHud() {
    Dbg.Log("ShowHud()");
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
    _completionTmp = completionTextGo.AddComponent<TextMeshPro>();
    _completionTmp.alignment = TextAlignmentOptions.BottomLeft;
    _completionTmp.fontSize = 0.2f;
    _completionTmp.rectTransform.sizeDelta = new Vector2(0.3f, 0.3f);
    _completionTmp.transform.localPosition = new Vector3(0.2f, 0, 0);
    _completionTmp.transform.localRotation = Quaternion.identity;

    var achievementTextGo = new GameObject("CompletionistHudAchievementText");
    achievementTextGo.transform.SetParent(hud.transform);
    _achievementTmp = achievementTextGo.AddComponent<TextMeshPro>();
    _achievementTmp.alignment = TextAlignmentOptions.BottomLeft;
    _achievementTmp.fontSize = 0.2f;
    _achievementTmp.rectTransform.sizeDelta = new Vector2(0.3f, 0.3f);
    _achievementTmp.transform.localPosition = new Vector3(-0.1f, 0, 0);
    _achievementTmp.transform.localRotation = Quaternion.identity;
  }
}
}
