using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sst.BoneworksPerformance;

internal static class Program {
  private static int _tests;

  private static int Main(string[] arguments) {
    try {
      TestIdentityMatrixAndTranslation();
      TestMatrixRotation();
      TestMatrixRoundTrip();
      TestPoseInterpolation();
      TestPoseExtrapolation();
      TestInvalidPoseInterpolation();
      TestRingBufferWraparound();
      TestTimestampBracketing();
      TestHeadVerticalVelocityWindow();
      TestFixedTimeMapping();
      TestCatchUpFixedTickTimeline();
      TestButtonEdgeAssignment();
      TestButtonStateAtFixedTicks();
      TestSlowMotionDoubleClickReplay();
      TestConcurrentSnapshots();
      if (arguments.Length > 0)
        TestSmokeReport(arguments[0]);
      Console.WriteLine("Passed " + _tests + " pose-history tests.");
      return 0;
    } catch (Exception exception) {
      Console.Error.WriteLine(exception);
      return 1;
    }
  }

  private static void TestIdentityMatrixAndTranslation() {
    var pose = PoseHistoryMath.FromMatrix(
        Matrix(1, 0, 0, 1.25f, 0, 1, 0, -2.5f, 0, 0, 1, 3.75f),
        true, true, 200
    );
    Equal(1.25f, pose.X, 0.00001f, "identity translation x");
    Equal(-2.5f, pose.Y, 0.00001f, "identity translation y");
    Equal(3.75f, pose.Z, 0.00001f, "identity translation z");
    Equal(0f, pose.Qx, 0.00001f, "identity quaternion x");
    Equal(0f, pose.Qy, 0.00001f, "identity quaternion y");
    Equal(0f, pose.Qz, 0.00001f, "identity quaternion z");
    Equal(1f, pose.Qw, 0.00001f, "identity quaternion w");
    Pass();
  }

  private static void TestMatrixRotation() {
    var pose = PoseHistoryMath.FromMatrix(
        Matrix(0, 0, 1, 0, 0, 1, 0, 0, -1, 0, 0, 0),
        true, true, 200
    );
    Equal(0f, pose.Qx, 0.0001f, "rotation quaternion x");
    Equal((float)Math.Sqrt(0.5), pose.Qy, 0.0001f,
          "rotation quaternion y");
    Equal(0f, pose.Qz, 0.0001f, "rotation quaternion z");
    Equal((float)Math.Sqrt(0.5), pose.Qw, 0.0001f,
          "rotation quaternion w");
    Pass();
  }

  private static void TestPoseInterpolation() {
    var first = PoseHistoryMath.FromMatrix(
        Matrix(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0),
        true, true, 200
    );
    var second = PoseHistoryMath.FromMatrix(
        Matrix(0, 0, 1, 10, 0, 1, 0, 4, -1, 0, 0, -2),
        true, true, 200
    );
    var midpoint = PoseHistoryMath.Interpolate(first, second, 0.5f);
    Equal(5f, midpoint.X, 0.0001f, "interpolated x");
    Equal(2f, midpoint.Y, 0.0001f, "interpolated y");
    Equal(-1f, midpoint.Z, 0.0001f, "interpolated z");
    Equal(0.3826834f, midpoint.Qy, 0.0001f, "interpolated rotation y");
    Equal(0.9238795f, midpoint.Qw, 0.0001f, "interpolated rotation w");
    Pass();
  }

  private static void TestPoseExtrapolation() {
    var first = PoseHistoryMath.FromMatrix(
        Matrix(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0),
        true, true, 200
    );
    var second = PoseHistoryMath.FromMatrix(
        Matrix(0, 0, 1, 1, 0, 1, 0, 2, -1, 0, 0, -1),
        true, true, 200
    );
    var predicted = PoseHistoryMath.Extrapolate(first, second, 2f);
    Equal(2f, predicted.X, 0.0001f, "extrapolated x");
    Equal(4f, predicted.Y, 0.0001f, "extrapolated y");
    Equal(-2f, predicted.Z, 0.0001f, "extrapolated z");
    Equal(1f, predicted.Qy, 0.0001f,
          "extrapolated rotation y");
    Equal(0f, predicted.Qw, 0.0001f,
          "extrapolated rotation w");
    Pass();
  }

