using MelonLoader;
using UnityEngine;
using Valve.VR;
using StressLevelZero.Utilities;
using StressLevelZero.Data;
using HarmonyLib;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;

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
    };
    public static readonly Mode HUNDRED_PERCENT = new Mode() {
      name = "100% speedrun",      color = new Color(0.3f, 0.3f, 0.9f),
      colorRgb = "4444ee",         resetSaveOnEnable = true,
      resetSaveOnMainMenu = false, resetTimerOnMainMenu = true,
    };

    public string name;
    public Color color;
    public string colorRgb;
    public bool resetSaveOnEnable;
    public bool resetSaveOnMainMenu;
    public bool resetTimerOnMainMenu;
    public string saveResourceFilename;
  }

  private static HashSet<string> ALLOWED_MODS = new HashSet<string>();
  private static HashSet<string> ALLOWED_PLUGINS = new HashSet<string>() {
    "Backwards Compatibility Plugin",
  };
  private const string MENU_TEXT_NAME = "SpeedrunTools_MenuText";
  private const string LOADING_TEXT_NAME = "SpeedrunTools_LoadingText";
  private static bool SHOW_BONEWORKS_LOGO_OVERLAY = true;
  private static readonly string OVERLAY_KEY =
      $"{SteamVR_Overlay.key}.SpeedrunTools_RunStatus";
  private const int NUM_SLOTS = 5;

  private static ulong s_overlayHandle;
  private static float? s_relativeStartTime;
  private static float? s_loadingStartTime;
  private static Texture2D s_overlayTexture;
  private static bool s_didReset = false;
  private static bool s_isSceneInitialized = false;
  private static bool s_blockSaveUntilSceneLoad = false;
  private static Data_Player s_playerPrefsToRestoreOnLoad;
  private static Mode s_mode = Mode.DISABLED;

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

  private static void ToggleRun(Mode mode) {
    if (!s_isSceneInitialized || s_blockSaveUntilSceneLoad)
      return;

    if (s_mode == Mode.DISABLED) {
      var illegitimacyReasons = ComputeRunLegitimacy();
      if (illegitimacyReasons.Count == 0) {
        SaveData();
        BackupSave();

        s_mode = mode;
        SpeedrunTools.s_isRunActive = true;
        if (s_mode.saveResourceFilename != null) {
          MelonLogger.Msg("Loading newgame+ save");
          s_playerPrefsToRestoreOnLoad = Data_Manager.Instance.data_player;
          RestoreSaveFileResource(s_mode.saveResourceFilename);
          LoadData();
          s_didReset = true;
        } else if (s_mode.resetSaveOnEnable) {
          ResetSave();
          s_didReset = true;
        }
        s_blockSaveUntilSceneLoad = true;
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
      s_mode = Mode.DISABLED;
      SpeedrunTools.s_isRunActive = false;
      s_blockSaveUntilSceneLoad = true;
      RestoreSaveBackupIfExists();
      LoadData();
      BoneworksSceneManager.ReloadScene();
      MelonLogger.Msg("Speedrun mode disabled");
    }
  }

  private enum RunIllegitimacyReason {
    DISALLOWED_MODS,
    DISALLOWED_PLUGINS,
  }

  private static Dictionary<RunIllegitimacyReason, string>
  ComputeRunLegitimacy() {
    var illegitimacyReasons = new Dictionary<RunIllegitimacyReason, string>();

    var disallowedMods =
        MelonHandler.Mods.Where(mod => !(mod is SpeedrunTools) &&
                                       !ALLOWED_MODS.Contains(mod.Info.Name));
    if (disallowedMods.Count() > 0) {
      var disallowedModNames = disallowedMods.Select(mod => mod.Info.Name);
      illegitimacyReasons[RunIllegitimacyReason.DISALLOWED_MODS] =
          $"Disallowed mods are active: {string.Join(", ", disallowedModNames)}";
    }

    var disallowedPlugins = MelonHandler.Plugins.Where(
        plugin => !ALLOWED_PLUGINS.Contains(plugin.Info.Name));
    if (disallowedPlugins.Count() > 0) {
      var disallowedPluginNames =
          disallowedPlugins.Select(mod => mod.Info.Name);
      illegitimacyReasons[RunIllegitimacyReason.DISALLOWED_PLUGINS] =
          $"Disallowed plugins are active: {string.Join(", ", disallowedPluginNames)}";
    }

    return illegitimacyReasons;
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

  // TODO: Make this work
  private static Texture TextToTexture(string text) {
    if (SHOW_BONEWORKS_LOGO_OVERLAY) {
      if (s_overlayTexture == null) {
        s_overlayTexture =
            Resources.FindObjectsOfTypeAll<Texture2D>()
                .Where(tex => tex.name == "sprite_title_boneworks")
                .First();
      }
      return s_overlayTexture;
    }

    // Build our text as a renderable mesh
    var textGameObject = new GameObject("SpeedrunTools_LoadingText") {
      // hideFlags = HideFlags.HideAndDontSave,
    };
    var tmp = textGameObject.AddComponent<TMPro.TextMeshPro>();
    tmp.material = new Material(Shader.Find("TextMeshPro/Distance Field"));
    tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
    tmp.fontSize = 1.8f;
    tmp.rectTransform.sizeDelta = new Vector2(2, 2);
    tmp.text = text;

    // Save existing GL/RenderTexture state
    GL.PushMatrix();
    var prevActiveRt = RenderTexture.active;

    // Point the projection matrix at our text
    GL.LoadIdentity();
    GL.LoadProjectionMatrix(
        Matrix4x4.Ortho(-tmp.rectTransform.sizeDelta.x / 2.0f,
                        tmp.rectTransform.sizeDelta.x / 2.0f,
                        -tmp.rectTransform.sizeDelta.x / 2.0f,
                        tmp.rectTransform.sizeDelta.y / 2.0f, -10, 100));

    // Render the viewed text to our texture
    var texture =
        RenderTexture.GetTemporary(2048, 2048, 24, RenderTextureFormat.ARGB32);
    Graphics.SetRenderTarget(texture);
    GL.Clear(false, true, Color.clear);
    tmp.renderer.material.SetPass(0);
    Graphics.DrawMeshNow(tmp.mesh, Matrix4x4.identity);

    // Restore state
    GL.PopMatrix();
    RenderTexture.active = prevActiveRt;
    // GameObject.Destroy(textGameObject);

    // TESTING: Put the texture on the carpet in front of the speedrun text in
    // main menu to see if it worked
    var carpet = GameObject.Find("carpet_2");
    if (carpet) {
      var renderer = carpet.GetComponent<MeshRenderer>();
      renderer.material.mainTexture = texture;
    } else {
      Utils.LogDebug("Carpet not found");
    }

    return texture;
  }

  private static void ShowOverlay(string text) {
    var texture = TextToTexture(text);

    if (OpenVR.Overlay == null)
      return;

    if (s_overlayHandle == OpenVR.k_ulOverlayHandleInvalid) {
      var createError = OpenVR.Overlay.CreateOverlay(
          OVERLAY_KEY, "SpeedrunTools Run Status", ref s_overlayHandle);
      if (createError != EVROverlayError.None) {
        var findError =
            OpenVR.Overlay.FindOverlay(OVERLAY_KEY, ref s_overlayHandle);
        if (findError != EVROverlayError.None)
          throw new System.Exception(
              $"Could not find speedrun overlay: {findError}");
      }
    }
    var showError = OpenVR.Overlay.ShowOverlay(s_overlayHandle);
    if (showError == EVROverlayError.InvalidHandle ||
        showError == EVROverlayError.UnknownOverlay) {
      var findError =
          OpenVR.Overlay.FindOverlay(OVERLAY_KEY, ref s_overlayHandle);
      if (findError != EVROverlayError.None)
        throw new System.Exception(
            $"Could not find speedrun overlay: {findError}");
    }
    var ovrTexture = new Texture_t() {
      handle = texture.GetNativeTexturePtr(),
      eType = SteamVR.instance.textureType,
      eColorSpace = EColorSpace.Auto,
    };
    OpenVR.Overlay.SetOverlayTexture(s_overlayHandle, ref ovrTexture);
    var color = s_mode.color;
    if (color != null)
      OpenVR.Overlay.SetOverlayColor(s_overlayHandle, color.r, color.g,
                                     color.b);
    OpenVR.Overlay.SetOverlayAlpha(s_overlayHandle, 1);
    OpenVR.Overlay.SetOverlayWidthInMeters(s_overlayHandle, 0.6f);
    OpenVR.Overlay.SetOverlayAutoCurveDistanceRangeInMeters(s_overlayHandle, 1,
                                                            2);
    var vecMouseScale = new HmdVector2_t() {
      v0 = 1,
      v1 = 1,
    };
    var uvOffset = new Vector4(0, 0, 1, 1);
    var textureBounds = new VRTextureBounds_t() {
      uMin = (0 + uvOffset.x) * uvOffset.z,
      vMin = (1 + uvOffset.y) * uvOffset.w,
      uMax = (1 + uvOffset.x) * uvOffset.z,
      vMax = (0 + uvOffset.y) * uvOffset.w,
    };
    OpenVR.Overlay.SetOverlayTextureBounds(s_overlayHandle, ref textureBounds);
    OpenVR.Overlay.SetOverlayMouseScale(s_overlayHandle, ref vecMouseScale);
    OpenVR.Overlay.SetOverlayInputMethod(s_overlayHandle,
                                         VROverlayInputMethod.None);
    var transform = new SteamVR_Utils.RigidTransform() {
      pos = new Vector3(0, -0.4f, 1),
      rot = Quaternion.identity,
      scl = new Vector3(1, 1, 1),
    };
    var ovrMatrix = transform.ToHmdMatrix34();
    OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(
        s_overlayHandle, OpenVR.k_unTrackedDeviceIndex_Hmd, ref ovrMatrix);
    OpenVR.Overlay.SetHighQualityOverlay(s_overlayHandle);
    OpenVR.Overlay.SetOverlayFlag(s_overlayHandle, VROverlayFlags.Curved,
                                  false);
    OpenVR.Overlay.SetOverlayFlag(s_overlayHandle, VROverlayFlags.RGSS4X, true);
  }

  private static void HideOverlay() {
    if (OpenVR.Overlay == null ||
        s_overlayHandle == OpenVR.k_ulOverlayHandleInvalid)
      return;
    OpenVR.Overlay.HideOverlay(s_overlayHandle);
  }

  [HarmonyPatch(typeof(BoneworksSceneManager),
                nameof(BoneworksSceneManager.LoadScene),
                new System.Type[] { typeof(string) })]
  class BoneworksSceneManager_LoadScene_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(string sceneName) {
      s_loadingStartTime = Time.time;
      s_isSceneInitialized = false;
      var isLoadingMainMenu = sceneName == Utils.SCENE_MENU_NAME ||
                              sceneName == Utils.SCENE_MENU_NAME_ALT;
      if (s_mode.resetSaveOnMainMenu && isLoadingMainMenu && !s_didReset) {
        ResetSave();
        s_didReset = true;
      }
    }
  }

  public override void OnSceneWasLoaded(int buildIndex, string sceneName) {
    s_blockSaveUntilSceneLoad = false;
  }

  public override void OnSceneWasInitialized(int buildIndex, string sceneName) {
    s_isSceneInitialized = true;

    if (s_playerPrefsToRestoreOnLoad != null) {
      RestorePlayerPrefs(s_playerPrefsToRestoreOnLoad);
      s_playerPrefsToRestoreOnLoad = null;
    }

    if (s_relativeStartTime.HasValue) {
      if (s_loadingStartTime.HasValue) {
        s_relativeStartTime += Time.time - s_loadingStartTime.Value;
        s_loadingStartTime = null;
      }
    } else if (buildIndex != Utils.SCENE_MENU_IDX) {
      s_relativeStartTime = Time.time;
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

  private static void ResetSave() {
    MelonLogger.Msg("Resetting save");
    var oldData = Data_Manager.Instance.data_player;
    Data_Manager.Instance.DATA_DEFAULT_ALL();
    RestorePlayerPrefs(oldData);
  }

  private static void RestorePlayerPrefs(Data_Player oldData) {
    var newData = Data_Manager.Instance.data_player;
    newData.additionalLighting = oldData.additionalLighting;
    newData.aliasing = oldData.aliasing;
    newData.ambientOcclusion = oldData.ambientOcclusion;
    newData.audio_GlobalVolume = oldData.audio_GlobalVolume;
    newData.audio_Music = oldData.audio_Music;
    newData.audio_SFX = oldData.audio_SFX;
    newData.beltRightSide = oldData.beltRightSide;
    newData.bloom = oldData.bloom;
    newData.fisheye = oldData.fisheye;
    newData.fisheyeLocation = oldData.fisheyeLocation;
    newData.isAdaptiveOn = oldData.isAdaptiveOn;
    newData.isInverted = oldData.isInverted;
    newData.isRightHanded = oldData.isRightHanded;
    newData.joySensitivityNew = oldData.joySensitivityNew;
    newData.language = oldData.language;
    newData.loco_Curve = oldData.loco_Curve;
    newData.loco_DegreesPerSnap = oldData.loco_DegreesPerSnap;
    newData.loco_Direction = oldData.loco_Direction;
    newData.loco_SnapDegreesPerFrame = oldData.loco_SnapDegreesPerFrame;
    newData.mod_Haptic = oldData.mod_Haptic;
    newData.motionBlur = oldData.motionBlur;
    newData.mouseSensitivityNew = oldData.mouseSensitivityNew;
    newData.offset_Floor = oldData.offset_Floor;
    newData.offset_Sitting = oldData.offset_Sitting;
    newData.physicsUpdateRate = oldData.physicsUpdateRate;
    newData.player_Height = oldData.player_Height;
    newData.player_Name = oldData.player_Name;
    newData.playMode = oldData.playMode;
    newData.profile_Name = oldData.profile_Name;
    newData.quality = oldData.quality;
    newData.resX = oldData.resX;
    newData.resY = oldData.resY;
    newData.shadowQuality = oldData.shadowQuality;
    newData.SnapEnabled = oldData.SnapEnabled;
    newData.standing = oldData.standing;
    newData.subtitlesOn = oldData.subtitlesOn;
    newData.TextureResolution = oldData.TextureResolution;
    newData.VirtualCrouching = oldData.VirtualCrouching;

    SaveData();
    LoadData();
  }

  [HarmonyPatch(typeof(LoadingScreenPackage),
                nameof(LoadingScreenPackage.StartAlpha))]
  class LoadingScreenPackage_StartAlpha_Patch {
    [HarmonyPrefix()]
    internal static void Prefix() {
      var durationSecs = (s_loadingStartTime - s_relativeStartTime) ?? 0;
      var enabledStatusText = s_mode == Mode.DISABLED ? "disabled" : "enabled";
      var text = $@"{s_mode.name} {ColorText(enabledStatusText, s_mode)}
v{BuildInfo.Version}
{System.TimeSpan.FromSeconds(durationSecs):h\:mm\:ss\.ff}";
      ShowOverlay(text);
    }
  }

  [HarmonyPatch(typeof(LoadingScreenPackage),
                nameof(LoadingScreenPackage.AlphaOverlays))]
  class LoadingScreenPackage_AlphaOverlays_Patch {
    [HarmonyPrefix()]
    internal static void Prefix() => HideOverlay();

    // Silence errors
    [HarmonyFinalizer()]
    internal static void Finalizer() {}
  }

  public override void OnApplicationStart() { RestoreSaveBackupIfExists(); }

  private static string GetBackupPath() {
    var dirName = Path.GetFileName(Application.persistentDataPath);
    return Path.Combine(Application.persistentDataPath, "..",
                        $"{dirName}.speedrun_backup");
  }

  private static void BackupSave() {
    // Backup existing save file to:
    // %UserProfile%\AppData\LocalLow\Stress Level
    // Zero\BONEWORKS.speedrun_backup
    var backupPath = GetBackupPath();
    if (Directory.Exists(backupPath))
      throw new System.Exception($"Backup already exists at: {backupPath}");
    MelonLogger.Msg($"Backing up save to: {backupPath}");
    Directory.CreateDirectory(backupPath);
    foreach (var filePath in Directory.EnumerateFiles(
                 Application.persistentDataPath)) {
      if (filePath.EndsWith("output_log.txt"))
        continue;
      File.Copy(filePath, Path.Combine(backupPath, Path.GetFileName(filePath)));
    }
  }

  private static void RestoreSaveFileResource(string saveResourceName) {
    Utils.LogDebug($"Loading save: {saveResourceName}");
    DeleteSave();
    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
    string resourcePath = assembly.GetManifestResourceNames().Single(
        str => str.EndsWith(saveResourceName));
    using (var stream = assembly.GetManifestResourceStream(resourcePath)) {
      using (var archive = new ZipArchive(stream, ZipArchiveMode.Read)) {
        foreach (var entry in archive.Entries) {
          var entryStream = entry.Open();
          using (var fileStream = File.Create(Path.Combine(
                     Application.persistentDataPath, entry.FullName))) {
            entryStream.CopyTo(fileStream);
          }
        }
      }
    }
  }

  private static void SaveData() {
    for (int slot = 0; slot < NUM_SLOTS; slot++)
      Data_Manager.Instance.DATA_SAVE(slot);
    AmmoData.Save();
    LevelData.Save();
    ReclaimerData.Save();
    TimeTrialData.Save();
  }

  private static void LoadData() {
    for (int slot = 0; slot < NUM_SLOTS; slot++)
      Data_Manager.Instance.DATA_LOAD(slot);
    Data_Manager.Instance.DATA_PROFILE_SET(0);
    AmmoData.Load();
    LevelData.Load();
    ReclaimerData.Load();
    TimeTrialData.Load();
  }

  private static void DeleteSave() {
    Utils.LogDebug("Deleting save");
    foreach (var filePath in Directory.EnumerateFiles(
                 Application.persistentDataPath)) {
      if (filePath.EndsWith("output_log.txt"))
        continue;
      File.Delete(filePath);
    }
  }

  private static void RestoreSaveBackupIfExists() {
    try {
      var backupPath = GetBackupPath();
      if (Directory.Exists(backupPath)) {
        MelonLogger.Msg($"Restoring save backup from: {backupPath}");
        DeleteSave();
        foreach (var filePath in Directory.EnumerateFiles(backupPath)) {
          File.Copy(filePath, Path.Combine(Application.persistentDataPath,
                                           Path.GetFileName(filePath)));
        }
        Directory.Delete(backupPath, true);
      }
    } catch (System.Exception err) {
      MelonLogger.Error(
          "WARNING: A backup of your save file exists because speedrun mode was " +
          "enabled but restoring it failed with this error:");
      MelonLogger.Error(err);
    }
  }

  // Save disabling (used to prevent overwriting of newly copied-in save files
  // when reloading a level)
  [HarmonyPatch(typeof(Data_Manager), nameof(Data_Manager.DATA_SAVE))]
  class Data_Manager_DATA_SAVE_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix() => !s_blockSaveUntilSceneLoad;
  }
  [HarmonyPatch(typeof(ReclaimerData), nameof(ReclaimerData.Save))]
  class ReclaimerData_Save_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix() => !s_blockSaveUntilSceneLoad;
  }
  [HarmonyPatch(typeof(AmmoData), nameof(AmmoData.Save))]
  class AmmoData_Save_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix() => !s_blockSaveUntilSceneLoad;
  }
  [HarmonyPatch(typeof(LevelData), nameof(LevelData.Save))]
  class LevelData_Save_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix() => !s_blockSaveUntilSceneLoad;
  }
  [HarmonyPatch(typeof(TimeTrialData), nameof(TimeTrialData.Save))]
  class TimeTrialData_Save_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix() => !s_blockSaveUntilSceneLoad;
  }
}
}
