using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.ThirdPersonCamera.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.ThirdPersonCamera.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.ThirdPersonCamera.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.ThirdPersonCamera.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.ThirdPersonCamera.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.ThirdPersonCamera.Mod), Sst.ThirdPersonCamera.BuildInfo.NAME,
    Sst.ThirdPersonCamera.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://bonelab.thunderstore.io/package/jakzo/ThirdPersonCamera/"
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.ThirdPersonCamera;

public static class BuildInfo {
  public const string NAME = "ThirdPersonCamera";
  public const string DESCRIPTION = "Makes spectator camera third-person.";
}
