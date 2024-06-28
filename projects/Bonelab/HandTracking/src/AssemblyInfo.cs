using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.HandTracking.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.HandTracking.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.HandTracking.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.HandTracking.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.HandTracking.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(typeof(Sst.HandTracking.Mod),
                    Sst.HandTracking.BuildInfo.NAME,
                    Sst.HandTracking.AppVersion.Value, Sst.Metadata.AUTHOR, "")]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.HandTracking {
public static class BuildInfo {
  public const string NAME = "HandTracking";
  public const string DESCRIPTION = "Adds support for hand tracking.";
}
}
