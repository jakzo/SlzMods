using MelonLoader;

namespace Sst {
public class Dbg {
  private static MelonPreferences_Entry<bool> _prefPrintDebugLogs;

  public static void Init(string prefCategoryId) {
    var category = MelonPreferences.CreateCategory(prefCategoryId);
    _prefPrintDebugLogs = category.CreateEntry("printDebugLogs", false,
                                               "Print debug logs to console");
  }

  public static void Log(string msg, params object[] data) {
    if (_prefPrintDebugLogs.Value)
      MelonLogger.Msg($"dbg: {msg}");
  }
}
}