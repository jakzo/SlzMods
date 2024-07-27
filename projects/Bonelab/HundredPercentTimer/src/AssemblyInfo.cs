using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.HundredPercentTimer.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.HundredPercentTimer.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.HundredPercentTimer.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.HundredPercentTimer.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.HundredPercentTimer.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.HundredPercentTimer.Mod), Sst.HundredPercentTimer.BuildInfo.NAME,
    Sst.HundredPercentTimer.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://bonelab.thunderstore.io/package/jakzo/HundredPercentTimer/"
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.HundredPercentTimer {
public static class BuildInfo {
  public const string NAME = "HundredPercentTimer";
  public const string DESCRIPTION =
      "LiveSplit integration for timing 100% speedruns.";
}
}
