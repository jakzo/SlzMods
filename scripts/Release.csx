#r "System.IO.Compression"
#r "System.IO.Compression.ZipFile"

using System.IO.Compression;

const string MANIFEST_PATH = "thunderstore/manifest.json";
const string APP_VERSION_PATH = "AppVersion.cs";
const string CHANGELOG_PATH = "CHANGELOG.md";

class SemverType
{
  public string name;
}

SemverType[] semverTypes = {
  new SemverType() { name = "major" },
  new SemverType() { name = "minor" },
  new SemverType() { name = "patch" },
};

string SemverIncrement(string version, int semverTypeIdx)
{
  var parts = version.Split(".");
  parts[semverTypeIdx] = (int.Parse(parts[semverTypeIdx]) + 1).ToString();
  int idx = semverTypeIdx;
  while (++idx < parts.Length) parts[idx] = "0";
  return String.Join(".", parts);
}

var semverTypeArg = Args[0].ToLower();
var semverTypeIdx = Array.FindIndex(semverTypes, type => type.name == semverTypeArg);
if (semverTypeIdx == -1) throw new Exception($"Unknown semver increment type: {semverTypeArg}");

var manifestJson = File.ReadAllText(MANIFEST_PATH);
const string MANIFEST_SEARCH_TERM = "\"version_number\": \"";
var manifestStartIdx = manifestJson.IndexOf(MANIFEST_SEARCH_TERM);
if (manifestStartIdx == -1) throw new Exception("Manifest version not found");
manifestStartIdx += MANIFEST_SEARCH_TERM.Length;
var manifestEndIdx = manifestJson.IndexOf("\"", manifestStartIdx);
var oldVersion = manifestJson.Substring(manifestStartIdx, manifestEndIdx - manifestStartIdx);
var newVersion = SemverIncrement(oldVersion, semverTypeIdx);

var appCode = File.ReadAllText(APP_VERSION_PATH);
const string APP_VERSION_SEARCH_TERM = "Value = \"";
var appStartIdx = appCode.IndexOf(APP_VERSION_SEARCH_TERM);
if (appStartIdx == -1) throw new Exception("App version not found");
appStartIdx += APP_VERSION_SEARCH_TERM.Length;
var appEndIdx = appCode.IndexOf("\"", appStartIdx);

Console.WriteLine($"Old version = {oldVersion}");
Console.WriteLine($"Version increment type = {semverTypeArg}");
Console.WriteLine($"New version = {newVersion}");

File.WriteAllText(
  MANIFEST_PATH,
  manifestJson.Substring(0, manifestStartIdx) + newVersion + manifestJson.Substring(manifestEndIdx)
);
File.WriteAllText(
  APP_VERSION_PATH,
  appCode.Substring(0, appStartIdx) + newVersion + appCode.Substring(appEndIdx)
);

Console.WriteLine("manifest.json and AppVersion.cs version updated");

var changelogDescription = Args[1];
var oldChangelog = File.ReadAllText(CHANGELOG_PATH);
var newChangelog = $"## {newVersion}\n\n{changelogDescription}\n\n{oldChangelog}";
File.WriteAllText(CHANGELOG_PATH, newChangelog);

Console.WriteLine("CHANGELOG.md updated");

var readme = File.ReadAllText("README.md");
File.WriteAllText("thunderstore/README.md", $"{readme}\n# Changelog\n\n{newChangelog}");
const string MODS_DIR = "thunderstore/Mods";
if (Directory.Exists(MODS_DIR))
{
  foreach (var file in Directory.GetFiles(MODS_DIR)) File.Delete(file);
} else
{
  Directory.CreateDirectory(MODS_DIR);
}
File.Copy("bin/Release/SpeedrunTools.dll", MODS_DIR);

Console.WriteLine("Thunderstore files copied");

var thunderstoreZipPath = $"thunderstore/SpeedrunTools_{newVersion}.zip";
using (ZipArchive zip = ZipFile.Open(thunderstoreZipPath, ZipArchiveMode.Create))
{
  zip.CreateEntryFromFile("thunderstore/manifest.json", "manifest.json");
  zip.CreateEntryFromFile("thunderstore/icon.png", "icon.png");
  zip.CreateEntryFromFile("thunderstore/README.md", "README.md");
  zip.CreateEntryFromFile("thunderstore/Mods/SpeedrunTools.dll", "Mods/SpeedrunTools.dll");
}

Console.WriteLine("Thunderstore zip file created");

Console.WriteLine("Setting Github action outputs");
var escapedChangelog = changelogDescription
  .Replace("%", "'%25'")
  .Replace("\n", "'%0A'")
  .Replace("\r", "'%0D'");
Console.WriteLine($"::set-output name=new_version::{newVersion}");
Console.WriteLine($"::set-output name=changelog::{escapedChangelog}");

Console.WriteLine("All done!");
