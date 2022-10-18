using MelonLoader;
using System;
using System.Collections.Generic;
using HarmonyLib;
using SLZ.Marrow.SceneStreaming;
using SLZ.Marrow.Warehouse;

namespace Sst {
public class Mod : MelonMod {
  private static readonly Feature[] features = {
    new Features.SplitsTimer(),
  };

  public static Mod Instance;
  public Mod() { Instance = this; }

  private static List<Feature> enabledFeatures = new List<Feature>();

  private static Dictionary<Feature, Pref<bool>> featureEnabledPrefs =
      new Dictionary<Feature, Pref<bool>>();

  private static Hotkeys s_hotkeys = new Hotkeys();

  public static bool IsRunActive = false;
  public static GameState GameState = new GameState();
  private LoadingScene _activeLoadingScene;

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

    Utils.LogDebug("OnInitialize");
    OnFeatureCallback(feature => feature.OnInitialize());
  }

  public override void OnUpdate() {
    if (_activeLoadingScene != null &&
        !_activeLoadingScene.gameObject.scene.isLoaded) {
      Utils.LogDebug("loading scene unloaded");
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

  [HarmonyPatch(typeof(LoadingScene), nameof(LoadingScene.Start))]
  class LoadingScene_Start_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(LoadingScene __instance) {
      Utils.LogDebug("LoadingScene.Start()");
      Instance._activeLoadingScene = __instance;
      GameState.prevLevel = GameState.currentLevel;
      GameState.currentLevel = null;
      OnFeatureCallback(feature => feature.OnLoadingScreen(
                            GameState.prevLevel, GameState.nextLevel));
    }
  }
}
}
