using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.BoneworksPerformance.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.BoneworksPerformance.BuildInfo.DESCRIPTION)]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.BoneworksPerformance.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:ComVisible(false)]
[assembly:AssemblyVersion(Sst.BoneworksPerformance.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.BoneworksPerformance.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.BoneworksPerformance.Mod),
    Sst.BoneworksPerformance.BuildInfo.NAME,
    Sst.BoneworksPerformance.AppVersion.Value,
    Sst.Metadata.AUTHOR,
    ""
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME_BONEWORKS)]

namespace Sst.BoneworksPerformance;

public static class BuildInfo {
  public const string NAME = "BoneworksPerformance";
  public const string DESCRIPTION =
      "Improves BONEWORKS performance without changing gameplay.";
}
