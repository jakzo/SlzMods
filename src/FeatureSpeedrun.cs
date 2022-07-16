using MelonLoader;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace SpeedrunTools
{
  class FeatureSpeedrun : Feature
  {
    private static HashSet<string> ALLOWED_MOD_IDS = new HashSet<string>();
    private static HashSet<string> ALLOWED_PLUGIN_IDS = new HashSet<string>();
    private static string MENU_TEXT_NAME = "SpeedrunTools_MenuText";
    private static string LOADING_TEXT_NAME = "SpeedrunTools_LoadingText";

    private static int s_currentSceneIdx;

    private enum RunIllegitimacyReason
    {
      DISALLOWED_MODS,
      DISALLOWED_PLUGINS,
    }

    private static Dictionary<RunIllegitimacyReason, string> ComputeRunLegitimacy()
    {
      var illegitimacyReasons = new Dictionary<RunIllegitimacyReason, string>();

      var disallowedMods = MelonHandler.Mods.Where(mod => mod is SpeedrunTools && !ALLOWED_MOD_IDS.Contains(mod.ID));
      if (disallowedMods.Count() > 0)
      {
        var disallowedModNames = disallowedMods.Select(mod => mod.Info.Name);
        illegitimacyReasons[RunIllegitimacyReason.DISALLOWED_MODS] =
          $"Disallowed mods are active: {string.Join(", ", disallowedModNames)}";
      }

      var disallowedPlugins = MelonHandler.Plugins.Where(plugin => !ALLOWED_PLUGIN_IDS.Contains(plugin.ID));
      if (disallowedPlugins.Count() > 0)
      {
        var disallowedPluginNames = disallowedPlugins.Select(mod => mod.Info.Name);
        illegitimacyReasons[RunIllegitimacyReason.DISALLOWED_PLUGINS] =
          $"Disallowed plugins are active: {string.Join(", ", disallowedPluginNames)}";
      }

      return illegitimacyReasons;
    }

    private static void UpdateMainMenuText(string customText)
    {
      var text = customText ?? GetMenuText();

      var menuText = GameObject.Find(MENU_TEXT_NAME);

      if (menuText == null)
      {
        menuText = new GameObject(MENU_TEXT_NAME);
        var tmp = menuText.AddComponent<TMPro.TextMeshPro>();
        tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
        tmp.fontSize = 1.8f;
        tmp.rectTransform.sizeDelta = new Vector2(2, 2);
        tmp.rectTransform.position = new Vector3(2.65f, 1.8f, 9.6f);
      }

      menuText.GetComponent<TMPro.TextMeshPro>().SetText(text);
    }

    private static string GetMenuText() =>
      $@"{(SpeedrunTools.s_isLegitRunActive ? "✅" : "❌")} Speedrun mode {(SpeedrunTools.s_isLegitRunActive ? "enabled" : "disabled")}
» Practice features are {(SpeedrunTools.s_isLegitRunActive ? "disabled" : "enabled")}
» You are{(SpeedrunTools.s_isLegitRunActive ? "" : " not")} allowed to submit runs to leaderboard";

    public FeatureSpeedrun()
    {
      isAllowedInLegitRuns = true;
    }

    private float? _relativeStartTime;
    private float? _loadingStartTime;

    public readonly Hotkey HotkeyToggle = new Hotkey()
    {
      Predicate = (cl, cr) =>
        s_currentSceneIdx == Utils.SCENE_MENU_IDX && (
          cl.GetBButton() && cr.GetBButton() ||
          Input.GetKeyDown(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.S)
        ),
      Handler = () =>
      {
        if (SpeedrunTools.s_isLegitRunActive)
        {
          SpeedrunTools.s_isLegitRunActive = false;
          UpdateMainMenuText(null);
          MelonLogger.Msg("Speedrun mode disabled");
        } else
        {
          var illegitimacyReasons = ComputeRunLegitimacy();
          if (illegitimacyReasons.Count == 0)
          {
            SpeedrunTools.s_isLegitRunActive = true;
            UpdateMainMenuText(null);
            MelonLogger.Msg("Speedrun mode enabled");
          } else
          {
            var reasonMessages = string.Join("", illegitimacyReasons.Select(reason => $"\n» {reason.Value}"));
            var message = $"Could not enable speedrun mode because:{reasonMessages}";
            UpdateMainMenuText($"❌ {message}");
            MelonLogger.Msg(message);
          }
        }
      }
    };

    public override void OnLoadingScreen()
    {
      _loadingStartTime = Time.time;

      var loadingText = GameObject.Find(LOADING_TEXT_NAME);

      if (loadingText == null)
      {
        loadingText = new GameObject(LOADING_TEXT_NAME);
        var tmp = loadingText.AddComponent<TMPro.TextMeshPro>();
        tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
        tmp.fontSize = 1.8f;
        tmp.rectTransform.sizeDelta = new Vector2(2, 2);
        tmp.rectTransform.position = new Vector3(1.5f, 1.8f, 1.5f);
      }

      var startTime = _relativeStartTime.HasValue ? _relativeStartTime.Value : Time.time;
      loadingText.GetComponent<TMPro.TextMeshPro>().SetText(
        SpeedrunTools.s_isLegitRunActive
          ? $@"✅ Speedrun mode enabled
{BuildInfo.Name} v{BuildInfo.Version}
Time: {System.TimeSpan.FromMilliseconds(Time.time - startTime):h\\:mm\\:ss\\.ff}"
          : "Speedrun mode disabled"
      );
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      if (_loadingStartTime.HasValue && _relativeStartTime.HasValue)
      {
        _relativeStartTime += Time.time - _loadingStartTime.Value;
        _loadingStartTime = null;
      }

      var previousSceneIdx = s_currentSceneIdx;
      s_currentSceneIdx = buildIndex;

      if (previousSceneIdx == Utils.SCENE_MENU_IDX)
        _relativeStartTime = Time.time;


      if (s_currentSceneIdx == Utils.SCENE_MENU_IDX) UpdateMainMenuText(null);
    }
  }
}
