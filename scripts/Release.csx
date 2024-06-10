#r "System.Xml"

using System.Xml;

string[] semverTypes = { "major", "minor", "patch" };

string SemverIncrement(string version, int semverTypeIdx) {
  var parts = version.Split('.');
  parts[semverTypeIdx] = (int.Parse(parts[semverTypeIdx]) + 1).ToString();
  int idx = semverTypeIdx;
  while (++idx < parts.Length)
    parts[idx] = "0";
  return String.Join(".", parts);
}

bool ShouldReleaseToThunderstore(string projectRelativePath,
                                 string newVersion) {
  if (!File.Exists($"{projectRelativePath}/thunderstore/thunderstore.toml")) {
    Console.WriteLine($"No ThunderStore manifest found. Skipping.");
    return false;
  }
  return true;
}

void UpdateChangelog(string projectRelativePath, string newVersion,
                     string changelogDescription) {
  string changelogPath = $"{projectRelativePath}/CHANGELOG.md";
  if (!File.Exists(changelogPath)) {
    Console.WriteLine($"No changelog found. Skipping.");
    return;
  }

  var oldChangelog = File.ReadAllText(changelogPath);
  var newChangelog =
      $"## {newVersion}\n\n{changelogDescription}\n\n{oldChangelog}";
  File.WriteAllText(changelogPath, newChangelog);

  Console.WriteLine("CHANGELOG.md updated");
}

bool UpdateLiveSplitChangelog(string projectRelativePath, string projectName,
                              string newVersion, string changelogDescription) {
  string changelogPath =
      $"{projectRelativePath}/update.LiveSplit.{projectName}.xml";
  if (!File.Exists(changelogPath)) {
    Console.WriteLine($"No LiveSplit changelog found. Skipping.");
    return false;
  }

  var doc = new XmlDocument();
  doc.Load(changelogPath);
  var updatesEl = doc.SelectSingleNode("/updates");
  var updateEl = doc.CreateElement("update");
  updateEl.SetAttribute("version", newVersion);
  var filesEl = doc.CreateElement("files");
  var fileEl = doc.CreateElement("file");
  var updateDllPath = $"Components/{projectName}.dll";
  fileEl.SetAttribute("path", updateDllPath);
  fileEl.SetAttribute("status", "changed");
  filesEl.AppendChild(fileEl);
  updateEl.AppendChild(filesEl);
  var changelogEl = doc.CreateElement("changelog");
  var changeEl = doc.CreateElement("change");
  changeEl.AppendChild(doc.CreateTextNode(changelogDescription));
  updateEl.AppendChild(changeEl);
  updateEl.AppendChild(changelogEl);
  updatesEl.PrependChild(updateEl);
  doc.Save(changelogPath);

  Console.WriteLine("LiveSplit changelog updated");
  return true;
}

void ReleaseProject(string gameName, string projectName, string semverTypeArg,
                    string changelogDescription) {
  var semverTypeIdx =
      Array.FindIndex(semverTypes, type => type == semverTypeArg);
  if (semverTypeIdx == -1)
    throw new Exception($"Unknown semver increment type: {semverTypeArg}");

  var projectRelativePath = Path.Combine("projects", gameName, projectName);
  var appVersionPath = Path.Combine(projectRelativePath, "AppVersion.cs");
  var appCode = File.ReadAllText(appVersionPath);
  const string APP_VERSION_SEARCH_TERM = "Value = \"";
  var appStartIdx = appCode.IndexOf(APP_VERSION_SEARCH_TERM);
  if (appStartIdx == -1)
    throw new Exception("App version not found");
  appStartIdx += APP_VERSION_SEARCH_TERM.Length;
  var appEndIdx = appCode.IndexOf("\"", appStartIdx);
  var oldVersion = appCode.Substring(appStartIdx, appEndIdx - appStartIdx);
  var newVersion = SemverIncrement(oldVersion, semverTypeIdx);

  Console.WriteLine($"Old version = {oldVersion}");
  Console.WriteLine($"Version increment type = {semverTypeArg}");
  Console.WriteLine($"New version = {newVersion}");

  File.WriteAllText(appVersionPath, appCode.Substring(0, appStartIdx) +
                                        newVersion +
                                        appCode.Substring(appEndIdx));

  UpdateChangelog(projectRelativePath, newVersion, changelogDescription);
  var isLiveSplitComponent = UpdateLiveSplitChangelog(
      projectRelativePath, projectName, newVersion, changelogDescription);

  Console.WriteLine("Setting Github action outputs");
  var escapedChangelog = changelogDescription.Replace("%", "'%25'")
                             .Replace("\n", "'%0A'")
                             .Replace("\r", "'%0D'");
  var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
  File.AppendAllText(githubOutput, $"new_version={newVersion}\n");
  File.AppendAllText(githubOutput, $"changelog={escapedChangelog}\n");
  var releaseThunderstore =
      ShouldReleaseToThunderstore(projectRelativePath, newVersion) ? "true"
                                                                   : "false";
  File.AppendAllText(githubOutput,
                     $"release_thunderstore={releaseThunderstore}\n");
  var releaseLiveSplit = isLiveSplitComponent ? "true" : "false";
  File.AppendAllText(githubOutput, $"release_livesplit={releaseLiveSplit}\n");
}

try {
  var gameName = Args[0];
  var projectName = Args[1];
  var semverTypeArg = Args[2].ToLower();
  var changelogDescription = Args[3];
  ReleaseProject(gameName, projectName, semverTypeArg, changelogDescription);

  Console.WriteLine("All done!");
} catch (Exception ex) {
  Console.WriteLine(ex);
  Environment.Exit(1);
}
