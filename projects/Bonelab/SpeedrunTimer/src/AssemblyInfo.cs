using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.SpeedrunTimer.BuildInfo.Name)]
[assembly:AssemblyDescription("")]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.SpeedrunTimer.BuildInfo.Name)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.SpeedrunTimer.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.SpeedrunTimer.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.SpeedrunTimer.Mod), Sst.SpeedrunTimer.BuildInfo.Name,
    Sst.SpeedrunTimer.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://bonelab.thunderstore.io/package/jakzo/SpeedrunTimer/"
)]

[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.SpeedrunTimer {
public static class BuildInfo {
  public const string Name = "SpeedrunTimer";
}
}