  private static void TestMatrixRoundTrip() {
    var original = PoseHistoryMath.FromMatrix(
        Matrix(0, 0, 1, 4, 0, 1, 0, -3, -1, 0, 0, 2),
        true, true, 200
    );
    var roundTrip = PoseHistoryMath.FromMatrix(
        PoseHistoryMath.ToMatrix(original), true, true, 200
    );
    Equal(original.X, roundTrip.X, 0.0001f, "round-trip x");
    Equal(original.Y, roundTrip.Y, 0.0001f, "round-trip y");
    Equal(original.Z, roundTrip.Z, 0.0001f, "round-trip z");
    var quaternionDot = Math.Abs(
        original.Qx * roundTrip.Qx + original.Qy * roundTrip.Qy +
        original.Qz * roundTrip.Qz + original.Qw * roundTrip.Qw
    );
    Equal(1f, quaternionDot, 0.0001f, "round-trip rotation");
    Pass();
  }

  private static void TestInvalidPoseInterpolation() {
    var invalid = new TrackedPoseValue {IsValid = false};
    var valid = new TrackedPoseValue {
      IsValid = true, X = 7f, Qw = 1f,
    };
    var result = PoseHistoryMath.Interpolate(invalid, valid, 0.25f);
    Equal(7f, result.X, 0f, "valid sample wins over invalid sample");
    Pass();
  }

  private static void TestRingBufferWraparound() {
    var buffer = new PoseHistoryBuffer(3);
    Add(buffer, 10);
    Add(buffer, 20);
    Add(buffer, 30);
    Add(buffer, 40);
    var samples = buffer.CopyChronological();
    Equal(3, samples.Length, "wrapped sample count");
    Equal(20L, samples[0].Timestamp, "oldest retained timestamp");
    Equal(40L, samples[2].Timestamp, "newest retained timestamp");
    Equal(1L, samples[0].Sequence, "oldest retained sequence");
    Equal(3L, samples[2].Sequence, "newest retained sequence");
    Pass();
  }

  private static void TestTimestampBracketing() {
    var buffer = new PoseHistoryBuffer(4);
    Add(buffer, 20);
    Add(buffer, 30);
    Add(buffer, 40);
    True(buffer.TryGetBracket(
             25, out var before, out var after, out var status
         ),
         "middle bracket exists");
    Equal(PoseBracketStatus.Interpolated, status,
          "middle bracket status");
    Equal(20L, before.Timestamp, "middle bracket before");
    Equal(30L, after.Timestamp, "middle bracket after");
    True(buffer.TryGetBracket(5, out before, out after, out status),
         "early bracket exists");
    Equal(PoseBracketStatus.BeforeHistory, status,
          "early bracket status");
    Equal(20L, before.Timestamp, "early bracket clamps before");
    Equal(20L, after.Timestamp, "early bracket clamps after");
    True(buffer.TryGetBracket(50, out before, out after, out status),
         "late bracket exists");
    Equal(PoseBracketStatus.AfterHistory, status,
          "late bracket status");
    Equal(40L, before.Timestamp, "late bracket clamps before");
    Equal(40L, after.Timestamp, "late bracket clamps after");
    True(buffer.TryGetLatestPair(out before, out after),
         "latest sample pair exists");
    Equal(30L, before.Timestamp, "latest pair previous timestamp");
    Equal(40L, after.Timestamp, "latest pair newest timestamp");
    Pass();
  }

  private static void TestHeadVerticalVelocityWindow() {
    const long frequency = 1000;
    var buffer = new PoseHistoryBuffer(8);
    AddHead(buffer, 0, 1.70f);
    AddHead(buffer, 50, 1.72f);
    AddHead(buffer, 100, 1.75f);
    AddHead(buffer, 150, 1.79f);
    AddHead(buffer, 200, 1.83f);
    True(buffer.TryGetHeadVerticalVelocity(
             100, frequency, out var velocity
         ), "head velocity window is available");
    Equal(0.8f, velocity, 0.0001f,
          "head velocity uses the complete 100 ms window");
    Pass();
  }

