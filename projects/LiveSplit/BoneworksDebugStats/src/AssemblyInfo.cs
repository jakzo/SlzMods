using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using LiveSplit.UI.Components;

[assembly:AssemblyTitle(Sst.BuildInfo.NAME)]
[assembly:AssemblyDescription(Sst.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.Livesplit.BoneworksDebugStats.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.Livesplit.BoneworksDebugStats.AppVersion.Value
)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:ComponentFactory(typeof(LiveSplit.UI.Components.Factory))]

namespace Sst {
public static class BuildInfo {
  public const string NAME = "BoneworksDebugStats";
  public const string DESCRIPTION = "Shows debug info for Boneworks.";
}
}
