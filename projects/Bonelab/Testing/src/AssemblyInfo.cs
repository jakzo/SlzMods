using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Jakzo.Testing.BuildInfo.NAME)]
[assembly:AssemblyDescription(Jakzo.Testing.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Jakzo.Testing.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Jakzo.Testing.AppVersion.Value)]
[assembly:AssemblyFileVersion(Jakzo.Testing.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Jakzo.Testing.Mod), Jakzo.Testing.BuildInfo.NAME,
    Jakzo.Testing.AppVersion.Value, Sst.Metadata.AUTHOR, ""
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Jakzo.Testing;

public static class BuildInfo {
  public const string NAME = "Testing";
  public const string DESCRIPTION = "Just for local testing.";
}