  private static void TestFixedTimeMapping() {
    var mapper = new FixedTimeMapper();
    mapper.ObserveFrame(5_000_000, 10f, 1f);
    True(mapper.TryMap(
             10.125f, 10_000_000, out var target,
             out var anchorTimestamp, out var anchorGameTime,
             out var anchorTimeScale
         ), "fixed time maps from frame anchor");
    Equal(6_250_000L, target, "fixed time target timestamp");
    Equal(5_000_000L, anchorTimestamp, "fixed time anchor timestamp");
    Equal(10f, anchorGameTime, 0f, "fixed time anchor game time");
    Equal(1f, anchorTimeScale, 0f, "fixed time anchor time scale");

    mapper.ObserveFrame(8_000_000, 20f, 0.5f);
    True(mapper.TryMap(
             20.25f, 10_000_000, out target,
             out anchorTimestamp, out anchorGameTime,
             out anchorTimeScale
         ), "later frame observation refreshes the timeline anchor");
    Equal(13_000_000L, target, "refreshed timeline target timestamp");
    Equal(8_000_000L, anchorTimestamp, "refreshed timeline anchor timestamp");
    Equal(20f, anchorGameTime, 0f, "refreshed anchor game time");
    Equal(0.5f, anchorTimeScale, 0f, "refreshed anchor time scale");

    var scaledMapper = new FixedTimeMapper();
    scaledMapper.ObserveFrame(8_000_000, 20f, 0.5f);
    True(scaledMapper.TryMap(
             20.125f, 10_000_000, out target,
             out anchorTimestamp, out anchorGameTime,
             out anchorTimeScale
         ), "scaled fixed time maps from initial anchor");
    Equal(10_500_000L, target, "scaled fixed time target timestamp");
    Pass();
  }

  private static void TestConcurrentSnapshots() {
    var buffer = new PoseHistoryBuffer(128);
    var writer = Task.Run(() => {
      for (var i = 0; i < 20000; i++)
        Add(buffer, i);
    });
    while (!writer.IsCompleted) {
      var samples = buffer.CopyChronological();
      for (var i = 1; i < samples.Length; i++) {
        True(samples[i - 1].Sequence < samples[i].Sequence,
             "concurrent snapshot sequence order");
        True(samples[i - 1].Timestamp < samples[i].Timestamp,
             "concurrent snapshot timestamp order");
      }
    }
    writer.GetAwaiter().GetResult();
    var final = buffer.CopyChronological();
    Equal(128, final.Length, "concurrent final sample count");
    Equal(19999L, final[final.Length - 1].Timestamp,
          "concurrent final timestamp");
    Pass();
  }

