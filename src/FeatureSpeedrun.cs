using MelonLoader;
using UnityEngine;
using Valve.VR;
using StressLevelZero.Utilities;
using HarmonyLib;
using System.Linq;

namespace SpeedrunTools {
class FeatureSpeedrun : Feature {
  class Mode {
    public static readonly Mode DISABLED = new Mode() {
      color = new Color(0.8f, 0.1f, 0.1f),
      colorRgb = "cc1111",
    };
    public static readonly Mode NORMAL = new Mode() {
      name = "Speedrun",          color = new Color(0.2f, 0.9f, 0.1f),
      colorRgb = "22ee11",        resetSaveOnEnable = true,
      resetSaveOnMainMenu = true, resetTimerOnMainMenu = true,
    };
    public static readonly Mode NEWGAME_PLUS = new Mode() {
      name = "Newgame+ speedrun",  color = new Color(0.9f, 0.9f, 0.1f),
      colorRgb = "eeee11",         resetSaveOnEnable = false,
      resetSaveOnMainMenu = false, saveResourceFilename = "NewgamePlusSave.zip",
      resetTimerOnMainMenu = true,
    };
    public static readonly Mode HUNDRED_PERCENT = new Mode() {
      name = "100% speedrun",      color = new Color(0.3f, 0.3f, 0.9f),
      colorRgb = "4444ee",         resetSaveOnEnable = true,
      resetSaveOnMainMenu = false, resetTimerOnMainMenu = false,
    };
    public static readonly Mode BLINDFOLD = new Mode() {
      name = "Blindfold",           color = new Color(0.5f, 0.5f, 0.5f),
      colorRgb = "888888",          resetSaveOnEnable = true,
      resetSaveOnMainMenu = true,   resetTimerOnMainMenu = true,
      blindfoldWhileInLevel = true,
    };

    public string name;
    public Color color;
    // Cannot be generated from color because the builtin Unity util doesn't
    // work in BW
    public string colorRgb;
    public bool resetSaveOnEnable;
    public bool resetSaveOnMainMenu;
    public bool resetTimerOnMainMenu;
    public string saveResourceFilename;
    public bool blindfoldWhileInLevel;
  }

  private const string MENU_TEXT_NAME = "SpeedrunTools_MenuText";

  private static bool s_didReset = false;
  private static bool s_resetSaveOnNewGame = false;
  private static Data_Player s_playerPrefsToRestoreOnLoad;
  private static Mode s_mode = Mode.DISABLED;
  private static Speedrun.RunTimer s_runTimer = new Speedrun.RunTimer();
  private static Speedrun.Overlay s_overlay = new Speedrun.Overlay();
  private static FeatureBlindfold s_blindfold = new FeatureBlindfold();

  public FeatureSpeedrun() { isAllowedInRuns = true; }

  public readonly Hotkey HotkeyToggleNormal = new Hotkey() {
    Predicate = (cl, cr) =>
        BoneworksSceneManager.currentSceneIndex == Utils.SCENE_MENU_IDX &&
        (cl.GetAButton() && cl.GetBButton() && cr.GetAButton() &&
             cr.GetBButton() ||
         Utils.GetKeyControl() && Input.GetKey(KeyCode.S)),
    Handler = () => ToggleRun(Mode.NORMAL),
  };
  public readonly Hotkey HotkeyToggleNewgamePlus = new Hotkey() {
    Predicate = (cl, cr) =>
        BoneworksSceneManager.currentSceneIndex == Utils.SCENE_MENU_IDX &&
        Utils.GetKeyControl() && Input.GetKey(KeyCode.N),
    Handler = () => ToggleRun(Mode.NEWGAME_PLUS),
  };
  public readonly Hotkey HotkeyToggleHundredPercent = new Hotkey() {
    Predicate = (cl, cr) =>
        BoneworksSceneManager.currentSceneIndex == Utils.SCENE_MENU_IDX &&
        Utils.GetKeyControl() && Input.GetKey(KeyCode.H),
    Handler = () => ToggleRun(Mode.HUNDRED_PERCENT),
  };
  public readonly Hotkey HotkeyToggleBlindfold = new Hotkey() {
    Predicate = (cl, cr) =>
        BoneworksSceneManager.currentSceneIndex == Utils.SCENE_MENU_IDX &&
        Utils.GetKeyControl() && Input.GetKey(KeyCode.B),
    Handler = () => ToggleRun(Mode.BLINDFOLD),
  };

