using MelonLoader;
using UnityEngine;
using StressLevelZero.Utilities;
using HarmonyLib;
using System.Linq;
using SpeedrunTools.Speedruns;

namespace SpeedrunTools.Features {
class Speedrun : Feature {
  private const string MENU_TEXT_NAME = "SpeedrunTools_MenuText";

  public static Speedrun Instance;

  private bool _didReset = false;
  private bool _resetSaveOnNewGame = false;
  private Data_Player _playerPrefsToRestoreOnLoad;
  private Speedruns.Overlay _overlay = new Speedruns.Overlay();
  private Blindfold _blindfold = new Blindfold();
  public Speedruns.RunTimer RunTimer = new Speedruns.RunTimer();

  public Speedrun() {
    if (Instance != null)
      throw new System.Exception("Only one instance of Speedrun is allowed");
    Instance = this;
    IsAllowedInRuns = true;

    // Toggle default speedrun mode (normal)
    Hotkeys.Add(new Hotkey() {
      Predicate = (cl, cr) =>
          Mod.GameState.currentSceneIdx == Utils.SCENE_MENU_IDX &&
          cl.GetAButton() && cl.GetBButton() && cr.GetAButton() &&
          cr.GetBButton(),
      Handler = () => ToggleRun(Mode.NORMAL),
    });

    foreach (var mode in new Mode[] {
               Mode.NORMAL, Mode.NEWGAME_PLUS, Mode.HUNDRED_PERCENT,
               Mode.BLINDFOLD, Mode.GRIPLESS, Mode.LEFT_CONTROLLER_GRIPLESS,
               Mode.RIGHT_CONTROLLER_GRIPLESS, Mode.ARMLESS
             })
      if (mode.hotkeyKey != KeyCode.None)
        Hotkeys.Add(new Hotkey() {
          Predicate = (cl, cr) =>
              Mod.GameState.currentSceneIdx == Utils.SCENE_MENU_IDX &&
              Utils.GetKeyControl() && Input.GetKey(mode.hotkeyKey),
          Handler = () => ToggleRun(mode),
        });
  }

  private void ToggleRun(Mode mode) {
    if (GameObject.FindObjectOfType<SceneLoader>() != null)
      return;

    if (Mode.CurrentMode == Mode.DISABLED) {
      var illegitimacyReasons = Speedruns.AntiCheat.ComputeRunLegitimacy();
      if (illegitimacyReasons.Count == 0) {
        Speedruns.SaveUtilities.SaveData();
        Speedruns.SaveUtilities.BackupSave();

        if (mode.OnEnable != null)
          mode.OnEnable();
        Mode.CurrentMode = mode;
        Mod.IsRunActive = true;
        if (Mode.CurrentMode.saveResourceFilename != null) {
          MelonLogger.Msg("Loading custom save");
          _playerPrefsToRestoreOnLoad = Data_Manager.Instance.data_player;
          Speedruns.SaveUtilities.RestoreSaveFileResource(
              Mode.CurrentMode.saveResourceFilename);
          Speedruns.SaveUtilities.LoadData();
          _didReset = true;
        } else if (Mode.CurrentMode.resetSaveOnEnable) {
          Speedruns.SaveUtilities.ResetSave();
          _didReset = true;
        }
        Speedruns.SaveUtilities.BlockSave = true;
        BoneworksSceneManager.ReloadScene();
        MelonLogger.Msg($"{Mode.CurrentMode.name} mode enabled");
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
    if (Mode.CurrentMode.OnDisable != null)
      Mode.CurrentMode.OnDisable();
    Mode.CurrentMode = Mode.DISABLED;
    Mod.IsRunActive = false;
    Speedruns.SaveUtilities.BlockSave = true;
    _resetSaveOnNewGame = false;
    Speedruns.SaveUtilities.RestoreSaveBackupIfExists();
    var oldData = Data_Manager.Instance.data_player;
    Speedruns.SaveUtilities.LoadData();
    Speedruns.SaveUtilities.RestorePlayerPrefs(oldData);
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
    Speedruns.SaveUtilities.BlockSave = false;

    if (Mode.CurrentMode.resetSaveOnMainMenu &&
        buildIndex == Utils.SCENE_MENU_IDX)
      _resetSaveOnNewGame = true;

    if (Mode.CurrentMode.OnSceneWasLoaded != null)
      Mode.CurrentMode.OnSceneWasLoaded(buildIndex);
  }

  public override void OnUpdate() {
    if (Mode.CurrentMode.OnUpdate != null)
      Mode.CurrentMode.OnUpdate();
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
            ColorText(Mode.CurrentMode == Mode.DISABLED
                          ? "Speedrun mode disabled"
                          : $"{Mode.CurrentMode.name} mode enabled",
                      Mode.CurrentMode),
            $"» You are{(Mode.CurrentMode == Mode.DISABLED ? " not" : "")} allowed to submit runs to leaderboard",
            $"» Practice features are {(Mode.CurrentMode == Mode.DISABLED ? "enabled" : "disabled")}",
            $"» Press A + B on both controllers at once (or CTRL + S) to toggle speedrun mode",
            Mode.CurrentMode == Mode.DISABLED
                ? "» Press CTRL + N for Newgame+ runs or CTRL + H for 100% runs"
                : null,
            _didReset ? Mode.CurrentMode == Mode.NEWGAME_PLUS
                            ? "» Completed save was loaded"
                            : "» Save state was reset"
                      : null,
          }
              .Where(line => line != null));
      UpdateMainMenuText(text);
    }
    _didReset = false;