  private static void TestCatchUpFixedTickTimeline() {
    const long frequency = 10_000_000;
    const float fixedDelta = 1f / 144f;
    var mapper = new FixedTimeMapper();
    mapper.ObserveFrame(50_000_000, 10f, 1f);
    var timeline = new FixedTickTimeline(mapper, 2.0);
    var timestamps = new long[5];
    for (var i = 0; i < timestamps.Length; i++) {
      var fixedTime = 10f + (i + 1) * fixedDelta;
      True(timeline.TryAdvance(
               fixedTime, fixedDelta, 1f, frequency, i == 0, false,
               out timestamps[i], out _, out var reanchored
           ), "catch-up fixed tick maps");
      True(!reanchored, "sequential catch-up tick does not reanchor");
    }
    var expectedSpacing = fixedDelta * frequency;
    for (var i = 1; i < timestamps.Length; i++)
      Equal(expectedSpacing, timestamps[i] - timestamps[i - 1], 1.0,
            "catch-up ticks have fixed real-time spacing");

    mapper.ObserveFrame(51_500_000, 10.15f, 1f);
    True(timeline.TryAdvance(
             10f + 6 * fixedDelta, fixedDelta, 1f, frequency, true,
             false,
             out var afterShortHitch, out var shortHitchMapped,
             out var reanchoredAfterShortHitch
         ), "first tick after a short hitch maps");
    True(!reanchoredAfterShortHitch,
         "short hitch keeps the fixed-tick timeline continuous");
    var shortHitchSpacing =
        afterShortHitch - timestamps[timestamps.Length - 1];
    True(shortHitchSpacing >= expectedSpacing - 1.0 &&
         shortHitchSpacing <= expectedSpacing * 2.0 + 1.0,
         "short-hitch recovery is bounded to double speed");
    True(Math.Abs(shortHitchMapped - afterShortHitch) < frequency,
         "short-hitch mapping remains within the continuity limit");

    var doubleMapper = new FixedTimeMapper();
    doubleMapper.ObserveFrame(50_000_000, 10f, 1f);
    var doubleTimeline = new FixedTickTimeline(doubleMapper, 2.0);
    True(doubleTimeline.TryAdvance(
             10f + fixedDelta, fixedDelta, 1f, frequency, true, false,
             out var doubleFirst, out _, out _
         ), "double-speed timeline starts");
    doubleMapper.ObserveFrame(52_000_000, 10f + fixedDelta, 1f);
    True(doubleTimeline.TryAdvance(
             10f + 2 * fixedDelta, fixedDelta, 1f, frequency, true,
             false, out var doubleSecond, out _, out _
         ), "double-speed timeline corrects positive phase error");
    Equal(expectedSpacing * 2.0, doubleSecond - doubleFirst, 2.0,
          "default catch-up advances the input clock at double speed");
    Equal(expectedSpacing, doubleTimeline.LastCorrectionTicks, 2.0,
          "double-speed catch-up adds one fixed interval of correction");

    var protectedMapper = new FixedTimeMapper();
    protectedMapper.ObserveFrame(50_000_000, 10f, 1f);
    var protectedTimeline = new FixedTickTimeline(protectedMapper, 2.0);
    True(protectedTimeline.TryAdvance(
             10f + fixedDelta, fixedDelta, 1f, frequency, true, false,
             out var protectedFirst, out _, out _
         ), "protected timeline starts");
    protectedMapper.ObserveFrame(51_500_000, 10.14f, 1f);
    True(protectedTimeline.TryAdvance(
             10f + 2 * fixedDelta, fixedDelta, 1f, frequency, true, true,
             out var protectedSecond, out _, out _
         ), "rising-head timeline advances");
    Equal(expectedSpacing, protectedSecond - protectedFirst, 1.0,
          "rising head keeps uncorrected fixed-tick spacing");
    True(protectedTimeline.LastForwardCorrectionSuppressed,
         "rising head suppresses positive clock correction");
    Equal(0L, protectedTimeline.LastCorrectionTicks,
          "suppressed correction applies no timestamp offset");

    mapper.ObserveFrame(80_000_000, 11f, 1f);
    True(timeline.TryAdvance(
             11f + fixedDelta, fixedDelta, 1f, frequency, true,
             false,
             out var resynchronized, out var independentlyMapped,
             out var reanchoredAfterGap
         ), "first tick after a rendered-frame gap maps");
    True(reanchoredAfterGap,
         "first tick after a rendered-frame gap reanchors");
    Equal(independentlyMapped, resynchronized,
          "render-frame boundary removes accumulated timeline drift");
    Pass();
  }

  private static void TestButtonEdgeAssignment() {
    var ticks = new[] {1_000L, 1_100L, 1_200L, 1_300L, 1_400L};
    Equal(0, TimedButtonMath.FirstTickAtOrAfter(999, ticks),
          "edge before batch belongs to first fixed tick");
    Equal(2, TimedButtonMath.FirstTickAtOrAfter(1_200, ticks),
          "edge on a fixed tick belongs to that tick");
    Equal(3, TimedButtonMath.FirstTickAtOrAfter(1_201, ticks),
          "edge between ticks belongs to the following tick");
    Equal(-1, TimedButtonMath.FirstTickAtOrAfter(1_401, ticks),
          "future edge has no tick in this batch");
    Pass();
  }

  private static void TestButtonStateAtFixedTicks() {
    var edges = new[] {
      new TimedButtonEdge {Timestamp = 1_050, Pressed = true},
      new TimedButtonEdge {Timestamp = 1_275, Pressed = false},
    };
    True(!TimedButtonMath.StateAt(false, edges, 1_000),
         "button starts released");
    True(TimedButtonMath.StateAt(false, edges, 1_200),
         "button is held after its press edge");
    True(!TimedButtonMath.StateAt(false, edges, 1_300),
         "button is released after its release edge");
    Pass();
  }

