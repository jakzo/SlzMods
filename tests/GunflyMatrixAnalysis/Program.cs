using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using Sst.BoneworksPerformance.Debug.Gunfly;

class Prefix : IList<Observation>
{
  public readonly List<Observation> Source;
  public int Length;
  public Prefix(List<Observation> source)
  {
    Source = source;
  }

  public int Count => Length;
  public bool IsReadOnly => true;

  public Observation this[int i] { get => i < Length ? Source[i] : throw new IndexOutOfRangeException(); set => throw new NotSupportedException(); }

  public IEnumerator<Observation> GetEnumerator()
  {
    for (int i = 0; i < Length; i++)
    {
      yield return Source[i];
    }
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  public int IndexOf(Observation x) => throw new NotSupportedException();
  public bool Contains(Observation x) => throw new NotSupportedException();
  public void CopyTo(Observation[] a, int i)
  {
    for (int j = 0; j < Length; j++)
    {
      a[i + j] = Source[j];
    }
  }

  public void Add(Observation x) => throw new NotSupportedException();
  public void Clear() => throw new NotSupportedException();
  public void Insert(int i, Observation x) => throw new NotSupportedException();
  public bool Remove(Observation x) => throw new NotSupportedException();
  public void RemoveAt(int i) => throw new NotSupportedException();
}

class Program
{
  static double D(string s) => string.IsNullOrEmpty(s) ? double.NaN : double.Parse(s, CultureInfo.InvariantCulture);
  static string F(double n) => n.ToString("R", CultureInfo.InvariantCulture);
  static List<string[]> Csv(string file)
  {
    using var p = new TextFieldParser(file);
    p.SetDelimiters(",");
    p.HasFieldsEnclosedInQuotes = true;
    var rows = new List<string[]>();
    while (!p.EndOfData)
    {
      rows.Add(p.ReadFields());
    }

    return rows;
  }

  static List<Observation> Frames(string file)
  {
    var result = new List<Observation>();
    using var reader = new StreamReader(file);
    var h = reader.ReadLine().Split(',').Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);
    string line;
    while ((line = reader.ReadLine()) != null)
    {
      if (line.Length == 0)
      {
        continue;
      }

      var s = line.Split(',');
      double Get(string name) => D(s[h[name]]);
      result.Add(new Observation { Time = Get("elapsed_s"), Dt = Get("render_dt_s"), Tick = (long)Get("fixed_tick"), X = Get("world_x_m"), Y = Get("world_y_m"), Z = Get("world_z_m"), Vx = Get("world_vx_m_s"), Vy = Get("world_vy_m_s"), Vz = Get("world_vz_m_s"), Rbx = Get("rigidbody_vx_m_s"), Rby = Get("rigidbody_vy_m_s"), Rbz = Get("rigidbody_vz_m_s"), RawHeight = Get("raw_hmd_y_m"), Angle = Get("look_angle_from_down_deg"), LeftX = Get("left_x"), LeftY = Get("left_y"), RightX = Get("right_x"), RightY = Get("right_y"), Sprint = Get("sprint_held") == 1, Jog = Get("native_jog") == 1, MagazineContact = Get("magazine_contact") == 1, VelocityValid = Get("velocity_valid") == 1 });
    }

    return result;
  }

  static (Window Window, int Checked, Dictionary<string, int> Reasons) Scan(List<Observation> rows, int hz, double angle, double seconds = 2)
  {
    var prefix = new Prefix(rows);
    var reasons = new Dictionary<string, int>();
    int checkedCount = 0;
    int lastBad = 0;
    for (int i = 1; i < rows.Count; i++)
    {
      var s = rows[i];
      if (s.Tick - rows[i - 1].Tick != 1 || s.Dt < .75 / hz || s.Dt > 1.25 / hz || !s.VelocityValid || Math.Abs(s.RawHeight - 1.76) > .00001 || Math.Abs(s.Angle - angle) > .1 || !s.Sprint || !s.Jog || Math.Abs(s.LeftX) > .00001 || Math.Abs(s.LeftY + 1) > .00001 || Math.Abs(s.RightX) > .00001 || Math.Abs(s.RightY + 1) > .00001)
      {
        lastBad = i;
      }

      if (s.Time < 16 || s.Time - rows[lastBad].Time < seconds + .01)
      {
        continue;
      }

      prefix.Length = i + 1;
      var w = Validation.Evaluate(prefix, hz, angle, 9, false, seconds);
      checkedCount++;
      reasons[w.Reason] = reasons.GetValueOrDefault(w.Reason) + 1;
      if (w.Valid)
      {
        return (w, checkedCount, reasons);
      }
    }

    return (null, checkedCount, reasons);
  }

