using MelonLoader;
using HarmonyLib;
using StressLevelZero.Data;
using StressLevelZero.Arena;
using UnityEngine;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Sst.Speedruns {
class SaveUtilities {
  /// Set to true to stop save files from being overwritten by the game
  public static bool BlockSave = false;

  private const int NUM_SLOTS = 5;

  public static string GetBackupPath() {
    var dirName = Path.GetFileName(Application.persistentDataPath);
    return Path.Combine(Application.persistentDataPath, "..",
                        $"{dirName}.speedrun_backup");
  }

  public static void BackupSave() {
    // Backup existing save file to BONEWORKS.speedrun_backup
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

  public static void RestoreSaveFileResource(string saveResourceName) {
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

  public static void SaveData() {
    for (int slot = 0; slot < NUM_SLOTS; slot++)
      Data_Manager.Instance.DATA_SAVE(slot);
    AmmoData.Save();
    LevelData.Save();
    ReclaimerData.Save();
    TimeTrialData.Save();
  }

  public static void LoadData() {
    for (int slot = 0; slot < NUM_SLOTS; slot++)
      Data_Manager.Instance.DATA_LOAD(slot);
    Data_Manager.Instance.DATA_PROFILE_SET(0);
    AmmoData.Load();
    LevelData.Load();
    ReclaimerData.Load();
    TimeTrialData.Load();
  }

  public static void DeleteSave() {
    Utils.LogDebug("Deleting save");
    foreach (var filePath in Directory.EnumerateFiles(
                 Application.persistentDataPath)) {
      if (filePath.EndsWith("output_log.txt"))
        continue;
      File.Delete(filePath);
    }
  }

  public static void RestoreSaveBackupIfExists() {
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

  private static void ResetArena() {
    var arenaDataPath =
        Application.persistentDataPath + new Arena_DataManager().arenaDataPath;
    if (File.Exists(arenaDataPath))
      File.Delete(arenaDataPath);
  }

  public static void ResetSave() {
    MelonLogger.Msg("Resetting save");
    var oldData = Data_Manager.Instance.data_player;
    Data_Manager.Instance.DATA_DEFAULT_ALL();
    ResetArena();
    RestorePlayerPrefs(oldData);
  }

  public static void RestorePlayerPrefs(Data_Player oldData) {
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

  // Save disabling (used to prevent overwriting of newly copied-in save files
  // when reloading a level)
  [HarmonyPatch(typeof(Data_Manager), nameof(Data_Manager.DATA_SAVE))]
  class Data_Manager_DATA_SAVE_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix() => !BlockSave;
  }
  [HarmonyPatch(typeof(ReclaimerData), nameof(ReclaimerData.Save))]
  class ReclaimerData_Save_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix() => !BlockSave;
  }
  [HarmonyPatch(typeof(AmmoData), nameof(AmmoData.Save))]
  class AmmoData_Save_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix() => !BlockSave;
  }
  [HarmonyPatch(typeof(LevelData), nameof(LevelData.Save))]
  class LevelData_Save_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix() => !BlockSave;
  }
  [HarmonyPatch(typeof(TimeTrialData), nameof(TimeTrialData.Save))]
  class TimeTrialData_Save_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix() => !BlockSave;
  }
}
}
