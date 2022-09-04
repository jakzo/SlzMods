using UnityEngine;
using SpeedrunTools.Features;

namespace SpeedrunTools.Speedruns {
class Mode {
  public static readonly Mode DISABLED = new Mode() {
    color = new Color(0.8f, 0.1f, 0.1f),
    colorRgb = "cc1111",
  };

  public static readonly Mode NORMAL = new Mode() {
    name = "Speedrun",
    hotkeyKey = KeyCode.S,
    color = new Color(0.2f, 0.9f, 0.1f),
    colorRgb = "22ee11",
    resetSaveOnEnable = true,
    resetSaveOnMainMenu = true,
    resetTimerOnMainMenu = true,
  };

  public static readonly Mode NEWGAME_PLUS = new Mode() {
    name = "Newgame+ speedrun",
    hotkeyKey = KeyCode.N,
    color = new Color(0.9f, 0.9f, 0.1f),
    colorRgb = "eeee11",
    resetSaveOnEnable = false,
    resetSaveOnMainMenu = false,
    saveResourceFilename = "NewgamePlusSave.zip",
    resetTimerOnMainMenu = true,
  };

  public static readonly Mode HUNDRED_PERCENT = new Mode() {
    name = "100% speedrun",
    hotkeyKey = KeyCode.H,
    color = new Color(0.3f, 0.3f, 0.9f),
    colorRgb = "4444ee",
    resetSaveOnEnable = true,
    resetSaveOnMainMenu = false,
    resetTimerOnMainMenu = false,
  };

  public static readonly Mode BLINDFOLD = new Mode() {
    name = "Blindfold",
    hotkeyKey = KeyCode.B,
    color = new Color(0.4f, 0.4f, 0.4f),
    colorRgb = "666666",
    resetSaveOnEnable = true,
    resetSaveOnMainMenu = true,
    resetTimerOnMainMenu = true,
    OnSceneWasLoaded =
        sceneIdx => {
          if (sceneIdx == Utils.SCENE_MENU_IDX) {
            Blindfold.s_blindfolder.SetBlindfold(false);
          } else {
            Blindfold.s_blindfolder.SetBlindfold(true);
          }
        },
    OnUpdate = () => Blindfold.s_blindfolder.OnUpdate(),
    OnDisable = () => Blindfold.s_blindfolder.SetBlindfold(false),
  };

  public static readonly Mode GRIPLESS = new Mode() {
    name = "Gripless",
    hotkeyKey = KeyCode.G,
    color = new Color(0.4f, 0.4f, 0.4f),
    colorRgb = "666666",
    resetSaveOnEnable = true,
    resetSaveOnMainMenu = true,
    resetTimerOnMainMenu = true,
    OnEnable = () => Gripless.IsGripDisabled = true,
    OnDisable = () => Gripless.IsGripDisabled = false,
  };

  public static readonly Mode LEFT_CONTROLLER_GRIPLESS = new Mode() {
    name = "Left controller only gripless",
    hotkeyKey = KeyCode.L,
    color = new Color(0.4f, 0.4f, 0.4f),
    colorRgb = "666666",
    resetSaveOnEnable = true,
    resetSaveOnMainMenu = true,
    resetTimerOnMainMenu = true,
    OnEnable =
        () => {
          Gripless.IsGripDisabled = true;
          Armless.SetArmsEnabled(true, false, true);
        },
    OnSceneWasInitialized = Armless.OnSceneWasInitializedStatic,
    OnDisable =
        () => {
          Gripless.IsGripDisabled = false;
          Armless.SetArmsEnabled(true, true, true);
        },
  };

  public static readonly Mode RIGHT_CONTROLLER_GRIPLESS = new Mode() {
    name = "Right controller only gripless",
    hotkeyKey = KeyCode.O,
    color = new Color(0.4f, 0.4f, 0.4f),
    colorRgb = "666666",
    resetSaveOnEnable = true,
    resetSaveOnMainMenu = true,
    resetTimerOnMainMenu = true,
    OnEnable =
        () => {
          Gripless.IsGripDisabled = true;
          Armless.SetArmsEnabled(false, true, true);
        },
    OnSceneWasInitialized = Armless.OnSceneWasInitializedStatic,
    OnDisable =
        () => {
          Gripless.IsGripDisabled = false;
          Armless.SetArmsEnabled(true, true, true);
        },
  };

  public static readonly Mode ARMLESS = new Mode() {
    name = "Armless",
    hotkeyKey = KeyCode.A,
    color = new Color(0.4f, 0.4f, 0.4f),
    colorRgb = "666666",
    resetSaveOnEnable = true,
    resetSaveOnMainMenu = true,
    resetTimerOnMainMenu = true,
    OnEnable = () => Armless.SetArmsEnabled(false, false, false),
    OnSceneWasInitialized = Armless.OnSceneWasInitializedStatic,
    OnDisable = () => Armless.SetArmsEnabled(true, true, false),
  };

  public static Mode CurrentMode = Mode.DISABLED;

  public string name;
  public KeyCode hotkeyKey = KeyCode.None;
  public Color color;
  // Cannot be generated from color because the builtin Unity util doesn't
  // work in BW
  public string colorRgb;
  public bool resetSaveOnEnable;
  public bool resetSaveOnMainMenu;
  public bool resetTimerOnMainMenu;
  public string saveResourceFilename;
  public System.Action OnEnable;
  public System.Action OnDisable;
  public System.Action<int> OnSceneWasLoaded;
  public System.Action<int, string> OnSceneWasInitialized;
  public System.Action OnUpdate;
}
}
