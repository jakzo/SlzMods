using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.Randomizer.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.Randomizer.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.Randomizer.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.Randomizer.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.Randomizer.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.Randomizer.Mod), Sst.Randomizer.BuildInfo.NAME,
    Sst.Randomizer.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://boneworks.thunderstore.io/package/jakzo/Randomizer/")]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME_BONEWORKS)]

namespace Sst.Randomizer {
public static class BuildInfo {
  public const string NAME = "Randomizer";
  public const string DESCRIPTION =
      "Random item spawns, enemies, level transitions and more.";
}
}
