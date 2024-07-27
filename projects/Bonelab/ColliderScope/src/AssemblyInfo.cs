using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.ColliderScope.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.ColliderScope.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.ColliderScope.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.ColliderScope.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.ColliderScope.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.ColliderScope.Mod), Sst.ColliderScope.BuildInfo.NAME,
    Sst.ColliderScope.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://bonelab.thunderstore.io/package/jakzo/ColliderScope/"
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.ColliderScope {
public static class BuildInfo {
  public const string NAME = "ColliderScope";
  public const string DESCRIPTION = "Shows the shape of physical colliders " +
                                    "and hitboxes. Kinda like X-ray vision.";
}
}
