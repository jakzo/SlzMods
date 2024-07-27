using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.GunGlitcher.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.GunGlitcher.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.GunGlitcher.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.GunGlitcher.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.GunGlitcher.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.GunGlitcher.Mod), Sst.GunGlitcher.BuildInfo.NAME,
    Sst.GunGlitcher.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://bonelab.thunderstore.io/package/jakzo/GunGlitcher/"
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.GunGlitcher {
public static class BuildInfo {
  public const string NAME = "GunGlitcher";
  public const string DESCRIPTION = "Forces the gun glitch to trigger.";
}
}
