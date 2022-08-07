﻿using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StressLevelZero.Utilities;
using Valve.VR;

namespace SpeedrunTools {
public static class BuildInfo {
  public const string Name = "SpeedrunTools";
  public const string Author = "jakzo";
  public const string Company = null;
  public const string Version = AppVersion.Value;
  public const string DownloadLink =
      "https://boneworks.thunderstore.io/package/jakzo/SpeedrunTools/";

  public const string Developer = "Stress Level Zero";
  public const string GameName = "BONEWORKS";
}

public class Mod : MelonMod {
  private static readonly Feature[] features = {
    new Features.Speedrun(), new Features.RemoveBossClawRng(),
    new Features.Teleport(), new Features.Blindfold(),
    new Features.Gripless(),
    // new Features.Replay(),
    // new Features.ControlTesting(),
    // new Features.Fps(),
    // new Features.Tas(),
    // new Features.FixPhysicsRate(),
  };

  private static List<Feature> enabledFeatures = new List<Feature>();

  private static Dictionary<Feature, Pref<bool>> featureEnabledPrefs =
      new Dictionary<Feature, Pref<bool>>();

  private static Hotkeys s_hotkeys = new Hotkeys();

  public static bool IsRunActive = false;
  public static GameState GameState = new GameState();

  private static IEnumerable<Hotkey> GetHotkeys(Feature feature) {
    foreach (var field in feature.GetType().GetFields()) {
      var type = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
      if (type == typeof(Hotkey)) {
        var hotkey = field.GetValue(feature) as Hotkey;
        yield return hotkey;
      }
    }
  }

  private static void EnableFeature(Feature feature) {
    if (enabledFeatures.Contains(feature))
      return;
    MelonLogger.Msg($"Enabling feature: {feature.GetType().Name}");
    enabledFeatures.Add(feature);
    feature.IsEnabled = true;
    foreach (var hotkey in GetHotkeys(feature))
      s_hotkeys.AddHotkey(feature, hotkey);
    feature.OnEnabled();
  }

  private static void DisableFeature(Feature feature) {
    if (!enabledFeatures.Contains(feature))
      return;
    MelonLogger.Msg($"Disabling feature: {feature.GetType().Name}");
    enabledFeatures.Remove(feature);
    feature.IsEnabled = false;
    foreach (var hotkey in GetHotkeys(feature))
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

  public override void OnApplicationStart() {
    Directory.CreateDirectory(Utils.DIR);

    Utils.s_prefCategory = MelonPreferences.CreateCategory(Utils.PREF_CATEGORY);
    Utils.PrefDebug.Create();
    foreach (var feature in features) {
      var name = feature.GetType().Name;
      var enabledPref = new Pref<bool>() { Id = $"enableFeature{name}",
                                           Name = $"Enable feature: {name}",
                                           DefaultValue = true };
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

    Utils.LogDebug("OnApplicationStart");
    OnFeatureCallback(feature => feature.OnApplicationStart());
  }

  public override void OnSceneWasLoaded(int buildIndex, string sceneName) {
    Utils.LogDebug("OnSceneWasLoaded");
    foreach (var feature in features) {
      if (featureEnabledPrefs[feature].Read()) {
        EnableFeature(feature);
      } else {
        DisableFeature(feature);
      }
    }
    OnFeatureCallback(feature =>
                          feature.OnSceneWasLoaded(buildIndex, sceneName));
  }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    Utils.LogDebug("OnSceneWasInitialized");
    s_hotkeys.Init();
    OnFeatureCallback(feature =>
                          feature.OnSceneWasInitialized(buildIndex, sceneName));
  }

  public override void OnUpdate() {
    s_hotkeys.OnUpdate();
    OnFeatureCallback(feature => feature.OnUpdate());
  }

  public override void OnFixedUpdate() {
    OnFeatureCallback(feature => feature.OnFixedUpdate());
  }

  [HarmonyPatch(typeof(BoneworksSceneManager),
                nameof(BoneworksSceneManager.LoadScene),
                new System.Type[] { typeof(string) })]
  class BoneworksSceneManager_LoadScene_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(string sceneName) {
      Utils.LogDebug($"LoadScene: {sceneName}");
      GameState.nextSceneIdx = Utils.SCENE_INDEXES_BY_NAME[sceneName];
    }
  }

  [HarmonyPatch(typeof(CVRCompositor), nameof(CVRCompositor.FadeGrid))]
  class CVRCompositor_FadeGrid_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(float fSeconds, bool bFadeIn) {
      if (bFadeIn) {
        GameState.prevSceneIdx = GameState.currentSceneIdx;
        GameState.currentSceneIdx = null;
        OnFeatureCallback(
            feature => feature.OnLoadingScreen(GameState.nextSceneIdx ?? 0,
                                               GameState.currentSceneIdx ?? 0));
      } else {
        GameState.currentSceneIdx = GameState.nextSceneIdx;
        GameState.nextSceneIdx = null;
        OnFeatureCallback(
            feature => feature.OnLevelStart(GameState.currentSceneIdx ?? 0));
      }
    }
  }
}
}