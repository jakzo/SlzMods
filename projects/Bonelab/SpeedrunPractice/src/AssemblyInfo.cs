using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.SpeedrunPractice.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.SpeedrunPractice.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.SpeedrunPractice.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.SpeedrunPractice.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.SpeedrunPractice.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.SpeedrunPractice.Mod), Sst.SpeedrunPractice.BuildInfo.NAME,
    Sst.SpeedrunPractice.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://bonelab.thunderstore.io/package/jakzo/SpeedrunPractice/"
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.SpeedrunPractice {
public static class BuildInfo {
  public const string NAME = "SpeedrunPractice";
  public const string DESCRIPTION = "Tools for practicing speedruns.";
}
}
