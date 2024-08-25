using System.Linq;
using MelonLoader;

namespace Sst {
public class Dbg {
  private static MelonLogger.Instance _logger;
  private static MelonPreferences_Entry<bool> _prefPrintDebugLogs;

  public static void
  Init(string prefCategoryId, MelonLogger.Instance logger = null) {
    _logger = logger;
    var category = MelonPreferences.CreateCategory(prefCategoryId);
    _prefPrintDebugLogs = category.CreateEntry(
        "printDebugLogs", false, "Print debug logs",
        "Print debug logs to console", false, true
    );
  }

  public static void Log(params object[] data) {
#if !DEBUG
    if (_prefPrintDebugLogs.Value) {
#endif
      var msg = "dbg: " +
          string.Join(" ", data.Select(d => d == null ? "" : d.ToString()));
      if (_logger != null) {
        _logger.Msg(msg);
      } else {
        MelonLogger.Msg(msg);
      }
#if !DEBUG
    }
#endif
  }
}
}
