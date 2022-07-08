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
      new FeatureTeleport(),
      new FeatureResetSave(),
      new FeatureBlindfold(),
      // new FeatureReplay(),
    };

    private static Hotkeys s_hotkeys;

    public override void OnApplicationStart()
    {
      Directory.CreateDirectory(Utils.DIR);

      Utils.s_prefCategory = MelonPreferences.CreateCategory(Utils.PREF_CATEGORY);
      Utils.PrefDebug.Create();
      List<Hotkey> hotkeys = new List<Hotkey>();
      foreach (var feature in features)
      {
        foreach (var field in feature.GetType().GetFields())
        {
          var type = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
          if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Pref<>))
          {
            var pref = field.GetValue(feature) as IPref;
            pref.Create();
          } else if (type == typeof(Hotkey))
          {
            var hotkey = field.GetValue(feature) as Hotkey;
            hotkeys.Add(hotkey);
          }
        }
      }
      Utils.LogDebug("Preferences loaded");

      s_hotkeys = new Hotkeys(hotkeys.ToArray());
      Utils.LogDebug("Hotkeys loaded");

      HarmonyInstance.PatchAll();
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      Utils.LogDebug("OnSceneWasInitialized: hotkeys");
      s_hotkeys.Init();

      foreach (var feature in features)
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
      foreach (var feature in features)
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
