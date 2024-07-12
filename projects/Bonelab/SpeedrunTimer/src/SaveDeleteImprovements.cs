using System;
using System.IO;
using UnityEngine;
using MelonLoader;
using HarmonyLib;

#if PATCH4 && ML6
using Il2CppSLZ.Bonelab.SaveData;
using Il2CppSLZ.Marrow.SaveData;
#elif PATCH4 && ML5
using SLZ.Bonelab.SaveData;
using SLZ.Marrow.SaveData;
#elif PATCH2 || PATCH3
using SLZ.SaveData;
#elif PATCH1
using SLZ.Data;
#endif

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

#if PATCH4 && ML5
// Melon Loader 0.5 messes up the order of the generics in MarrowDataManager
// so it cannot be patched unfortunately meaning no stopping Mods from being
// deleted on Quest patch 4 :'(
// [HarmonyPatch(typeof(MarrowDataManager<DataManager, Save, Settings,
//                                        PlayerProgression, PlayerUnlocks>),
//               "get_SavePath")]
// class MarrowDataManager_SavePath_Patch {
//   [HarmonyPrefix()]
//   internal static void Prefix() {
//     Dbg.Log("MarrowDataManager_SavePath_Patch");
//     RestoreModsBackup();
//   }
// }
#else

// According to Ghidra this is the only hookable method which is called after
// the Mods folder is deleted and before the application quits
#if ML6
  [HarmonyPatch(typeof(MarrowDataManager<DataManager, Save, Settings,
                                         PlayerProgression, PlayerUnlocks>),
                "get_SavePath")]
  class MarrowDataManager_SavePath_Patch {
    [HarmonyPrefix()]
    internal static void Prefix() {
      Dbg.Log("MarrowDataManager_SavePath_Patch");
      RestoreModsBackup();
    }
  }
#else
  [HarmonyPatch(typeof(DataManager), nameof(DataManager.SavePath),
                MethodType.Getter)]
  class DataManager_SavePath_Patch {
    [HarmonyPrefix()]
    internal static void Prefix() {
      Dbg.Log("DataManager_SavePath_Patch");
      RestoreModsBackup();
    }
  }
#endif

  private static void RestoreModsBackup() {
    if (_modsBackupPath != null && Directory.Exists(_modsBackupPath) &&
        !Directory.Exists(GetModsPath())) {
      Directory.Move(_modsBackupPath, GetModsPath());
      _modsBackupPath = null;
    }
  }

#endif
}
}
