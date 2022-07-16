using MelonLoader;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace SpeedrunTools
{
  class FeatureSpeedrun : Feature
  {
    private static HashSet<string> ALLOWED_MOD_IDS = new HashSet<string>();
    private static string MENU_TEXT_NAME = "SpeedrunTools_MenuText";

    private static MelonMod s_thisMod;
    private static int s_currentSceneIdx;
    private static bool s_isActive = false;

    private enum RunIllegitimacyReason
    {
      DISALLOWED_MODS,
    }

    private static Dictionary<RunIllegitimacyReason, string> ComputeRunLegitimacy()
    {
      var illegitimacyReasons = new Dictionary<RunIllegitimacyReason, string>();

      var disallowedMods = MelonHandler.Mods.Where(mod => mod != s_thisMod && !ALLOWED_MOD_IDS.Contains(mod.ID));
      if (disallowedMods.Count() > 0)
      {
        var disallowedModNames = disallowedMods.Select(mod => mod.Info.Name);
        illegitimacyReasons[RunIllegitimacyReason.DISALLOWED_MODS] =
          $"Disallowed mods are active: {string.Join(", ", disallowedModNames)}";
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
        var rectTransform = menuText.AddComponent<RectTransform>();
        rectTransform.rect.Set(-1, -2, 2, 2);
        var tmp = menuText.AddComponent<TMPro.TextMeshPro>();
        tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
        tmp.fontSize = 1.8f;
        tmp.rectTransform.sizeDelta = new Vector2(2, 2);
        tmp.rectTransform.position = new Vector3(2.65f, 1.8f, 9.6f);
      }

      menuText.GetComponent<TMPro.TextMeshPro>().SetText(text);
    }

    private static string GetMenuText() =>
      $@"{(s_isActive ? "✅" : "❌")} Speedrun mode {(s_isActive ? "enabled" : "disabled")}
» Practice features are {(s_isActive ? "disabled" : "enabled")}
» You are{(s_isActive ? "" : " not")} allowed to submit runs to leaderboard";

    public FeatureSpeedrun(MelonMod thisMod)
    {
      s_thisMod = thisMod;
      isAllowedInLegitRuns = true;
    }

    public readonly Hotkey HotkeyToggle = new Hotkey()
    {
      Predicate = (cl, cr) =>
        s_currentSceneIdx == Utils.SCENE_MENU_IDX && (
          cl.GetBButton() && cr.GetBButton() ||
          Input.GetKeyDown(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.S)
        ),
      Handler = () =>
      {
        if (s_isActive)
        {
          s_isActive = false;
          UpdateMainMenuText(null);
          MelonLogger.Msg("Speedrun mode disabled");
        } else
        {
          var illegitimacyReasons = ComputeRunLegitimacy();
          if (illegitimacyReasons.Count == 0)
          {
            UpdateMainMenuText(null);
            MelonLogger.Msg("Speedrun mode enabled");
            s_isActive = true;
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
      // TODO: Add text to loading screen to indicate run legitimacy (include mod version)
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      s_currentSceneIdx = buildIndex;

      if (s_currentSceneIdx == Utils.SCENE_MENU_IDX) UpdateMainMenuText(null);
    }
  }
}