  private static void ToggleRun(Mode mode) {
    if (GameObject.FindObjectOfType<SceneLoader>() != null)
      return;

    if (s_mode == Mode.DISABLED) {
      var illegitimacyReasons = Speedrun.AntiCheat.ComputeRunLegitimacy();
      if (illegitimacyReasons.Count == 0) {
        Speedrun.SaveUtilities.SaveData();
        Speedrun.SaveUtilities.BackupSave();

        s_mode = mode;
        SpeedrunTools.s_isRunActive = true;
        if (s_mode.saveResourceFilename != null) {
          MelonLogger.Msg("Loading newgame+ save");
          s_playerPrefsToRestoreOnLoad = Data_Manager.Instance.data_player;
          Speedrun.SaveUtilities.RestoreSaveFileResource(
              s_mode.saveResourceFilename);
          Speedrun.SaveUtilities.LoadData();
          s_didReset = true;
        } else if (s_mode.resetSaveOnEnable) {
          Speedrun.SaveUtilities.ResetSave();
          s_didReset = true;
        }
        Speedrun.SaveUtilities.s_BlockSave = true;
        BoneworksSceneManager.ReloadScene();
        MelonLogger.Msg($"{s_mode.name} mode enabled");
      } else {
        var reasonMessages = string.Join(
            "", illegitimacyReasons.Select(reason => $"\n» {reason.Value}"));
        UpdateMainMenuText(
            $"{ColorText("Could not enable speedrun mode", Mode.DISABLED)} because:{reasonMessages}");
        MelonLogger.Msg(
            $"Could not enable speedrun mode because:{reasonMessages}");
      }
    } else {
      DisableSpeedrunMode();
      BoneworksSceneManager.ReloadScene();
    }
  }

  private static void DisableSpeedrunMode() {
    s_mode = Mode.DISABLED;
    SpeedrunTools.s_isRunActive = false;
    Speedrun.SaveUtilities.s_BlockSave = true;
    s_resetSaveOnNewGame = false;
    Speedrun.SaveUtilities.RestoreSaveBackupIfExists();
    var oldData = Data_Manager.Instance.data_player;
    Speedrun.SaveUtilities.LoadData();
    Speedrun.SaveUtilities.RestorePlayerPrefs(oldData);
    FeatureBlindfold.s_blindfolder.SetBlindfold(false);
    MelonLogger.Msg("Speedrun mode disabled");
  }

  private static string ColorText(string text, Mode mode) =>
      $@"<color=#{mode.colorRgb}>{text}</color>";

  private static void UpdateMainMenuText(string text) {
    var menuText = GameObject.Find(MENU_TEXT_NAME);

    if (menuText == null) {
      menuText = new GameObject(MENU_TEXT_NAME);
      var tmp = menuText.AddComponent<TMPro.TextMeshPro>();
      tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
      tmp.fontSize = 1.5f;
      tmp.rectTransform.sizeDelta = new Vector2(2, 2);
      tmp.rectTransform.position = new Vector3(2.65f, 1.8f, 9.6f);
    }

    menuText.GetComponent<TMPro.TextMeshPro>().SetText(text);
  }

  public override void OnApplicationStart() {
    Speedrun.SaveUtilities.RestoreSaveBackupIfExists();
  }

