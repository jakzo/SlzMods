using MelonLoader;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle(BoneworksSpeedrunTools.BuildInfo.Name)]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany(BoneworksSpeedrunTools.BuildInfo.Company)]
[assembly: AssemblyProduct(BoneworksSpeedrunTools.BuildInfo.Name)]
[assembly: AssemblyCopyright("Created by " + BoneworksSpeedrunTools.BuildInfo.Author)]
[assembly: AssemblyTrademark(BoneworksSpeedrunTools.BuildInfo.Company)]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
//[assembly: Guid("")]
[assembly: AssemblyVersion(BoneworksSpeedrunTools.BuildInfo.Version)]
[assembly: AssemblyFileVersion(BoneworksSpeedrunTools.BuildInfo.Version)]
[assembly: NeutralResourcesLanguage("en")]
[assembly: MelonInfo(typeof(BoneworksSpeedrunTools.BoneworksSpeedrunTools), BoneworksSpeedrunTools.BuildInfo.Name, BoneworksSpeedrunTools.BuildInfo.Version, BoneworksSpeedrunTools.BuildInfo.Author, BoneworksSpeedrunTools.BuildInfo.DownloadLink)]


// Create and Setup a MelonModGame to mark a Mod as Universal or Compatible with specific Games.
// If no MelonModGameAttribute is found or any of the Values for any MelonModGame on the Mod is null or empty it will be assumed the Mod is Universal.
// Values for MelonModGame can be found in the Game's app.info file or printed at the top of every log directly beneath the Unity version.
[assembly: MelonGame(null, null)]