using MelonLoader;
using UnityEngine;
using Valve.VR;
using StressLevelZero.Utilities;
using HarmonyLib;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace SpeedrunTools
{
  class FeatureSpeedrun : Feature
  {
    private static HashSet<string> ALLOWED_MODS = new HashSet<string>();
    private static HashSet<string> ALLOWED_PLUGINS = new HashSet<string>()
    {
      "Backwards Compatibility Plugin",
    };
    private const string MENU_TEXT_NAME = "SpeedrunTools_MenuText";
    private const string LOADING_TEXT_NAME = "SpeedrunTools_LoadingText";
    private static readonly Color COLOR_GREEN = new Color(0.2f, 0.9f, 0.1f);
    private static readonly Color COLOR_RED = new Color(0.8f, 0.1f, 0.1f);
    private static readonly Color COLOR_BLUE = new Color(0.2f, 0.1f, 0.9f);
    private static bool SHOW_BONEWORKS_LOGO_OVERLAY = true;
    private static readonly string OVERLAY_KEY = $"{SteamVR_Overlay.key}.SpeedrunTools_RunStatus";

    private static int s_currentSceneIdx;
    private static ulong s_overlayHandle;
    private static float? s_relativeStartTime;
    private static float? s_loadingStartTime;
    private static Texture2D s_overlayTexture;
    private static bool s_is100PercentRun = false;
    private static bool s_didReset = false;
    private static bool s_shouldRestoreSave = false;

    public FeatureSpeedrun()
    {
      isAllowedInLegitRuns = true;
    }

    public readonly Hotkey HotkeyToggle = new Hotkey()
    {
      Predicate = (cl, cr) =>
        s_currentSceneIdx == Utils.SCENE_MENU_IDX && (
          cl.GetAButton() && cl.GetBButton() && cr.GetAButton() && cr.GetBButton() ||
          Utils.GetKeyControl() && Input.GetKey(KeyCode.S)
        ),
      Handler = () => ToggleRun(false),
    };
    public readonly Hotkey HotkeyToggle100Percent = new Hotkey()
    {
      Predicate = (cl, cr) =>
        s_currentSceneIdx == Utils.SCENE_MENU_IDX &&
          Utils.GetKeyControl() && Input.GetKey(KeyCode.H),
      Handler = () => ToggleRun(true),
    };

    private static void ToggleRun(bool is100PercentRun)
    {
      if (SpeedrunTools.s_isLegitRunActive)
      {
        SpeedrunTools.s_isLegitRunActive = s_is100PercentRun = false;
        s_shouldRestoreSave = true;
        BoneworksSceneManager.ReloadScene();
        MelonLogger.Msg("Speedrun mode disabled");
      } else
      {
        var illegitimacyReasons = ComputeRunLegitimacy();
        if (illegitimacyReasons.Count == 0)
        {
          BackupSave();
          ResetSave();
          SpeedrunTools.s_isLegitRunActive = true;
          s_is100PercentRun = is100PercentRun;
          BoneworksSceneManager.ReloadScene();
          MelonLogger.Msg($"Speedrun mode enabled{(is100PercentRun ? " (100%)" : "")}");
        } else
        {
          var reasonMessages = string.Join("", illegitimacyReasons.Select(reason => $"\n» {reason.Value}"));
          UpdateMainMenuText($"{ColorText("Could not enable speedrun mode", COLOR_RED)} because:{reasonMessages}");
          MelonLogger.Msg($"Could not enable speedrun mode because:{reasonMessages}");
        }
      }
    }

    private enum RunIllegitimacyReason
    {
      DISALLOWED_MODS,
      DISALLOWED_PLUGINS,
    }

    private static Dictionary<RunIllegitimacyReason, string> ComputeRunLegitimacy()
    {
      var illegitimacyReasons = new Dictionary<RunIllegitimacyReason, string>();

      var disallowedMods = MelonHandler.Mods.Where(mod => !(mod is SpeedrunTools) && !ALLOWED_MODS.Contains(mod.Info.Name));
      if (disallowedMods.Count() > 0)
      {
        var disallowedModNames = disallowedMods.Select(mod => mod.Info.Name);
        illegitimacyReasons[RunIllegitimacyReason.DISALLOWED_MODS] =
          $"Disallowed mods are active: {string.Join(", ", disallowedModNames)}";
      }

      var disallowedPlugins = MelonHandler.Plugins.Where(plugin => !ALLOWED_PLUGINS.Contains(plugin.Info.Name));
      if (disallowedPlugins.Count() > 0)
      {
        var disallowedPluginNames = disallowedPlugins.Select(mod => mod.Info.Name);
        illegitimacyReasons[RunIllegitimacyReason.DISALLOWED_PLUGINS] =
          $"Disallowed plugins are active: {string.Join(", ", disallowedPluginNames)}";
      }

      return illegitimacyReasons;
    }

    private static string ColorText(string text, Color color) =>
      $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

    private static void UpdateMainMenuText(string text)
    {
      var menuText = GameObject.Find(MENU_TEXT_NAME);

      if (menuText == null)
      {
        menuText = new GameObject(MENU_TEXT_NAME);
        var tmp = menuText.AddComponent<TMPro.TextMeshPro>();
        tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
        tmp.fontSize = 1.6f;
        tmp.rectTransform.sizeDelta = new Vector2(2, 2);
        tmp.rectTransform.position = new Vector3(2.65f, 1.8f, 9.6f);
      }

      menuText.GetComponent<TMPro.TextMeshPro>().SetText(text);
    }

    // TODO: Make this work
    private static Texture TextToTexture(string text)
    {
      if (SHOW_BONEWORKS_LOGO_OVERLAY)
      {
        if (s_overlayTexture == null)
        {
          s_overlayTexture = Resources
            .FindObjectsOfTypeAll<Texture2D>()
            .Where(tex => tex.name == "sprite_title_boneworks")
            .First();
        }
        return s_overlayTexture;
      }

      // Build our text as a renderable mesh
      var textGameObject = new GameObject("SpeedrunTools_LoadingText")
      {
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
      GL.LoadProjectionMatrix(Matrix4x4.Ortho(
        -tmp.rectTransform.sizeDelta.x / 2.0f,
        tmp.rectTransform.sizeDelta.x / 2.0f,
        -tmp.rectTransform.sizeDelta.x / 2.0f,
        tmp.rectTransform.sizeDelta.y / 2.0f,
        -10,
        100
      ));

      // Render the viewed text to our texture
      var texture = RenderTexture.GetTemporary(2048, 2048, 24, RenderTextureFormat.ARGB32);
      Graphics.SetRenderTarget(texture);
      GL.Clear(false, true, Color.clear);
      tmp.renderer.material.SetPass(0);
      Graphics.DrawMeshNow(tmp.mesh, Matrix4x4.identity);

      // Restore state
      GL.PopMatrix();
      RenderTexture.active = prevActiveRt;
      // GameObject.Destroy(textGameObject);

      // TESTING: Put the texture on the carpet in front of the speedrun text in main menu to see if it worked
      var carpet = GameObject.Find("carpet_2");
      if (carpet)
      {
        var renderer = carpet.GetComponent<MeshRenderer>();
        renderer.material.mainTexture = texture;
      } else
      {
        Utils.LogDebug("Carpet not found");
      }

      return texture;
    }

    private static void ShowOverlay(string text, Color color)
    {
      var texture = TextToTexture(text);

      if (OpenVR.Overlay == null) return;

      if (s_overlayHandle == OpenVR.k_ulOverlayHandleInvalid)
      {
        var createError = OpenVR.Overlay.CreateOverlay(OVERLAY_KEY, "SpeedrunTools Run Status", ref s_overlayHandle);
        if (createError != EVROverlayError.None)
        {
          var findError = OpenVR.Overlay.FindOverlay(OVERLAY_KEY, ref s_overlayHandle);
          if (findError != EVROverlayError.None)
            throw new System.Exception($"Could not find speedrun overlay: {findError}");
        }
      }
      var showError = OpenVR.Overlay.ShowOverlay(s_overlayHandle);
      if (showError == EVROverlayError.InvalidHandle || showError == EVROverlayError.UnknownOverlay)
      {
        var findError = OpenVR.Overlay.FindOverlay(OVERLAY_KEY, ref s_overlayHandle);
        if (findError != EVROverlayError.None)
          throw new System.Exception($"Could not find speedrun overlay: {findError}");
      }
      var ovrTexture = new Texture_t()
      {
        handle = texture.GetNativeTexturePtr(),
        eType = SteamVR.instance.textureType,
        eColorSpace = EColorSpace.Auto,
      };
      OpenVR.Overlay.SetOverlayTexture(s_overlayHandle, ref ovrTexture);
      if (color != null)
        OpenVR.Overlay.SetOverlayColor(s_overlayHandle, color.r, color.g, color.b);
      OpenVR.Overlay.SetOverlayAlpha(s_overlayHandle, 1);
      OpenVR.Overlay.SetOverlayWidthInMeters(s_overlayHandle, 0.6f);
      OpenVR.Overlay.SetOverlayAutoCurveDistanceRangeInMeters(s_overlayHandle, 1, 2);
      var vecMouseScale = new HmdVector2_t()
      {
        v0 = 1,
        v1 = 1,
      };
      var uvOffset = new Vector4(0, 0, 1, 1);
      var textureBounds = new VRTextureBounds_t()
      {
        uMin = (0 + uvOffset.x) * uvOffset.z,
        vMin = (1 + uvOffset.y) * uvOffset.w,
        uMax = (1 + uvOffset.x) * uvOffset.z,
        vMax = (0 + uvOffset.y) * uvOffset.w,
      };
      OpenVR.Overlay.SetOverlayTextureBounds(s_overlayHandle, ref textureBounds);
      OpenVR.Overlay.SetOverlayMouseScale(s_overlayHandle, ref vecMouseScale);
      OpenVR.Overlay.SetOverlayInputMethod(s_overlayHandle, VROverlayInputMethod.None);
      var transform = new SteamVR_Utils.RigidTransform()
      {
        pos = new Vector3(0, -0.4f, 1),
        rot = Quaternion.identity,
        scl = new Vector3(1, 1, 1),
      };
      var ovrMatrix = transform.ToHmdMatrix34();
      OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(
        s_overlayHandle,
        OpenVR.k_unTrackedDeviceIndex_Hmd,
        ref ovrMatrix
      );
      OpenVR.Overlay.SetHighQualityOverlay(s_overlayHandle);
      OpenVR.Overlay.SetOverlayFlag(s_overlayHandle, VROverlayFlags.Curved, false);
      OpenVR.Overlay.SetOverlayFlag(s_overlayHandle, VROverlayFlags.RGSS4X, true);
    }

    private static void HideOverlay()
    {
      if (OpenVR.Overlay == null || s_overlayHandle == OpenVR.k_ulOverlayHandleInvalid) return;
      OpenVR.Overlay.HideOverlay(s_overlayHandle);
    }

    private static void OnLoadingStart()
    {
      s_loadingStartTime = Time.time;
      var startTime = s_relativeStartTime.HasValue ? s_relativeStartTime.Value : Time.time;
      var duration = System.TimeSpan.FromSeconds(Time.time - startTime);
      var modeText = SpeedrunTools.s_isLegitRunActive
        ? ColorText("enabled", s_is100PercentRun ? COLOR_BLUE : COLOR_GREEN)
        : ColorText("disabled", COLOR_RED);
      var text = $@"{(s_is100PercentRun ? "100% speedrun mode" : "Speedrun mode")} {modeText}
v{BuildInfo.Version}
{duration:h\:mm\:ss\.ff}";
      ShowOverlay(
        text,
        SpeedrunTools.s_isLegitRunActive ? s_is100PercentRun ? COLOR_BLUE : COLOR_GREEN : COLOR_RED
      );
    }

    public override void OnApplicationStart()
    {
      RestoreSaveBackupIfExists();
    }

    public override void OnSceneWasLoaded(int sceneBuildIndex, string sceneName)
    {
      if (!SpeedrunTools.s_isLegitRunActive)
      {
        RestoreSaveBackupIfExists();
      } else if (sceneBuildIndex == Utils.SCENE_MENU_IDX && !s_is100PercentRun)
      {
        ResetSave();
        s_didReset = true;
      }
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      if (s_loadingStartTime.HasValue && s_relativeStartTime.HasValue)
      {
        s_relativeStartTime += Time.time - s_loadingStartTime.Value;
        s_loadingStartTime = null;
      }

      var previousSceneIdx = s_currentSceneIdx;
      s_currentSceneIdx = buildIndex;

      if (previousSceneIdx == Utils.SCENE_MENU_IDX)
        s_relativeStartTime = Time.time;

      if (s_currentSceneIdx == Utils.SCENE_MENU_IDX)
      {
        var heading = SpeedrunTools.s_isLegitRunActive
          ? s_is100PercentRun
            ? ColorText("100% speedrun mode enabled", COLOR_BLUE)
            : ColorText("Speedrun mode enabled", COLOR_GREEN)
          : ColorText("Speedrun mode disabled", COLOR_RED);
        var text = $@"{heading}
» You are{(SpeedrunTools.s_isLegitRunActive ? "" : " not")} allowed to submit runs to leaderboard
» Practice features are {(SpeedrunTools.s_isLegitRunActive ? "disabled" : "enabled")}
» Press A + B on both controllers at once (or CTRL + S) to toggle speedrun mode
» Press CTRL + H to toggle speedrun mode for 100% runs
{(s_didReset ? $@"» Save was reset (deactivate speedrun mode to restore)" : "")}";
        UpdateMainMenuText(text);
      }
      s_didReset = false;
    }

    [HarmonyPatch(typeof(LoadingScreenPackage), nameof(LoadingScreenPackage.StartAlpha))]
    class LoadingScreenPackage_StartAlpha_Patch
    {
      [HarmonyPrefix()]
      internal static void Prefix()
      {
        OnLoadingStart();
      }
    }

    [HarmonyPatch(typeof(LoadingScreenPackage), nameof(LoadingScreenPackage.AlphaOverlays))]
    class LoadingScreenPackage_AlphaOverlays_Patch
    {
      [HarmonyPrefix()]
      internal static void Prefix()
      {
        HideOverlay();
      }
    }

    private static string GetBackupPath()
    {
      var dirName = Path.GetFileName(Application.persistentDataPath);
      return Path.Combine(Application.persistentDataPath, "..", $"{dirName}.speedrun_backup");
    }

    private static void BackupSave()
    {
      // Backup existing save file to:
      // %UserProfile%\AppData\LocalLow\Stress Level Zero\BONEWORKS.backup
      var backupPath = GetBackupPath();
      if (Directory.Exists(backupPath))
        throw new System.Exception("Backup already exists");
      MelonLogger.Msg($"Backing up save to: {backupPath}");
      CopyDirectory(Application.persistentDataPath, backupPath, true);
    }

    private static void ResetSave()
    {
      MelonLogger.Msg("Resetting save");
      var dataManager = Object.FindObjectOfType<Data_Manager>();

      var oldData = dataManager.data_player;
      var additionalLighting = oldData.additionalLighting;
      var aliasing = oldData.aliasing;
      var ambientOcclusion = oldData.ambientOcclusion;
      var audio_GlobalVolume = oldData.audio_GlobalVolume;
      var audio_Music = oldData.audio_Music;
      var audio_SFX = oldData.audio_SFX;
      var beltRightSide = oldData.beltRightSide;
      var bloom = oldData.bloom;
      var fisheye = oldData.fisheye;
      var fisheyeLocation = oldData.fisheyeLocation;
      var isAdaptiveOn = oldData.isAdaptiveOn;
      var isInverted = oldData.isInverted;
      var isRightHanded = oldData.isRightHanded;
      var joySensitivityNew = oldData.joySensitivityNew;
      var language = oldData.language;
      var loco_Curve = oldData.loco_Curve;
      var loco_DegreesPerSnap = oldData.loco_DegreesPerSnap;
      var loco_Direction = oldData.loco_Direction;
      var loco_SnapDegreesPerFrame = oldData.loco_SnapDegreesPerFrame;
      var mod_Haptic = oldData.mod_Haptic;
      var motionBlur = oldData.motionBlur;
      var mouseSensitivityNew = oldData.mouseSensitivityNew;
      var offset_Floor = oldData.offset_Floor;
      var offset_Sitting = oldData.offset_Sitting;
      var physicsUpdateRate = oldData.physicsUpdateRate;
      var player_Height = oldData.player_Height;
      var player_Name = oldData.player_Name;
      var playIntroCheck = oldData.playIntroCheck;
      var playMode = oldData.playMode;
      var profile_Name = oldData.profile_Name;
      var quality = oldData.quality;
      var resX = oldData.resX;
      var resY = oldData.resY;
      var shadowQuality = oldData.shadowQuality;
      var SnapEnabled = oldData.SnapEnabled;
      var standing = oldData.standing;
      var subtitlesOn = oldData.subtitlesOn;
      var TextureResolution = oldData.TextureResolution;
      var VirtualCrouching = oldData.VirtualCrouching;

      dataManager.DATA_DEFAULT_ALL();

      var newData = dataManager.data_player;
      newData.additionalLighting = additionalLighting;
      newData.aliasing = aliasing;
      newData.ambientOcclusion = ambientOcclusion;
      newData.audio_GlobalVolume = audio_GlobalVolume;
      newData.audio_Music = audio_Music;
      newData.audio_SFX = audio_SFX;
      newData.beltRightSide = beltRightSide;
      newData.bloom = bloom;
      newData.fisheye = fisheye;
      newData.fisheyeLocation = fisheyeLocation;
      newData.isAdaptiveOn = isAdaptiveOn;
      newData.isInverted = isInverted;
      newData.isRightHanded = isRightHanded;
      newData.joySensitivityNew = joySensitivityNew;
      newData.language = language;
      newData.loco_Curve = loco_Curve;
      newData.loco_DegreesPerSnap = loco_DegreesPerSnap;
      newData.loco_Direction = loco_Direction;
      newData.loco_SnapDegreesPerFrame = loco_SnapDegreesPerFrame;
      newData.mod_Haptic = mod_Haptic;
      newData.motionBlur = motionBlur;
      newData.mouseSensitivityNew = mouseSensitivityNew;
      newData.offset_Floor = offset_Floor;
      newData.offset_Sitting = offset_Sitting;
      newData.physicsUpdateRate = physicsUpdateRate;
      newData.player_Height = player_Height;
      newData.player_Name = player_Name;
      newData.playIntroCheck = playIntroCheck;
      newData.playMode = playMode;
      newData.profile_Name = profile_Name;
      newData.quality = quality;
      newData.resX = resX;
      newData.resY = resY;
      newData.shadowQuality = shadowQuality;
      newData.SnapEnabled = SnapEnabled;
      newData.standing = standing;
      newData.subtitlesOn = subtitlesOn;
      newData.TextureResolution = TextureResolution;
      newData.VirtualCrouching = VirtualCrouching;

      dataManager.DATA_SAVE();
    }

    private static void RestoreSaveBackupIfExists()
    {
      try
      {
        var backupPath = GetBackupPath();
        if (Directory.Exists(backupPath))
        {
          MelonLogger.Msg($"Restoring save backup from: {backupPath}");
          Directory.Delete(Application.persistentDataPath);
          Directory.Move(backupPath, Application.persistentDataPath);
        }
      } catch (System.Exception err)
      {
        MelonLogger.Error("WARNING: There is a backup of your save file created when enabling " +
          "speedrun mode but restoring it failed with this error:");
        MelonLogger.Error(err);
      }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
    {
      var dir = new DirectoryInfo(sourceDir);
      if (!dir.Exists)
        throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
      DirectoryInfo[] dirs = dir.GetDirectories();
      Directory.CreateDirectory(destinationDir);
      foreach (FileInfo file in dir.GetFiles())
      {
        string targetFilePath = Path.Combine(destinationDir, file.Name);
        file.CopyTo(targetFilePath);
      }
      if (recursive)
      {
        foreach (DirectoryInfo subDir in dirs)
        {
          string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
          CopyDirectory(subDir.FullName, newDestinationDir, true);
        }
      }
    }
  }
}
