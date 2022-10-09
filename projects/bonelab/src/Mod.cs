using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Valve.VR;
using SLZ.Rig;

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
    new Features.Splits(),

    // Dev features
  };

  private static List<Feature> enabledFeatures = new List<Feature>();

  private static Dictionary<Feature, Pref<bool>> featureEnabledPrefs =
      new Dictionary<Feature, Pref<bool>>();

  private static Hotkeys s_hotkeys = new Hotkeys();

  public static bool IsRunActive = false;
  public static GameState GameState = new GameState();

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

  public override void OnApplicationStart() {
    Directory.CreateDirectory(Utils.DIR);

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
    GameState.rigManager = Utilities.Boneworks.GetRigManager();
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

  [HarmonyPatch(typeof(Controller), nameof(Controller.CacheInputs))]
  class Controller_CacheInputs_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(Controller __instance) {
      Features.Gripless.OnCacheInputs(__instance);
      Features.Armless.OnCacheInputs(__instance);
    }
  }

  [HarmonyPatch(typeof(Controller), nameof(Controller.ProcessFingers))]
  class Controller_ProcessFingers_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(Controller __instance) {
      Features.Gripless.OnProcessFingers(__instance);
      Features.Armless.OnProcessFingers(__instance);
    }
  }

  [HarmonyPatch(typeof(Controller), nameof(Controller.SolveGrip))]
  class Controller_SolveGrip_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(Controller __instance) {
      Features.Gripless.OnSolveGrip(__instance);
      Features.Armless.OnSolveGrip(__instance);
    }
  }
}
}
