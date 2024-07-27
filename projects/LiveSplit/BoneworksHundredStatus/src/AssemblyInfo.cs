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
[assembly:AssemblyVersion(Sst.Livesplit.BoneworksHundredStatus.AppVersion.Value
)]
[assembly:AssemblyFileVersion(
    Sst.Livesplit.BoneworksHundredStatus.AppVersion.Value
)]
[assembly:NeutralResourcesLanguage("en")]
[assembly:ComponentFactory(typeof(LiveSplit.UI.Components.Factory))]

namespace Sst {
public static class BuildInfo {
  public const string NAME = "BoneworksHundredStatus";
  public const string DESCRIPTION =
      "Shows game progress for Boneworks 100% speedruns in LiveSplit.";
}
}
