using MelonLoader;
using System;
using System.Collections.Generic;
using SLZ.Marrow.Warehouse;

namespace Sst.SpeedrunPractice {
public class Mod : MelonMod {
  private static readonly Feature[] features = {
    new Features.Teleport(),
    new Features.ScriptedMovement(),
  };

  private static List<Feature> enabledFeatures = new List<Feature>();

  private static Dictionary<Feature, Pref<bool>> featureEnabledPrefs =
      new Dictionary<Feature, Pref<bool>>();

  private static Hotkeys s_hotkeys = new Hotkeys();

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
      try {
        action(feature);
      } catch (Exception ex) {
        MelonLogger.Error(ex);
      }
    }
  }

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);

    Utils.s_prefCategory = MelonPreferences.CreateCategory(Utils.PREF_CATEGORY);
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

    Utilities.LevelHooks.OnLevelStart += OnLevelStart;

    Dbg.Log("OnApplicationStart");
    OnFeatureCallback(feature => feature.OnApplicationStart());
  }

  private void OnLevelStart(LevelCrate level) {
    foreach (var feature in features) {
      if (featureEnabledPrefs[feature].Read()) {
        EnableFeature(feature);
      } else {
        DisableFeature(feature);
      }
    }
    Utils.State.rigManager = Utilities.Bonelab.GetRigManager();
    s_hotkeys.Init();
    Dbg.Log("OnLevelStart");
    OnFeatureCallback(feature => feature.OnLevelStart(level));
  }

  public override void OnUpdate() {
    s_hotkeys.OnUpdate();
    OnFeatureCallback(feature => feature.OnUpdate());
  }

  public override void OnFixedUpdate() {
    OnFeatureCallback(feature => feature.OnFixedUpdate());
  }
}
}