  static void Main(string[] args)
  {
    var minimumArguments = args.Length == 0 ? 2 : args[0] switch
    {
      "--retry-plan" => 3,
      "--merge-retries" => 5,
      "--case" => 4,
      "--verify-subset" => 3,
      _ => 2
    };
    if (args.Length < minimumArguments)
    {
      throw new ArgumentException("Supply RUN OUTPUT.json, --verify-subset RUN OUTPUT.json, --case CASE Hz ANGLE [SECONDS], --retry-plan VERIFIED.json DIRECTORY, or --merge-retries ORIGINAL.json SELECTION.json OUTPUT.json RETRY.json...");
    }

    if (args[0] == "--retry-plan")
    {
      RetrySelection.Create(args[1], args[2]);
      return;
    }

    if (args[0] == "--merge-retries")
    {
      RetryMerge.Merge(args[1], args[2], args[3], args.Skip(4).ToArray());
      return;
    }

    if (args[0] == "--case")
    {
      var files = Directory.GetFiles(args[1], "attempt-*-frames.csv").OrderBy(x => x);
      foreach (var file in files)
      {
        var frames = Frames(file);
        var scan = Scan(frames, int.Parse(args[2]), D(args[3]), args.Length > 4 ? D(args[4]) : 2);
        Console.WriteLine(JsonSerializer.Serialize(new { file, scan.Checked, scan.Reasons, valid = scan.Window != null, start = scan.Window == null ? 0 : frames[scan.Window.First].Time, end = scan.Window == null ? 0 : frames[scan.Window.Last].Time, horizontal = scan.Window?.Horizontal, vertical = scan.Window?.Vertical }));
      }

      return;
    }

    bool subset = args[0] == "--verify-subset";
    string root = args[subset ? 1 : 0], output = args[subset ? 2 : 1];
    using var progressDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "progress.json")));
    var progress = progressDocument.RootElement;
    int expected = subset ? progress.GetProperty("total").GetInt32() : 320;
    if (expected < 1 || expected > 400 || progress.GetProperty("active").GetBoolean() || progress.GetProperty("phase").GetString() != "complete" || progress.GetProperty("completed").GetInt32() != expected)
    {
      throw new Exception("Cannot finalize an incomplete matrix");
    }

    var source = Csv(Path.Combine(root, "results-raw.csv"));
    var headers = source[0].ToList();
    int originalColumns = headers.Count;
    headers.AddRange(new[] { "validation_source", "window_start_elapsed_s", "window_end_elapsed_s", "runtime_status", "runtime_reason", "validation_notes" });
    if (source.Count != expected + 1)
    {
      throw new Exception("Result count differs from completed configuration count");
    }

    var columns = headers.Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);
    var outputRows = new List<string[]>();
    int runtimeValid = 0, recovered = 0, failed = 0;
    foreach (var original in source.Skip(1))
    {
      var row = new string[headers.Count];
      Array.Copy(original, row, originalColumns);
      string Get(string key) => row[columns[key]];
      void Set(string key, string value) => row[columns[key]] = value;
      Set("runtime_status", Get("status"));
      Set("runtime_reason", Get("reason"));
      int hz = (int)D(Get("requested_hz"));
      double angle = D(Get("look_angle_from_down_deg"));
      Window selected = null;
      List<Observation> selectedFrames = null;
      string selectedPath = null;
      int selectedAttempt = 0;
      if (Get("status") == "valid")
      {
        selectedPath = Path.Combine(root, Get("frame_log"));
        selectedFrames = Frames(selectedPath);
        selected = Validation.Evaluate(selectedFrames, hz, angle, 9, false, D(Get("window_seconds")) < 1.5 ? 1 : 2);
        if (!selected.Valid)
        {
          throw new Exception("Runtime accepted a window that does not revalidate: " + Get("case_id") + " " + selected.Reason);
        }

        if (Math.Abs(selected.Horizontal - D(Get("world_horizontal_median_m_s"))) > 1e-7 || Math.Abs(selected.Vertical - D(Get("world_vertical_median_m_s"))) > 1e-7)
        {
          throw new Exception("Median does not reproduce for " + Get("case_id"));
        }

        selectedAttempt = (int)D(Get("source_attempt"));
        Set("validation_source", "runtime_reproduced");
        runtimeValid++;
      } else if (Get("status") == "no_valid_measurement")
      {
        string caseDirectory = Path.GetDirectoryName(Path.Combine(root, Get("frame_log")));
        var notes = new List<string>();
        foreach (double windowSeconds in new[]
        {
          2.0,
          1.0
        }

        )
        {
          foreach (var file in Directory.GetFiles(caseDirectory, "attempt-*-frames.csv").OrderBy(x => x))
          {
            var frames = Frames(file);
            var scan = Scan(frames, hz, angle, windowSeconds);
            notes.Add(Path.GetFileName(file) + ": window=" + windowSeconds + " checked=" + scan.Checked + " " + string.Join(";", scan.Reasons.Select(x => x.Key + "=" + x.Value)));
            if (scan.Window == null)
            {
              continue;
            }

            selected = scan.Window;
            selectedFrames = frames;
            selectedPath = file;
            selectedAttempt = int.Parse(Path.GetFileName(file).Split('-')[1]);
            break;
          }

          if (selected != null)
          {
            break;
          }
        }

        Set("validation_notes", string.Join(" / ", notes));
        if (selected != null)
        {
          recovered++;
          Set("status", "valid");
          Set("reason", "valid_stable_flight");
          Set("validation_source", "offline_exact_window");
        } else
        {
          failed++;
          Set("validation_source", "offline_no_qualifying_window");
        }
      } else
      {
        failed++;
        Set("validation_source", "setup_failed_no_measurement");
      }

      if (selected != null)
      {
        Set("window_seconds", F(selected.Seconds));
        Set("render_fps", F(selected.Fps));
        Set("physics_hz", F(selected.PhysicsHz));
        Set("world_horizontal_median_m_s", F(selected.Horizontal));
        Set("world_vertical_median_m_s", F(selected.Vertical));
        Set("world_vx_median_m_s", F(selected.Vx));
        Set("world_vz_median_m_s", F(selected.Vz));
        Set("rigidbody_horizontal_median_m_s", F(selected.RbHorizontal));
        Set("rigidbody_vertical_median_m_s", F(selected.RbVertical));
        Set("rigidbody_vx_median_m_s", F(selected.RbX));
        Set("rigidbody_vz_median_m_s", F(selected.RbZ));
        Set("world_horizontal_p95_minus_p05_m_s", F(selected.HorizontalSpread));
        Set("world_vertical_p95_minus_p05_m_s", F(selected.VerticalSpread));
        Set("world_lateral_p95_minus_p05_m_s", F(selected.LateralSpread));
        Set("world_horizontal_half_window_drift_m_s", F(selected.HorizontalDrift));
        Set("world_vertical_half_window_drift_m_s", F(selected.VerticalDrift));
        Set("lateral_drift_flag", Math.Abs(selected.Lateral) > .15 ? "1" : "0");
        Set("source_attempt", selectedAttempt.ToString());
        Set("frame_log", Path.GetRelativePath(root, selectedPath));
        Set("window_start_elapsed_s", F(selectedFrames[selected.First].Time));
        Set("window_end_elapsed_s", F(selectedFrames[selected.Last].Time));
      }

      outputRows.Add(row);
      Console.WriteLine(Get("case_id") + " " + Get("status") + " " + Get("validation_source"));
    }

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
    File.WriteAllText(output, JsonSerializer.Serialize(new { headers, rows = outputRows, runDirectory = root, runtimeValid, recovered, failed }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Verified {outputRows.Count} configurations; runtime={runtimeValid}, recovered={recovered}, no_valid_measurement={failed}");
  }
}
