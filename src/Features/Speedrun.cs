using MelonLoader;
using UnityEngine;
using Valve.VR;
using StressLevelZero.Utilities;
using HarmonyLib;
using System.Linq;

namespace SpeedrunTools.Features {
class Speedrun : Feature {
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

  private static Speedrun Instance;

  private bool _didReset = false;
  private bool _resetSaveOnNewGame = false;
  private Data_Player _playerPrefsToRestoreOnLoad;
  private Mode _mode = Mode.DISABLED;
  private Speedruns.Overlay _overlay = new Speedruns.Overlay();
  private Blindfold _blindfold = new Blindfold();
  private Speedruns.RunTimer _runTimer = new Speedruns.RunTimer();

  public readonly Hotkey HotkeyToggleNormal;
  public readonly Hotkey HotkeyToggleNewgamePlus;
  public readonly Hotkey HotkeyToggleHundredPercent;
  public readonly Hotkey HotkeyToggleBlindfold;

  public Speedrun() {
    if (Instance != null)
      throw new System.Exception("Only one instance of Speedrun is allowed");
    Instance = this;
    IsAllowedInRuns = true;
    HotkeyToggleNormal = new Hotkey() {
      Predicate = (cl, cr) =>
          Mod.s_gameState.currentSceneIdx == Utils.SCENE_MENU_IDX &&
          (cl.GetAButton() && cl.GetBButton() && cr.GetAButton() &&
               cr.GetBButton() ||
           Utils.GetKeyControl() && Input.GetKey(KeyCode.S)),
      Handler = () => ToggleRun(Mode.NORMAL),
    };
    HotkeyToggleNewgamePlus = new Hotkey() {
      Predicate = (cl, cr) =>
          Mod.s_gameState.currentSceneIdx == Utils.SCENE_MENU_IDX &&
          Utils.GetKeyControl() && Input.GetKey(KeyCode.N),
      Handler = () => ToggleRun(Mode.NEWGAME_PLUS),
    };
    HotkeyToggleHundredPercent = new Hotkey() {
      Predicate = (cl, cr) =>
          Mod.s_gameState.currentSceneIdx == Utils.SCENE_MENU_IDX &&
          Utils.GetKeyControl() && Input.GetKey(KeyCode.H),
      Handler = () => ToggleRun(Mode.HUNDRED_PERCENT),
    };
    HotkeyToggleBlindfold = new Hotkey() {
      Predicate = (cl, cr) =>
          Mod.s_gameState.currentSceneIdx == Utils.SCENE_MENU_IDX &&
          Utils.GetKeyControl() && Input.GetKey(KeyCode.B),
      Handler = () => ToggleRun(Mode.BLINDFOLD),
    };
  }

