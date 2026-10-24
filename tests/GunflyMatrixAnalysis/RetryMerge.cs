using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

static class RetryMerge
{
  sealed class Table
  {
    public string Root;
    public string[] Headers;
    public Dictionary<string, int> Columns;
    public string[][] Rows;
    public string Get(string[] row, string key) => row[Columns[key]] ?? "";
    public static Table Read(string file)
    {
      using var doc = JsonDocument.Parse(File.ReadAllText(file));
      var r = doc.RootElement;
      var t = new Table
      {
        Root = r.GetProperty("runDirectory").GetString(),
        Headers = r.GetProperty("headers").EnumerateArray().Select(x => x.GetString()).ToArray(),
        Rows = r.GetProperty("rows").EnumerateArray().Select(x => x.EnumerateArray().Select(y => y.ValueKind == JsonValueKind.Null ? "" : y.GetString()).ToArray()).ToArray()
      };
      t.Columns = t.Headers.Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);
      return t;
    }
  }

  public static void Merge(string originalFile, string selectionFile, string output, string[] retryFiles)
  {
    var original = Table.Read(originalFile);
    if (original.Rows.Length != 320)
    {
      throw new Exception("Original matrix must have 320 configurations");
    }

    using var selection = JsonDocument.Parse(File.ReadAllText(selectionFile));
    var decisions = selection.RootElement.GetProperty("decisions").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetString());
    var retries = new Dictionary<string, (Table Table, string[] Row)>();
    foreach (var file in retryFiles)
    {
      var table = Table.Read(file);
      foreach (var row in table.Rows)
      {
        string id = table.Get(row, "case_id");
        if (!decisions.ContainsKey(id))
        {
          throw new Exception("Unknown retry ID " + id);
        }

        if (!decisions[id].GetProperty("retry").GetBoolean())
        {
          continue; // Separate control trials are not replacements.
        }

        if (retries.TryGetValue(id, out var previous) && previous.Table.Get(previous.Row, "status") != "setup_failed")
        {
          throw new Exception("Multiple scored retry batches for " + id);
        }

        retries[id] = (table, row);
      }
    }

    var extra = new[]
    {
      "original_status",
      "original_reason",
      "original_attempts",
      "original_runtime_status",
      "original_runtime_reason",
      "original_validation_source",
      "original_frame_log",
      "retry_decision",
      "retry_status",
      "retry_reason",
      "retry_attempts",
      "retry_validation_source",
      "retry_frame_log",
      "retry_fixture_note"
    };
    var headers = original.Headers.Concat(extra).ToArray();
    var columns = headers.Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);
    var result = new List<string[]>();
    int promoted = 0, attempts = 0;
    foreach (var source in original.Rows)
    {
      string id = original.Get(source, "case_id");
      var row = new string[headers.Length];
      Array.Copy(source, row, source.Length);
      void Set(string key, string value) => row[columns[key]] = value;
      Set("original_status", original.Get(source, "status"));
      Set("original_reason", original.Get(source, "reason"));
      Set("original_attempts", original.Get(source, "attempts"));
      Set("original_runtime_status", original.Get(source, "runtime_status"));
      Set("original_runtime_reason", original.Get(source, "runtime_reason"));
      Set("original_validation_source", original.Get(source, "validation_source"));
      Set("original_frame_log", Path.GetFullPath(Path.Combine(original.Root, original.Get(source, "frame_log"))));
      var decision = decisions[id];
      bool requested = decision.GetProperty("retry").GetBoolean();
      Set("retry_decision", decision.GetProperty("why").GetString());
      Set("retry_attempts", "0");
      if (requested)
      {
        if (!retries.TryGetValue(id, out var item))
        {
          throw new Exception("Missing requested retry " + id);
        }

        var table = item.Table;
        var retry = item.Row;
        string status = table.Get(retry, "status");
        if (status == "setup_failed")
        {
          throw new Exception("Requested retries still lack usable weapon setup: " + id);
        }

        string NormalizeCondition(string value) => value == "baseline_without_performance_mod" ? "absent" : value;
        if (NormalizeCondition(original.Get(source, "condition")) != NormalizeCondition(table.Get(retry, "condition")))
        {
          throw new Exception("Retry mod condition mismatch " + id);
        }

        foreach (string key in new[]
        {
          "rifle_prefab",
          "requested_hz",
          "look_angle_from_down_deg",
          "hmd_height_m",
          "player_height_m"
        }

        )
        {
          if (original.Get(source, key) != table.Get(retry, key))
          {
            throw new Exception("Retry configuration mismatch " + id + " " + key);
          }
        }

        int count = int.Parse(table.Get(retry, "attempts"), CultureInfo.InvariantCulture);
        if (count < 1 || count > 2)
        {
          throw new Exception("Retry attempt allowance exceeded " + id);
        }

        attempts += count;
        foreach (var key in original.Headers)
        {
          Set(key, table.Get(retry, key));
        }

        string log = Path.GetFullPath(Path.Combine(table.Root, table.Get(retry, "frame_log")));
        if (!File.Exists(log))
        {
          throw new Exception("Missing retry evidence " + log);
        }

        Set("frame_log", Path.GetRelativePath(original.Root, log));
        Set("retry_status", status);
        Set("retry_reason", table.Get(retry, "reason"));
        Set("retry_attempts", count.ToString());
        Set("retry_validation_source", table.Get(retry, "validation_source"));
        Set("retry_frame_log", log);
        Set("attempts", (int.Parse(original.Get(source, "attempts"), CultureInfo.InvariantCulture) + count).ToString());
        Set("retry_fixture_note", "Native weapon/magazine geometry and declared launch/acceptance rules retained. Inspect saved geometry and protocol for fixture controls. Setup-only failures did not consume scored attempts.");
        if (status == "valid")
        {
          promoted++;
        }
      } else
      {
        Set("retry_status", "not_retried");
      }

      result.Add(row);
    }

    if (result.Select(x => x[columns["case_id"]]).Distinct().Count() != 320)
    {
      throw new Exception("Duplicate or missing configurations");
    }

    int valid = result.Count(x => x[columns["status"]] == "valid");
    File.WriteAllText(output, JsonSerializer.Serialize(new { headers, rows = result, runDirectory = original.Root, originalVerifiedFile = Path.GetFullPath(originalFile), retryVerifiedFiles = retryFiles.Select(Path.GetFullPath), originalValid = original.Rows.Count(x => original.Get(x, "status") == "valid"), additionalValid = promoted, retryAttempts = attempts, valid, failed = 320 - valid }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Merged 320 configurations: valid={valid}, recovered_by_retries={promoted}, retry_attempts={attempts}");
  }
}
