using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.TasTool.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.TasTool.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.TasTool.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.TasTool.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.TasTool.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.TasTool.Mod), Sst.TasTool.BuildInfo.NAME,
    Sst.TasTool.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://bonelab.thunderstore.io/package/jakzo/TasTool/")]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.TasTool {
public static class BuildInfo {
  public const string NAME = "TasTool";
  public const string DESCRIPTION = "Utilities for building tool assisted speedruns.";
}
}
