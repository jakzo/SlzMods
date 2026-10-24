using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.BoneworksFrameFix.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.BoneworksFrameFix.BuildInfo.DESCRIPTION)]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.BoneworksFrameFix.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:ComVisible(false)]
[assembly:AssemblyVersion(Sst.BoneworksFrameFix.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.BoneworksFrameFix.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.BoneworksFrameFix.Mod),
    Sst.BoneworksFrameFix.BuildInfo.NAME,
    Sst.BoneworksFrameFix.AppVersion.Value,
    Sst.Metadata.AUTHOR,
    ""
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME_BONEWORKS)]

namespace Sst.BoneworksFrameFix;

public static class BuildInfo {
  public const string NAME = "BoneworksFrameFix";
  public const string DESCRIPTION =
      "Keeps BONEWORKS movement consistent across frame rates.";
}
