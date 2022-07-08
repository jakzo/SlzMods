using MelonLoader;
using UnityEngine;
using System.IO;

namespace SpeedrunTools
{
  class FeatureResetSave : Feature
  {
    private static int s_currentSceneIdx;

    public readonly Hotkey HotkeyReset = new Hotkey()
    {
      Predicate = (cl, cr) =>
        s_currentSceneIdx == Utils.SCENE_MENU_IDX && (
          cl.GetAButton() && cl.GetBButton() && cr.GetAButton() && cr.GetBButton() ||
          Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.R)
        ),
      Handler = () =>
      {
        // Backup existing save (if no backup already exists) to
        // %UserProfile%\AppData\LocalLow\Stress Level Zero\BONEWORKS.backup
        try
        {
          var dirName = Path.GetFileName(Application.persistentDataPath);
          var backupPath = Path.Combine(Application.persistentDataPath, "..", $"{dirName}.backup");
          if (!Directory.Exists(backupPath))
          {
            MelonLogger.Msg($"Backing up save to: {backupPath}");
            CopyDirectory(Application.persistentDataPath, backupPath, true);
          } else
          {
            MelonLogger.Msg("Not backing up save (backup already exists)");
          }
        } catch
        {
          MelonLogger.Warning("Failed to backup, continuing with reset anyway");
        }

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

        StressLevelZero.Utilities.BoneworksSceneManager.ReloadScene();
      }
    };

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      s_currentSceneIdx = buildIndex;
    }

    static private void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
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
