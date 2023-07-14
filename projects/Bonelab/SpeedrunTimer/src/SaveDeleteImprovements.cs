using System;
using System.IO;
using UnityEngine;
using MelonLoader;
using HarmonyLib;
using SLZ.SaveData;

namespace Sst.SpeedrunTimer {
public static class SaveDeleteImprovements {
  private static MelonPreferences_Entry<bool> _prefDeleteModsOnWipe;
  private static string _modsBackupPath;

  private static string
  GetModsPath() => Path.Combine(Application.persistentDataPath, "Mods");

  public static void OnInitialize() {
    _prefDeleteModsOnWipe = Mod.Instance.PrefCategory.CreateEntry<bool>(
        "deleteModsOnWipe", false, "Let mods be deleted when wiping all data",
        "Normally when resetting your save state through the main menu the game will delete all mods including the SpeedrunTimer. For convenience the timer will keep your mods folder. This option reverts to the original behavior of deleting mods.");
  }

  [HarmonyPatch(typeof(DataManager), nameof(DataManager._MSAFAIGE))]
  class DataManager_MSAFAIGE_Patch {
    [HarmonyPrefix()]
    internal static void Prefix() {
      Dbg.Log("DataManager_MSAFAIGE_Prefix");
      if (_prefDeleteModsOnWipe.Value)
        return;
      _modsBackupPath =
          Path.Combine(Application.persistentDataPath, "Mods.backup");
      if (Directory.Exists(_modsBackupPath))
        Directory.Delete(_modsBackupPath, true);
      Directory.Move(GetModsPath(), _modsBackupPath);
    }

    [HarmonyPostfix()]
    internal static void Postfix() {
      Dbg.Log("DataManager_MSAFAIGE_Postfix");
      if (_modsBackupPath == null)
        return;
      if (Directory.Exists(_modsBackupPath))
        MelonLogger.Warning(
            "Failed to restore mods folder on data wipe! Old mods are in Mods.backup now.");
      _modsBackupPath = null;
    }
  }

  // According to dnSpy this is the only hookable method which is called after
  // the Mods folder is deleted and before the application quits
  [HarmonyPatch(typeof(DataManager), "get_SavePath")]
  class DataManager_SavePath_Patch {
    [HarmonyPrefix()]
    internal static void Postfix() {
      Dbg.Log("DataManager_SavePath_Patch");
      if (_modsBackupPath != null && Directory.Exists(_modsBackupPath) &&
          !Directory.Exists(GetModsPath())) {
        Directory.Move(_modsBackupPath, GetModsPath());
        _modsBackupPath = null;
      }
    }
  }
}
}
