using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly:AssemblyTitle(Sst.BonelabHundredPercentStatus.BuildInfo.NAME)]
[assembly:AssemblyDescription(
    Sst.BonelabHundredPercentStatus.BuildInfo.DESCRIPTION)]
[assembly:AssemblyConfiguration("")]
[assembly:AssemblyCompany(Sst.Metadata.COMPANY)]
[assembly:AssemblyProduct(Sst.BonelabHundredPercentStatus.BuildInfo.NAME)]
[assembly:AssemblyCopyright("Created by " + Sst.Metadata.AUTHOR)]
[assembly:AssemblyTrademark(Sst.Metadata.COMPANY)]
[assembly:AssemblyCulture("")]
[assembly:ComVisible(false)]
//[assembly: Guid("")]
[assembly:AssemblyVersion(Sst.BonelabHundredPercentStatus.AppVersion.Value)]
[assembly:AssemblyFileVersion(Sst.BonelabHundredPercentStatus.AppVersion.Value)]
[assembly:NeutralResourcesLanguage("en")]

namespace Sst.BonelabHundredPercentStatus {
public static class BuildInfo {
  public const string NAME = "BonelabHundredPercentStatus";
  public const string DESCRIPTION =
      "Shows game progress and achievements for Bonelab 100% speedruns in LiveSplit.";
}
}
