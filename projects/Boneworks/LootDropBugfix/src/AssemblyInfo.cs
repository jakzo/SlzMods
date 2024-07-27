using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.LootDropBugfix.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.LootDropBugfix.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.LootDropBugfix.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.LootDropBugfix.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.LootDropBugfix.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.LootDropBugfix.Mod), Sst.LootDropBugfix.BuildInfo.NAME,
    Sst.LootDropBugfix.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://boneworks.thunderstore.io/package/jakzo/LootDropBugfix/"
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME_BONEWORKS)]

namespace Sst.LootDropBugfix;

public static class BuildInfo {
  public const string NAME = "LootDropBugfix";
  public const string DESCRIPTION =
      "Fixes bug where dropped loot sometimes does not spawn.";
}
