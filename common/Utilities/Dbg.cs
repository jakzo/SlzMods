using System.Linq;
using MelonLoader;

namespace Sst {
public class Dbg {
  private static MelonPreferences_Entry<bool> _prefPrintDebugLogs;

  public static void Init(string prefCategoryId) {
    var category = MelonPreferences.CreateCategory(prefCategoryId);
    _prefPrintDebugLogs =
        category.CreateEntry("printDebugLogs", false, "Print debug logs",
                             "Print debug logs to console", false, true);
  }

  public static void Log(params object[] data) {
#if !DEBUG
    if (_prefPrintDebugLogs.Value) {
#endif
      var msg =
          string.Join(" ", data.Select(d => d == null ? "" : d.ToString()));
      MelonLogger.Msg($"dbg: {msg}");
#if !DEBUG
    }
#endif
  }
}
}
