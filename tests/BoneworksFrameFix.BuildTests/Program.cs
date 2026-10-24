using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

var root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
var project = Path.Combine(root, "projects/Boneworks/BoneworksFrameFix");
var source = Path.Combine(project, "src");
var checks = 0;
void Check(bool valid, string message)
{
  if (!valid) throw new Exception(message);
  checks++;
}

Dictionary<string, (string[] Fields, string[] Methods)> ReadAssembly(string configuration)
{
  using var stream = File.OpenRead(Path.Combine(project, "bin", configuration, "BoneworksFrameFix.dll"));
  using var pe = new PEReader(stream);
  var metadata = pe.GetMetadataReader();
  Check(metadata.GetString(metadata.GetAssemblyDefinition().Name) == "BoneworksFrameFix",
      "Unexpected mod assembly name");
  if (configuration == "Release")
  {
    Check(!metadata.MemberReferences.Any(handle =>
        metadata.GetString(metadata.GetMemberReference(handle).Name) == "SpinWait"),
        "Busy waiting leaked into Release");
    Check(!metadata.MemberReferences.Any(handle =>
        metadata.GetString(metadata.GetMemberReference(handle).Name) == "UpdateActionState"),
        "Background capture must not advance the game's shared SteamVR action state");
    Check(!metadata.MemberReferences.Any(handle =>
    {
      var member = metadata.GetMemberReference(handle);
      if (member.Parent.Kind != HandleKind.TypeReference) return false;
      var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
      return metadata.GetString(type.Namespace) == "MelonLoader" &&
          metadata.GetString(type.Name) == "MelonLogger" &&
          metadata.GetString(member.Name).StartsWith("Msg");
    }), "Informational logging leaked into Release");
  }
  string TypeName(TypeDefinitionHandle handle)
  {
    var type = metadata.GetTypeDefinition(handle);
    var parent = type.GetDeclaringType();
    return (parent.IsNil ? metadata.GetString(type.Namespace) + "." : TypeName(parent) + "+") +
      metadata.GetString(type.Name);
  }
  return metadata.TypeDefinitions.ToDictionary(TypeName,
      handle =>
      {
        var type = metadata.GetTypeDefinition(handle);
        return (
        type.GetFields().Select(handle => metadata.GetString(metadata.GetFieldDefinition(handle).Name)).ToArray(),
        type.GetMethods().Select(handle => metadata.GetString(metadata.GetMethodDefinition(handle).Name)).ToArray()
      );
      });
}

var release = ReadAssembly("Release");
var debug = ReadAssembly("Debug");
var modSource = File.ReadAllText(Path.Combine(source, "Mod.cs"));
foreach (var field in new[] {
    "_poseHistorySampleRate", "_poseHistoryInterpolationDelayTicks", "_poseHistoryCatchUpSpeed",
    "_poseHistoryCatchUpHistorySeconds", "_jumpRiseSpeedThreshold", "_jumpRiseDetectionWindowSeconds",
    "_showPerformanceHud"
})
{
  Check(modSource.Contains(field + " = advanced.CreateEntry("), field + " is outside the Advanced category");
}
var names = new[] {
  "PerformanceCapture", "SuperJumpDiagnostics", "FixedPoseInputDiagnostic",
  "FixedPoseObservation", "ResampledFixedPoseObservation", "ReportWriter",
  "FixedPoseSmoothnessTest", "GunflyCatchUpExperiment", "PoseMatrix3x4", "TimedButtonMath"
};
foreach (var name in names)
{
  Check(!release.Keys.Any(type => type.EndsWith("." + name)), name + " leaked into Release");
  Check(debug.Keys.Any(type => type.EndsWith("." + name)), name + " is missing from Debug");
}
Check(!release.Values.SelectMany(type => type.Methods).Any(name =>
    name.StartsWith("Debug") || name.Contains("Diagnostic") || name == "StartProfiling" ||
    name == "StopProfiling" || name == "ToggleProfiling" ||
    name == "FirstTickAtOrAfter" || name == "CopyChronological" || name == "StateAt" ||
    name == "ToMatrix" || name == "ApplyCalibratedPose" ||
    name == "ToOpenVrMatrix" || name == "AfterController"), "Diagnostic or unused method leaked into Release");
foreach (var field in new[] {
    "_capture", "_diagnostics", "_queryErrors", "_scheduleOverruns",
    "_appliedTicks", "_unavailableTicks", "_lastSummaryTimestamp",
    "_runtimeSettings", "_hasRuntimeSettings", "_receiversReady", "_hasOriginalLockSetting",
    "_suppressed", "_lastSelectedRate", "_slowMotionEdgeLock", "_pendingJumpReleaseTimestamp",
    "_jumpButtonEdges", "_slowMotionEdges"
})
{
  Check(!release.Values.SelectMany(type => type.Fields).Contains(field), field + " leaked into Release");
}
Check(release["Sst.BoneworksFrameFix.FixedUpdatePoseHistory+CurrentFixedPose"].Fields.Length == 9, "Release fixed-pose state grew unexpectedly");
Check(release["Sst.BoneworksFrameFix.PoseHistorySample"].Fields.Length == 4,
    "Diagnostic pose payload leaked into Release");
Check(!release["Sst.BoneworksFrameFix.TrackedPoseValue"].Fields.Any(name => name.StartsWith("Q")),
    "Unused rotations leaked into Release input history");
Check(release["Sst.BoneworksFrameFix.TrackedPoseValue"].Fields.Length == 4,
    "Release input history must store only position and validity");
Check(debug["Sst.BoneworksFrameFix.FixedUpdatePoseHistory+CurrentFixedPose"].Fields.Length > 9, "Debug fixed-pose payload is missing");
foreach (var file in Directory.GetFiles(source, "*.cs", SearchOption.AllDirectories))
{
  if (Path.GetRelativePath(source, file).Replace('\\', '/').StartsWith("Debug/")) continue;
  var text = File.ReadAllText(file);
  Check(!text.Contains("WriteDiagnostic"), "Diagnostic writer outside Debug: " + file);
  foreach (Match block in Regex.Matches(text, @"#if DEBUG\s*\n([\s\S]*?)#endif"))
  {
    Check(!Regex.IsMatch(block.Groups[1].Value, @"(?m)^\s*(private|public|internal)\s"),
        "Debug implementation outside Debug: " + file);
  }
}
Console.WriteLine("Passed " + checks + " Debug/Release boundary checks.");
