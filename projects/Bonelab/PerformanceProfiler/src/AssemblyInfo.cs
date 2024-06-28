using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.PerformanceProfiler.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.PerformanceProfiler.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.PerformanceProfiler.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.PerformanceProfiler.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.PerformanceProfiler.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.PerformanceProfiler.Mod), Sst.PerformanceProfiler.BuildInfo.NAME,
    Sst.PerformanceProfiler.AppVersion.Value, Sst.Metadata.AUTHOR, "")]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.PerformanceProfiler {
public static class BuildInfo {
  public const string NAME = "PerformanceProfiler";
  public const string DESCRIPTION =
      "Logs data for debugging performance issues.";
}
}
