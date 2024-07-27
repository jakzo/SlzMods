using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.FlatPlayer.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.FlatPlayer.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.FlatPlayer.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.FlatPlayer.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.FlatPlayer.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.FlatPlayer.FlatBooter), Sst.FlatPlayer.BuildInfo.NAME,
    Sst.FlatPlayer.AppVersion.Value, Sst.Metadata.AUTHOR, ""
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.FlatPlayer {
public static class BuildInfo {
  public const string NAME = "FlatPlayer";
  public const string DESCRIPTION = "Port of FlatPlayer by LlamasHere.";
}
}
