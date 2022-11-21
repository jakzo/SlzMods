using System;
using System.IO;

namespace Sst.Common.LiveSplit {
static class Log {
  public static string LOG_FILE =
      Environment.GetEnvironmentVariable("SST_LIVESPLIT_LOG_PATH");

  public static void Initialize() {
    if (LOG_FILE == null)
      return;
    if (File.Exists(LOG_FILE))
      File.Delete(LOG_FILE);
  }

  public static void Info(string message) { LogImpl("i", message); }
  public static void Warn(string message) { LogImpl("WARN", message); }
  public static void Error(string message) { LogImpl("ERROR", message); }

  private static void LogImpl(string prefix, string message) {
    if (LOG_FILE == null)
      return;
    File.AppendAllText(LOG_FILE, $"{prefix} [{BuildInfo.NAME}] {message}\n");
  }
}
}