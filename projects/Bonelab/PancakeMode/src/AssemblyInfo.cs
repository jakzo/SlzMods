using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.PancakeMode.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.PancakeMode.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.PancakeMode.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.PancakeMode.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.PancakeMode.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.PancakeMode.Mod), Sst.PancakeMode.BuildInfo.NAME,
    Sst.PancakeMode.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://bonelab.thunderstore.io/package/jakzo/PancakeMode/"
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.PancakeMode {
public static class BuildInfo {
  public const string NAME = "PancakeMode";
  public const string DESCRIPTION =
      "Allows playing the game with a keyboard, mouse and monitor instead of " +
      "VR.";
}
}
