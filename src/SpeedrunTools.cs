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
    private static readonly Plugin[] plugins = {
      // new PluginRemoveBossClawRng(),
      // new PluginTeleport(),
      // new PluginResetSave(),
      new PluginBlindfold(),
    };

    private static Hotkeys s_hotkeys;

    public override void OnApplicationStart()
    {
      Directory.CreateDirectory(Utils.DIR);

      Utils.s_prefCategory = MelonPreferences.CreateCategory(Utils.PREF_CATEGORY);
      Utils.PrefDebug.Create();
      List<Hotkey> hotkeys = new List<Hotkey>();
      foreach (var plugin in plugins)
      {
        foreach (var field in plugin.GetType().GetFields())
        {
          var type = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
          if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Pref<>))
          {
            var pref = field.GetValue(plugin) as IPref;
            pref.Create();
          } else if (type == typeof(Hotkey))
          {
            var hotkey = field.GetValue(plugin) as Hotkey;
            hotkeys.Add(hotkey);
          }
        }
      }
      Utils.LogDebug("Preferences loaded");

      s_hotkeys = new Hotkeys(hotkeys.ToArray());
      Utils.LogDebug("Hotkeys loaded");
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      Utils.LogDebug("OnSceneWasInitialized: hotkeys");
      s_hotkeys.Init();

      foreach (var plugin in plugins)
      {
        try
        {
          Utils.LogDebug($"OnSceneWasInitialized: {plugin}");
          plugin.OnSceneWasInitialized(buildIndex, sceneName);
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
      foreach (var plugin in plugins)
      {
        try
        {
          plugin.OnUpdate();
        } catch (Exception ex)
        {
          MelonLogger.Error(ex);
        }
      }
    }
  }
}
