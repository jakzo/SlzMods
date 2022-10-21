using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.BuildInfo.Name)]
[assembly:AssemblyDescription("")]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.BuildInfo.Company)]
[assembly:AssemblyProduct(Sst.BuildInfo.Name)]
[assembly:AssemblyCopyright("Created by " + Sst.BuildInfo.Author)]
[assembly:AssemblyTrademark(Sst.BuildInfo.Company)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.BuildInfo.Version)]
[assembly:AssemblyFileVersion(Sst.BuildInfo.Version)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:MelonInfo(typeof(Sst.Mod), Sst.BuildInfo.Name, Sst.BuildInfo.Version,
                    Sst.BuildInfo.Author, Sst.BuildInfo.DownloadLink)]

[assembly:MelonGame(Sst.BuildInfo.Developer, Sst.BuildInfo.GameName)]