  private void ToggleRun(Mode mode) {
    if (GameObject.FindObjectOfType<SceneLoader>() != null)
      return;

    if (_mode == Mode.DISABLED) {
      var illegitimacyReasons = Speedruns.AntiCheat.ComputeRunLegitimacy();
      if (illegitimacyReasons.Count == 0) {
        Speedruns.SaveUtilities.SaveData();
        Speedruns.SaveUtilities.BackupSave();

        _mode = mode;
        Mod.s_isRunActive = true;
        if (_mode.saveResourceFilename != null) {
          MelonLogger.Msg("Loading newgame+ save");
          _playerPrefsToRestoreOnLoad = Data_Manager.Instance.data_player;
          Speedruns.SaveUtilities.RestoreSaveFileResource(
              _mode.saveResourceFilename);
          Speedruns.SaveUtilities.LoadData();
          _didReset = true;
        } else if (_mode.resetSaveOnEnable) {
          Speedruns.SaveUtilities.ResetSave();
          _didReset = true;
        }
        Speedruns.SaveUtilities.s_BlockSave = true;
        BoneworksSceneManager.ReloadScene();
        MelonLogger.Msg($"{_mode.name} mode enabled");
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

  private void DisableSpeedrunMode() {
    _mode = Mode.DISABLED;
    Mod.s_isRunActive = false;
    Speedruns.SaveUtilities.s_BlockSave = true;
    _resetSaveOnNewGame = false;
    Speedruns.SaveUtilities.RestoreSaveBackupIfExists();
    var oldData = Data_Manager.Instance.data_player;
    Speedruns.SaveUtilities.LoadData();
    Speedruns.SaveUtilities.RestorePlayerPrefs(oldData);
    Blindfold.s_blindfolder.SetBlindfold(false);
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
    Speedruns.SaveUtilities.RestoreSaveBackupIfExists();
  }

  public override void OnSceneWasLoaded(int buildIndex, string sceneName) {
    Speedruns.SaveUtilities.s_BlockSave = false;

    if (_mode.resetSaveOnMainMenu && buildIndex == Utils.SCENE_MENU_IDX)
      _resetSaveOnNewGame = true;

    if (_mode.blindfoldWhileInLevel) {
      if (buildIndex == Utils.SCENE_MENU_IDX) {
        Blindfold.s_blindfolder.SetBlindfold(false);
      } else {
        Blindfold.s_blindfolder.SetBlindfold(true);
      }
    }
  }

  public override void OnUpdate() {
    if (_mode.blindfoldWhileInLevel)
      Blindfold.s_blindfolder.OnUpdate();
  }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    if (_playerPrefsToRestoreOnLoad != null) {
      Speedruns.SaveUtilities.RestorePlayerPrefs(_playerPrefsToRestoreOnLoad);
      _playerPrefsToRestoreOnLoad = null;
    }

    if (buildIndex == Utils.SCENE_MENU_IDX) {
      var text = string.Join(
          "\n",
          new string[] {
            ColorText(_mode == Mode.DISABLED ? "Speedrun mode disabled"
                                             : $"{_mode.name} mode enabled",
                      _mode),
            $"» You are{(_mode == Mode.DISABLED ? " not" : "")} allowed to submit runs to leaderboard",
            $"» Practice features are {(_mode == Mode.DISABLED ? "enabled" : "disabled")}",
            $"» Press A + B on both controllers at once (or CTRL + S) to toggle speedrun mode",
            _mode == Mode.DISABLED
                ? "» Press CTRL + N for Newgame+ runs or CTRL + H for 100% runs"
                : null,
            _didReset ? _mode == Mode.NEWGAME_PLUS
                            ? "» Completed save was loaded"
                            : "» Save state was reset"
                      : null,
          }
              .Where(line => line != null));
      UpdateMainMenuText(text);
    }
    _didReset = false;
  }

  public override void OnLoadingScreen(int nextSceneIdx, int prevSceneIdx) {
    if (_mode == Mode.DISABLED)
      _runTimer.Stop();
    else
      _runTimer.OnLevelEnd();

    _overlay.Show(string.Join(
        "\n",
        new string[] {
          ColorText(_mode == Mode.DISABLED ? "Speedrun mode disabled"
                                           : $"{_mode.name} mode enabled",
                    _mode),
          $"v{BuildInfo.Version}",
          _runTimer.Duration?.ToString(
              $"{(_runTimer.Duration.Value.Seconds >= 60 * 60 ? "h\\:m" : "")}m\\:ss\\.ff"),
        }
            .Where(line => line != null)));
  }

  public override void OnLevelStart(int sceneIdx) {
    // Hide overlay a little after level load to make splicing harder
    System.Threading.Tasks.Task.Delay(new System.TimeSpan(0, 0, 3))
        .ContinueWith(o => {
          if (!SceneLoader.loading)
            _overlay.Hide();
        });

    if (_mode != Mode.DISABLED) {
      if (BoneworksSceneManager.currentSceneIndex == Utils.SCENE_MENU_IDX &&
          (_mode.resetTimerOnMainMenu || !_runTimer.Duration.HasValue)) {
        _runTimer.Reset();
      } else {
        _runTimer.OnLevelStart();
      }
    }
  }

  [HarmonyPatch(typeof(BoneworksSceneManager),
                nameof(BoneworksSceneManager.LoadScene),
                new System.Type[] { typeof(string) })]
  class BoneworksSceneManager_LoadScene_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(string sceneName) {
      if (Instance == null)
        return;

      if (Instance._resetSaveOnNewGame) {
        Instance._resetSaveOnNewGame = false;
        if (sceneName == Utils.SCENE_INTRO_NAME)
          Speedruns.SaveUtilities.ResetSave();
        else
          Instance.DisableSpeedrunMode();
      }
    }
  }
}
}