    if (Mode.CurrentMode.OnSceneWasInitialized != null)
      Mode.CurrentMode.OnSceneWasInitialized(buildIndex, sceneName);
  }

  public override void OnLoadingScreen(int nextSceneIdx, int prevSceneIdx) {
    if (Mode.CurrentMode == Mode.DISABLED)
      RunTimer.Stop();
    else if (RunTimer.IsActive)
      RunTimer.Pause();
    else if (nextSceneIdx != Utils.SCENE_MENU_IDX)
      RunTimer.Reset(true);

    var duration = RunTimer.CalculateDuration();
    _overlay.Show(string.Join(
        "\n",
        new string[] {
          ColorText(Mode.CurrentMode == Mode.DISABLED
                        ? "Speedrun mode disabled"
                        : $"{Mode.CurrentMode.name} mode enabled",
                    Mode.CurrentMode),
          $"v{BuildInfo.Version}",
          duration?.ToString(
              $"{(duration.Value.Seconds >= 60 * 60 ? "h\\:m" : "")}m\\:ss\\.ff"),
        }
            .Where(line => line != null)));
  }

  public override void OnLevelStart(int sceneIdx) {
    // Hide overlay a little after level load to make splicing harder
    System.Threading.Tasks.Task.Delay(new System.TimeSpan(0, 0, 3))
        .ContinueWith(o => HideOverlayIfNotLoading());

    if (Mode.CurrentMode != Mode.DISABLED) {
      var isMainMenu =
          BoneworksSceneManager.currentSceneIndex == Utils.SCENE_MENU_IDX;
      if (Mode.CurrentMode.resetTimerOnMainMenu && isMainMenu) {
        RunTimer.Stop();
      } else {
        // No starting timer on main menu (only unpause in the case of 100%)
        if (RunTimer.IsActive || isMainMenu)
          RunTimer.Unpause();
        else
          RunTimer.Reset();
      }
    }
  }

  private void HideOverlayIfNotLoading() {
    if (!SceneLoader.loading)
      _overlay.Hide();
    else
      // Try again later if SceneLoader still says it's loading so we don't get
      // stuck with text on the screen during the level
      System.Threading.Tasks.Task.Delay(new System.TimeSpan(0, 0, 1))
          .ContinueWith(o => HideOverlayIfNotLoading());
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
