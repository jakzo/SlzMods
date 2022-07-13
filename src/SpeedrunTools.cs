using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SpeedrunTools
{
  public static class BuildInfo
  {
    public const string Name = "SpeedrunTools";
    public const string Author = "jakzo";
    public const string Company = null;
    public const string Version = AppVersion.Value;
    public const string DownloadLink = "https://boneworks.thunderstore.io/package/jakzo/SpeedrunTools/";

    public const string Developer = "Stress Level Zero";
    public const string GameName = "BONEWORKS";
  }

  public class SpeedrunTools : MelonMod
  {
    private static readonly Feature[] features = {
      new FeatureRemoveBossClawRng(),
      // new FeatureTeleport(),
      // new FeatureResetSave(),
      new FeatureBlindfold(),
      // new FeatureReplay(),
      new FeatureControlTesting(),
    };

    private static List<Feature> enabledFeatures = new List<Feature>();

    private static Dictionary<Feature, Pref<bool>> featureEnabledPrefs =
      new Dictionary<Feature, Pref<bool>>();

    private static Hotkeys s_hotkeys = new Hotkeys();

    private static void EnableFeature(Feature feature)
    {
      if (enabledFeatures.Contains(feature)) return;
      enabledFeatures.Add(feature);
      foreach (var field in feature.GetType().GetFields())
      {
        var type = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
        if (type == typeof(Hotkey))
        {
          var hotkey = field.GetValue(feature) as Hotkey;
          s_hotkeys.AddHotkey(hotkey);
        }
      }
    }

    private static void DisableFeature(Feature feature)
    {
      if (!enabledFeatures.Contains(feature)) return;
      enabledFeatures.Remove(feature);
      foreach (var field in feature.GetType().GetFields())
      {
        var type = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
        if (type == typeof(Hotkey))
        {
          var hotkey = field.GetValue(feature) as Hotkey;
          s_hotkeys.RemoveHotkey(hotkey);
        }
      }
    }

    public override void OnApplicationStart()
    {
      Directory.CreateDirectory(Utils.DIR);

      Utils.s_prefCategory = MelonPreferences.CreateCategory(Utils.PREF_CATEGORY);
      Utils.PrefDebug.Create();
      foreach (var feature in features)
      {
        var name = feature.GetType().Name;
        var enabledPref = new Pref<bool>()
        {
          Id = $"enableFeature{name}",
          Name = $"Enable {name}",
          DefaultValue = true
        };
        enabledPref.Create();
        featureEnabledPrefs[feature] = enabledPref;

        foreach (var field in feature.GetType().GetFields())
        {
          var type = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
          if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Pref<>))
          {
            var pref = field.GetValue(feature) as IPref;
            pref.Create();
          }
        }

        if (enabledPref.Read()) EnableFeature(feature);
      }
      Utils.LogDebug("Feature preferences and hotkeys loaded");

      HarmonyInstance.PatchAll();
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      Utils.LogDebug("OnSceneWasInitialized: hotkeys");
      s_hotkeys.Init();

      foreach (var feature in features)
      {
        if (featureEnabledPrefs[feature].Read())
        {
          EnableFeature(feature);
        } else
        {
          DisableFeature(feature);
        }
      }

      foreach (var feature in enabledFeatures)
      {
        try
        {
          Utils.LogDebug($"OnSceneWasInitialized: {feature}");
          feature.OnSceneWasInitialized(buildIndex, sceneName);
        } catch (Exception ex)
        {
          MelonLogger.Error(ex);
        }
      }

      Utils.LogDebug("Initialization complete");
    }

    public override void OnUpdate()
    {
      s_hotkeys.OnUpdate();
      foreach (var feature in enabledFeatures)
      {
        try
        {
          feature.OnUpdate();
        } catch (Exception ex)
        {
          MelonLogger.Error(ex);
        }
      }
    }
  }
}
