void CopyBuildsToBin() {
  var binDir = "bin";
  Directory.Delete(binDir, true);
  Directory.CreateDirectory(binDir);

  var projectsDirRelative = "projects";
  foreach (var gameDir in Directory.EnumerateDirectories(projectsDirRelative)) {
    var gameDirRelative = Path.Combine(projectsDirRelative, gameDir);
    foreach (var projectDir in Directory.EnumerateDirectories(
                 gameDirRelative)) {
      var buildFilename = $"{projectDir}.dll";
      var buildFile = Path.Combine(gameDirRelative, projectDir, "bin", "Debug",
                                   buildFilename);
      File.Copy(buildFile, Path.Combine(binDir, $"{gameDir}{buildFilename}"));
    }
  }
}

try {
  CopyBuildsToBin();
  Console.WriteLine("CopyBuildsToBin done!");
} catch (Exception ex) {
  Console.WriteLine(ex);
  Environment.Exit(1);
}
