using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.LoadMirror.BuildInfo.NAME)]
[assembly:AssemblyDescription("")]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.LoadMirror.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.LoadMirror.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.LoadMirror.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(
    typeof(Sst.LoadMirror.Mod), Sst.LoadMirror.BuildInfo.NAME, Sst.LoadMirror.AppVersion.Value,
    Sst.Metadata.AUTHOR,
    "https://bonelab.thunderstore.io/package/jakzo/LoadMirror/")]

[assembly:MelonGame(Sst.Metadata.DEVELOPER, Sst.Metadata.GAME)]

namespace Sst.LoadMirror {
public static class BuildInfo { public const string NAME = "LoadMirror"; }
}