  private static void TestSlowMotionDoubleClickReplay() {
    var replay = new SlowMotionReplayState(1000);
    Equal(
        SlowMotionReplayCommand.DecreaseTimeScale,
        replay.Apply(new TimedButtonEdge(1000, true)),
        "first slow-motion press decreases time scale"
    );
    Equal(
        SlowMotionReplayCommand.None,
        replay.Apply(new TimedButtonEdge(1050, false)),
        "first release schedules rather than toggles"
    );
    Equal(1300L, replay.PendingToggleTimestamp,
          "release schedules a quarter-second toggle");
    Equal(
        SlowMotionReplayCommand.DecreaseTimeScale,
        replay.Apply(new TimedButtonEdge(1125, true)),
        "second press survives in the same slow rendered frame"
    );
    Equal(0L, replay.PendingToggleTimestamp,
          "second press cancels the first release timer");
    replay.Apply(new TimedButtonEdge(1175, false));
    Equal(
        SlowMotionReplayCommand.None,
        replay.ConsumeToggleAt(1424),
        "second release does not toggle early"
    );
    Equal(
        SlowMotionReplayCommand.ToggleTimeScale,
        replay.ConsumeToggleAt(1425),
        "second release toggles after exactly a quarter second"
    );
    Pass();
  }

  private static void TestSmokeReport(string directory) {
    var metadataPath = Path.Combine(directory, "metadata.txt");
    var backgroundPath = Path.Combine(directory, "openvr-pose-history.csv");
    var fixedPath = Path.Combine(directory, "fixed-tick-poses.csv");
    var resampledPath = Path.Combine(
        directory, "resampled-fixed-tick-poses.csv"
    );
    var reportPath = Path.Combine(directory, "report.html");
    True(File.Exists(metadataPath), "smoke metadata exists");
    True(File.Exists(backgroundPath), "background pose CSV exists");
    True(File.Exists(fixedPath), "fixed-tick pose CSV exists");
    True(File.Exists(resampledPath), "resampled pose CSV exists");
    True(File.Exists(reportPath), "HTML report exists");

    var metadata = new Dictionary<string, string>();
    foreach (var line in File.ReadLines(metadataPath)) {
      var separator = line.IndexOf('=');
      if (separator > 0)
        metadata[line.Substring(0, separator)] = line.Substring(separator + 1);
    }
    Equal("complete", metadata["poseHistoryStatus"],
          "pose sampler completed");
    Equal(0, Int(metadata, "poseHistoryQueryErrors"),
          "pose sampler query errors");
    True(Double(metadata, "poseHistoryActualHz") >= 200.0,
         "pose sampler reached at least 200 Hz");
    True(Double(metadata, "poseHistoryActualHz") <= 300.0,
         "pose sampler stayed near its requested rate");

    var background = ReadAndValidateCsv(backgroundPath, 1, 2);
    var fixedSamples = ReadAndValidateCsv(fixedPath, 4, 5);
    var resampled = ReadAndValidateCsv(resampledPath, 4, 7);
    Equal(Int(metadata, "poseHistoryBackgroundSamples"), background.Count,
          "background CSV row count");
    Equal(Int(metadata, "poseHistoryFixedSamples"), fixedSamples.Count,
          "fixed CSV row count");
    Equal(fixedSamples.Count, resampled.Count,
          "resampled CSV row count");
    True(background.Count > 500, "background CSV has useful history");
    True(fixedSamples.Count > 0, "fixed CSV has observations");
    True(Percentile(background.QueryMicroseconds, 0.95) < 1000.0,
         "background OpenVR query p95 is below one millisecond");
    True(Percentile(fixedSamples.QueryMicroseconds, 0.95) < 1000.0,
         "fixed-tick OpenVR query p95 is below one millisecond");
    var resampledLines = File.ReadAllLines(resampledPath);
    for (var i = 1; i < resampledLines.Length; i++) {
      var columns = resampledLines[i].Split(',');
      var amount = Parse(columns[20]);
      True(amount >= 0.0 && amount <= 1.0,
           "resampled interpolation amount is clamped");
      True(Enum.TryParse<PoseBracketStatus>(columns[12], out _),
           "resampled bracket status is known");
    }

    var html = File.ReadAllText(reportPath);
    True(html.Contains("openvr-pose-history.csv"),
         "HTML links background pose CSV");
    True(html.Contains("fixed-tick-poses.csv"),
         "HTML links fixed-tick pose CSV");
    True(html.Contains("resampled-fixed-tick-poses.csv"),
         "HTML links resampled pose CSV");
    Pass();
  }

