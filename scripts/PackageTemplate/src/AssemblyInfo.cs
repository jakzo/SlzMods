using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.$$NAME$$.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.$$NAME$$.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.$$NAME$$.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.$$NAME$$.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.$$NAME$$.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.$$NAME$$.Mod), Sst.$$NAME$$.BuildInfo.NAME,
    Sst.$$NAME$$.AppVersion.Value, Sst.Metadata.AUTHOR,
    "https://$$GAME_LOWER$$.thunderstore.io/package/jakzo/$$NAME$$/"
)]
[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.$$NAME$$;

public static class BuildInfo {
  public const string NAME = "$$NAME$$";
  public const string DESCRIPTION = "$$DESCRIPTION$$";
}
