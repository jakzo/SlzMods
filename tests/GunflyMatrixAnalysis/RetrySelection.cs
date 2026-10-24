using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

static class RetrySelection
{
  public static void Create(string verifiedPath, string outputDirectory)
  {
    using var doc = JsonDocument.Parse(File.ReadAllText(verifiedPath));
    var root = doc.RootElement;
    var headers = root.GetProperty("headers").EnumerateArray().Select(x => x.GetString()).ToArray();
    var rows = root.GetProperty("rows").EnumerateArray().Select(x => x.EnumerateArray().Select(y => y.ValueKind == JsonValueKind.Null ? "" : y.GetString()).ToArray()).ToArray();
    var col = headers.Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i);
    string Get(string[] row, string name) => row[col[name]];
    string Family(string gun) => gun == "M16" || gun.StartsWith("rifle_M16_") ? "M16" : "MK18";
    if (rows.Length != 320 || rows.Select(x => Get(x, "case_id")).Distinct().Count() != 320)
    {
      throw new Exception("Expected independently verified complete 320-case sweep");
    }

    var valid = rows.Where(x => Get(x, "status") == "valid").ToArray();
    var requests = new List<string>();
    var decisions = new List<object>();
    foreach (var row in rows)
    {
      string id = Get(row, "case_id"), gun = Get(row, "rifle_prefab"), hz = Get(row, "requested_hz"), angle = Get(row, "look_angle_from_down_deg");
      int sameGunOtherRates = valid.Count(x => Get(x, "rifle_prefab") == gun && Get(x, "look_angle_from_down_deg") == angle);
      int sameFamilyRate = valid.Count(x => Family(Get(x, "rifle_prefab")) == Family(gun) && Get(x, "requested_hz") == hz && Get(x, "look_angle_from_down_deg") == angle);
      bool failed = Get(row, "status") == "no_valid_measurement";
      // Repeated failures at singular near-vertical angles are not measurement-only failures:
      // offline exact-window scans show extensive unsettled motion despite clean frames.
      bool nearVertical = angle == "0" || angle == "-15";
      bool supported = sameGunOtherRates >= 2 || sameFamilyRate >= 2;
      bool retry = failed && !nearVertical && supported;
      string why = !failed ? "already_valid_no_retry" : nearVertical ? "skip_repeated_near_vertical_instability" : supported ? "retry_intermittent_failure_with_multiple_successful_comparable_controls" : "skip_no_repeated_successful_comparable_flight";
      if (retry)
      {
        requests.Add(string.Join("\t", id, gun, hz, angle));
      }

      decisions.Add(new { id, gun, hz, angle, originalStatus = Get(row, "status"), originalReason = Get(row, "reason"), offlineNotes = Get(row, "validation_notes"), sameGunOtherRates, sameFamilyRate, retry, why, maxAdditionalAttempts = retry ? 2 : 0 });
    }

    Directory.CreateDirectory(outputDirectory);
    File.WriteAllLines(Path.Combine(outputDirectory, "retry-requests.tsv"), requests);
    File.WriteAllText(Path.Combine(outputDirectory, "retry-selection.json"), JsonSerializer.Serialize(new { verifiedPath, policy = "After exact-window recovery, retry non-near-vertical failures supported by at least two successful same-gun/angle rate controls or same-family/rate/angle controls. Do not assert skipped classes are impossible.", selected = requests.Count, decisions }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Selected " + requests.Count + " configurations for up to two additional attempts each.");
    foreach (var request in requests)
    {
      Console.WriteLine(request);
    }
  }
}
