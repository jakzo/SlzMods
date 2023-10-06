using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.Hitboxes.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.Hitboxes.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.Hitboxes.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.Hitboxes.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.Hitboxes.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.Hitboxes.Mod), Sst.Hitboxes.BuildInfo.NAME,
    Sst.Hitboxes.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://bonelab.thunderstore.io/package/jakzo/Hitboxes/")]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.Hitboxes {
public static class BuildInfo {
  public const string NAME = "Hitboxes";
  public const string DESCRIPTION = "Makes hitboxes visible.";
}
}
