using MelonLoader;

namespace Sst {
public class Dbg {
  private const string PREF_ID = "printDebugLogs";

  private static MelonPreferences_Category _prefCategory;

  public static void Init(string prefCategoryId) {
    _prefCategory = MelonPreferences.CreateCategory(prefCategoryId);
    _prefCategory.CreateEntry(PREF_ID, false, "Print debug logs to console");
  }

  public static void Log(string msg, params object[] data) {
    if (_prefCategory.GetEntry<bool>(PREF_ID).Value)
      MelonLogger.Msg($"dbg: {msg}", data);
  }
}
}