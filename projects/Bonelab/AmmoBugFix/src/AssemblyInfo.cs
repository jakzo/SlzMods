using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.AmmoBugFix.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.AmmoBugFix.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.AmmoBugFix.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.AmmoBugFix.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.AmmoBugFix.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.AmmoBugFix.Mod), Sst.AmmoBugFix.BuildInfo.NAME,
    Sst.AmmoBugFix.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://bonelab.thunderstore.io/package/jakzo/AmmoBugFix/"
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.AmmoBugFix {
public static class BuildInfo {
  public const string NAME = "AmmoBugFix";
  public const string DESCRIPTION = "Fixes bug where ammo sometimes doesn't " +
                                    "spawn after breaking ammo crates.";
}
}
