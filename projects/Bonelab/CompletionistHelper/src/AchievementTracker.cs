using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using HarmonyLib;
using SLZ.Bonelab;
using SLZ.SaveData;

namespace Sst.CompletionistHelper {
static class AchievementTracker {
  public static event Action<string, string> OnUnlock;

  public static string SAVE_PATH;

  public static HashSet<string> Unlocked = new HashSet<string>();

  public static Dictionary<string, string> AllAchievements =
      Utilities.Il2cpp.ToDictionary(Achievements.AchievementsDict);

  public static void Initialize() {
    SAVE_PATH =
        Path.Combine(DataManager.SettingsPath, "..", "achievements.txt");
    Unlocked = File.Exists(SAVE_PATH) ? File.ReadAllLines(SAVE_PATH).ToHashSet()
                                      : new HashSet<string>();
  }

  private static void Unlock(string id) {
    if (!AllAchievements.ContainsKey(id) || Unlocked.Contains(id))
      return;

    Unlocked.Add(id);
    SaveUnlockedToFile().ContinueWith(task => {});
    OnUnlock?.Invoke(id, AllAchievements[id]);
  }

  private static async Task SaveUnlockedToFile() {
    byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\n", Unlocked));
    using (var stream = new FileStream(SAVE_PATH, FileMode.Create,
                                       FileAccess.Write, FileShare.Read,
                                       bufferSize: 4096, useAsync: true)) {
      await stream.WriteAsync(bytes, 0, bytes.Length);
    };
  }

  [HarmonyPatch(typeof(Achievements), nameof(Achievements.Unlock))]
  class Achievements_Unlock_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(string id) {
      Dbg.Log($"Achievements_Unlock_Patch: {id}");
      Unlock(id);
    }
  }

  [HarmonyPatch(typeof(Achievements), nameof(Achievements.AWARDACHIEVEMENT))]
  class Achievements_AWARDACHIEVEMENT_Patch {
    [HarmonyPostfix()]
    internal static void Postfix(string name_achievement) {
      Dbg.Log($"Achievements_AWARDACHIEVEMENT_Patch: {name_achievement}");
      Unlock(name_achievement);
    }
  }
}
}
