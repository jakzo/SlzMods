using MelonLoader;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace SpeedrunTools
{
  class FeatureSpeedrun : Feature
  {
    private static HashSet<string> ALLOWED_MOD_IDS = new HashSet<string>();

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

    private static void AddTextToMainScreen(string text)
    {
      // TODO
    }

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
          var message = "Speedrun mode disabled";
          AddTextToMainScreen($"❌ {message}\n- Practice features are enabled\n- You are not allowed to submit runs to leaderboard");
          MelonLogger.Msg(message);
          s_isActive = false;
        } else
        {
          var illegitimacyReasons = ComputeRunLegitimacy();
          if (illegitimacyReasons.Count == 0)
          {
            var message = "Speedrun mode enabled";
            AddTextToMainScreen($"✅ {message}\n- Practice features are disabled\n- You are allowed to submit runs to leaderboard");
            MelonLogger.Msg(message);
            s_isActive = true;
          } else
          {
            var reasonMessages = string.Join("", illegitimacyReasons.Select(reason => $"\n- {reason.Value}"));
            var message = $"Could not enable speedrun mode because:{reasonMessages}";
            AddTextToMainScreen($"❌ {message}");
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
    }
  }
}