  public override void OnSceneWasLoaded(int buildIndex, string sceneName) {
    Speedrun.SaveUtilities.s_BlockSave = false;

    if (s_mode.resetSaveOnMainMenu && buildIndex == Utils.SCENE_MENU_IDX)
      s_resetSaveOnNewGame = true;

    if (s_mode.blindfoldWhileInLevel) {
      if (buildIndex == Utils.SCENE_MENU_IDX) {
        FeatureBlindfold.s_blindfolder.SetBlindfold(false);
      } else {
        FeatureBlindfold.s_blindfolder.SetBlindfold(true);
      }
    }
  }

  public override void OnUpdate() {
    if (s_mode.blindfoldWhileInLevel)
      FeatureBlindfold.s_blindfolder.OnUpdate();
  }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    if (s_playerPrefsToRestoreOnLoad != null) {
      Speedrun.SaveUtilities.RestorePlayerPrefs(s_playerPrefsToRestoreOnLoad);
      s_playerPrefsToRestoreOnLoad = null;
    }

    if (buildIndex == Utils.SCENE_MENU_IDX) {
      var text = string.Join(
          "\n",
          new string[] {
            ColorText(s_mode == Mode.DISABLED ? "Speedrun mode disabled"
                                              : $"{s_mode.name} mode enabled",
                      s_mode),
            $"» You are{(s_mode == Mode.DISABLED ? " not" : "")} allowed to submit runs to leaderboard",
            $"» Practice features are {(s_mode == Mode.DISABLED ? "enabled" : "disabled")}",
            $"» Press A + B on both controllers at once (or CTRL + S) to toggle speedrun mode",
            s_mode == Mode.DISABLED
                ? "» Press CTRL + N for Newgame+ runs or CTRL + H for 100% runs"
                : null,
            s_didReset ? s_mode == Mode.NEWGAME_PLUS
                             ? "» Completed save was loaded"
                             : "» Save state was reset"
                       : null,
          }
              .Where(line => line != null));
      UpdateMainMenuText(text);
    }
    s_didReset = false;
  }

  [HarmonyPatch(typeof(BoneworksSceneManager),
                nameof(BoneworksSceneManager.LoadScene),
                new System.Type[] { typeof(string) })]
  class BoneworksSceneManager_LoadScene_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(string sceneName) {
      if (s_resetSaveOnNewGame) {
        s_resetSaveOnNewGame = false;
        if (sceneName == Utils.SCENE_INTRO_NAME)
          Speedrun.SaveUtilities.ResetSave();
        else
          DisableSpeedrunMode();
      }

      if (s_mode == Mode.DISABLED)
        s_runTimer.Stop();
      else
        s_runTimer.OnLevelEnd();
    }
  }

  [HarmonyPatch(typeof(CVRCompositor), nameof(CVRCompositor.FadeGrid))]
  class CVRCompositor_FadeGrid_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(float fSeconds, bool bFadeIn) {
      if (bFadeIn) {
        s_overlay.Show(string.Join(
            "\n",
            new string[] {
              ColorText(s_mode == Mode.DISABLED ? "Speedrun mode disabled"
                                                : $"{s_mode.name} mode enabled",
                        s_mode),
              $"v{BuildInfo.Version}",
              s_runTimer.Duration?.ToString(
                  $"{(s_runTimer.Duration.Value.Seconds >= 60 * 60 ? "h\\:m" : "")}m\\:ss\\.ff"),
            }
                .Where(line => line != null)));
      } else {
        // Hide overlay a little after level load to make cheating harder
        System.Threading.Tasks.Task.Delay(new System.TimeSpan(0, 0, 3))
            .ContinueWith(o => {
              if (!SceneLoader.loading)
                s_overlay.Hide();
            });

        if (s_mode != Mode.DISABLED) {
          if (BoneworksSceneManager.currentSceneIndex == Utils.SCENE_MENU_IDX &&
              (s_mode.resetTimerOnMainMenu || !s_runTimer.Duration.HasValue)) {
            s_runTimer.Reset();
          } else {
            s_runTimer.OnLevelStart();
          }
        }
      }
    }
  }
}
}
