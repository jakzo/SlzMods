using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.Colliders.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.Colliders.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.Colliders.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.Colliders.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.Colliders.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(typeof(Sst.Colliders.Mod), Sst.Colliders.BuildInfo.NAME,
                    Sst.Colliders.AppVersion.Value, Sst.Metadata.AUTHOR,
                    "https://bonelab.thunderstore.io/package/jakzo/Colliders/")]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.Colliders {
public static class BuildInfo {
  public const string NAME = "Colliders";
  public const string DESCRIPTION = "Makes colliders visible.";
}
}
