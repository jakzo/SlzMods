using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SLZ.Marrow.SceneStreaming;
using SLZ.Marrow.Warehouse;
using UnityEngine.SceneManagement;

namespace SpeedrunTools {
public static class BuildInfo {
  public const string Name = "SpeedrunTools";
  public const string Author = "jakzo";
  public const string Company = null;
  public const string Version = AppVersion.Value;
  public const string DownloadLink =
      "https://bonelab.thunderstore.io/package/jakzo/SpeedrunTools/";

  public const string Developer = "Stress Level Zero";
  public const string GameName = "BONELAB";
}

public class Mod : MelonMod {
  private static readonly Feature[] features = {
    new Features.SplitsTimer(),

    // Dev features
  };

  public static Mod Instance;
  public Mod() { Instance = this; }

  private static List<Feature> enabledFeatures = new List<Feature>();

  private static Dictionary<Feature, Pref<bool>> featureEnabledPrefs =
      new Dictionary<Feature, Pref<bool>>();

  private static Hotkeys s_hotkeys = new Hotkeys();

  public static bool IsRunActive = false;
  public static GameState GameState = new GameState();
  private Scene? _activeLoadingScene;

  private static void EnableFeature(Feature feature) {
    if (enabledFeatures.Contains(feature))
      return;
    MelonLogger.Msg($"Enabling feature: {feature.GetType().Name}");
    enabledFeatures.Add(feature);
    feature.IsEnabled = true;
    foreach (var hotkey in feature.Hotkeys)
      s_hotkeys.AddHotkey(feature, hotkey);
    feature.OnEnabled();
  }

  private static void DisableFeature(Feature feature) {
    if (!enabledFeatures.Contains(feature))
      return;
    MelonLogger.Msg($"Disabling feature: {feature.GetType().Name}");
    enabledFeatures.Remove(feature);
    feature.IsEnabled = false;
    foreach (var hotkey in feature.Hotkeys)
      s_hotkeys.RemoveHotkey(hotkey);
    feature.OnDisabled();
  }

  private static void OnFeatureCallback(Action<Feature> action) {
    foreach (var feature in enabledFeatures) {
      if (IsRunActive && !feature.IsAllowedInRuns)
        continue;
      try {
        action(feature);
      } catch (Exception ex) {
        MelonLogger.Error(ex);
      }
    }
  }

  public override void OnInitializeMelon() {
    Utils.s_prefCategory = MelonPreferences.CreateCategory(Utils.PREF_CATEGORY);
    Utils.PrefDebug.Create();
    foreach (var feature in features) {
      var name = feature.GetType().Name;
      var devName = feature.IsDev ? "Dev" : "";
      var devText = feature.IsDev ? "dev " : "";
      var enabledPref = new Pref<bool>() {
        Id = $"enable{devName}Feature{name}",
        Name = $"Enable {devText}feature: {name}",
        DefaultValue = feature.IsEnabledByDefault && !feature.IsDev,
      };
      enabledPref.Create();
      featureEnabledPrefs[feature] = enabledPref;

      foreach (var field in feature.GetType().GetFields()) {
        var type =
            Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(Pref<>)) {
          var pref = field.GetValue(feature) as IPref;
          pref.Create();
        }
      }

      if (enabledPref.Read())
        EnableFeature(feature);
    }

    SceneManager.activeSceneChanged +=
        new System.Action<Scene, Scene>(OnActiveSceneChanged);

    Utils.LogDebug("OnInitialize");
    OnFeatureCallback(feature => feature.OnInitialize());
  }

  private void DoLevelStart() {
    GameState.currentLevel = GameState.nextLevel;
    GameState.nextLevel = null;
    GameState.rigManager = Utilities.Bonelab.GetRigManager();

    foreach (var feature in features) {
      if (featureEnabledPrefs[feature].Read()) {
        EnableFeature(feature);
      } else {
        DisableFeature(feature);
      }
    }

    s_hotkeys.Init();

    OnFeatureCallback(feature => feature.OnLevelStart(GameState.currentLevel));
  }

  public override void OnUpdate() {
    if (_activeLoadingScene.HasValue && !_activeLoadingScene.Value.isLoaded) {
      _activeLoadingScene = null;

      if (GameState.currentLevel == null && GameState.nextLevel != null)
        DoLevelStart();
    }

    s_hotkeys.OnUpdate();
    OnFeatureCallback(feature => feature.OnUpdate());
  }

  public override void OnFixedUpdate() {
    OnFeatureCallback(feature => feature.OnFixedUpdate());
  }

  private void OnActiveSceneChanged(Scene prevScene, Scene nextScene) {
    if (nextScene.name ==
        SceneStreamer._session.LoadLevel?.MainScene.AssetGUID) {
      Utils.LogDebug("Load screen detected");
      if (_activeLoadingScene == null)
        _activeLoadingScene = nextScene;

      GameState.prevLevel = GameState.currentLevel;
      GameState.currentLevel = null;
      OnFeatureCallback(feature => feature.OnLoadingScreen(
                            GameState.prevLevel, GameState.nextLevel));
    }
  }

  [HarmonyPatch(typeof(SceneStreamer), nameof(SceneStreamer.Load),
                new System.Type[] { typeof(LevelCrateReference),
                                    typeof(LevelCrateReference) })]
  class SceneStreamer_Load_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(LevelCrateReference level) {
      Utils.LogDebug($"Load: {level.Crate.Title}");
      GameState.nextLevel = level.Crate;
    }
  }
}
}