  private static CsvMeasurements ReadAndValidateCsv(
      string path, int timestampColumn, int queryColumn
  ) {
    var lines = File.ReadAllLines(path);
    True(lines.Length > 1, Path.GetFileName(path) + " has data rows");
    var columnCount = lines[0].Split(',').Length;
    var previousTimestamp = double.NegativeInfinity;
    var queries = new List<double>(lines.Length - 1);
    for (var i = 1; i < lines.Length; i++) {
      var columns = lines[i].Split(',');
      Equal(columnCount, columns.Length,
            Path.GetFileName(path) + " row " + i + " column count");
      var timestamp = Parse(columns[timestampColumn]);
      var query = Parse(columns[queryColumn]);
      True(timestamp > previousTimestamp,
           Path.GetFileName(path) + " timestamps are strictly increasing");
      True(query >= 0.0 && !double.IsInfinity(query) && !double.IsNaN(query),
           Path.GetFileName(path) + " query duration is finite");
      previousTimestamp = timestamp;
      queries.Add(query);
    }
    return new CsvMeasurements(lines.Length - 1, queries);
  }

  private static int Int(Dictionary<string, string> values, string key) =>
      int.Parse(values[key], CultureInfo.InvariantCulture);

  private static double Double(
      Dictionary<string, string> values, string key
  ) => double.Parse(values[key], CultureInfo.InvariantCulture);

  private static double Parse(string value) =>
      double.Parse(value, CultureInfo.InvariantCulture);

  private static double Percentile(List<double> values, double amount) {
    var sorted = values.OrderBy(value => value).ToArray();
    var index = Math.Min(
        sorted.Length - 1, (int)Math.Floor(amount * sorted.Length)
    );
    return sorted[index];
  }

  private static PoseMatrix3x4 Matrix(
      float m0, float m1, float m2, float m3,
      float m4, float m5, float m6, float m7,
      float m8, float m9, float m10, float m11
  ) => new PoseMatrix3x4 {
    M0 = m0, M1 = m1, M2 = m2, M3 = m3,
    M4 = m4, M5 = m5, M6 = m6, M7 = m7,
    M8 = m8, M9 = m9, M10 = m10, M11 = m11,
  };

  private static void Add(PoseHistoryBuffer buffer, long timestamp) {
    buffer.Add(new PoseHistorySample {Timestamp = timestamp});
  }

  private static void AddHead(
      PoseHistoryBuffer buffer, long timestamp, float y
  ) {
    buffer.Add(new PoseHistorySample {
      Timestamp = timestamp,
      Head = new TrackedPoseValue {IsValid = true, Y = y, Qw = 1f},
    });
  }

  private static void Pass() { _tests++; }

  private static void True(bool value, string message) {
    if (!value)
      throw new Exception("Assertion failed: " + message);
  }

  private static void Equal(int expected, int actual, string message) {
    if (expected != actual)
      throw new Exception(
          "Assertion failed: " + message + ". Expected " + expected +
          ", got " + actual + "."
      );
  }

  private static void Equal(string expected, string actual, string message) {
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
      throw new Exception(
          "Assertion failed: " + message + ". Expected " + expected +
          ", got " + actual + "."
      );
  }

  private static void Equal(
      PoseBracketStatus expected, PoseBracketStatus actual, string message
  ) {
    if (expected != actual)
      throw new Exception(
          "Assertion failed: " + message + ". Expected " + expected +
          ", got " + actual + "."
      );
  }

  private static void Equal(
      SlowMotionReplayCommand expected, SlowMotionReplayCommand actual,
      string message
  ) {
    if (expected != actual)
      throw new Exception(
          "Assertion failed: " + message + ". Expected " + expected +
          ", got " + actual + "."
      );
  }

  private static void Equal(long expected, long actual, string message) {
    if (expected != actual)
      throw new Exception(
          "Assertion failed: " + message + ". Expected " + expected +
          ", got " + actual + "."
      );
  }

  private static void Equal(
      double expected, double actual, double tolerance, string message
  ) {
    if (Math.Abs(expected - actual) > tolerance)
      throw new Exception(
          "Assertion failed: " + message + ". Expected " + expected +
          ", got " + actual + "."
      );
  }

  private static void Equal(
      float expected, float actual, float tolerance, string message
  ) {
    if (Math.Abs(expected - actual) > tolerance)
      throw new Exception(
          "Assertion failed: " + message + ". Expected " + expected +
          ", got " + actual + "."
      );
  }

  private sealed class CsvMeasurements {
    public readonly int Count;
    public readonly List<double> QueryMicroseconds;

    public CsvMeasurements(int count, List<double> queryMicroseconds) {
      Count = count;
      QueryMicroseconds = queryMicroseconds;
    }
  }
}
