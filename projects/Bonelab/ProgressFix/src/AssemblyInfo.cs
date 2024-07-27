using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.ProgressFix.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.ProgressFix.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.ProgressFix.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.ProgressFix.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.ProgressFix.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.ProgressFix.Mod), Sst.ProgressFix.BuildInfo.NAME,
    Sst.ProgressFix.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://bonelab.thunderstore.io/package/jakzo/ProgressFix/"
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.ProgressFix {
public static class BuildInfo {
  public const string NAME = "ProgressFix";
  public const string DESCRIPTION =
      "Fixes the game progression percentage displayed in the main menu.";
}
}
