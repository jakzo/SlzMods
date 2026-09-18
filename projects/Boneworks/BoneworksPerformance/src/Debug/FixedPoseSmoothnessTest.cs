#if DEBUG
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using MelonLoader;
using UnityEngine;

namespace Sst.BoneworksPerformance;

internal sealed class FixedPoseSmoothnessTest {
  private const float StationaryBeforeSeconds = 1f;
  private const float MovementSeconds = 3f;
  private const float TestSeconds = 5f;
  private const float SpeedMetresPerSecond = 1f;
  private const int Capacity = 4096;

  private readonly Row[] _rows = new Row[Capacity];
  private long _startTimestamp;
  private int _count;
  private bool _active;
  private bool _hitchInjected;

  private struct Row {
    public int UnityFrame;
    public int TickInFrame;
    public float FixedTime;
    public long RecordedTimestamp;
    public long TargetTimestamp;
    public float IdealTargetX;
    public float SourceX;
    public float ObservedX;
    public float CurrentIdealX;
    public float IdealTargetY;
    public float SourceY;
    public float ObservedY;
    public float CurrentIdealY;
    public PoseBracketStatus Status;
    public bool HeadApplied;
    public float HeadVerticalVelocity;
    public bool JumpProtectionActive;
    public bool ForwardCorrectionSuppressed;
    public long TimelineCorrectionTicks;
  }

  public bool IsActive => _active;
  public bool IsComplete => _active &&
      Stopwatch.GetTimestamp() - _startTimestamp >=
      TestSeconds * Stopwatch.Frequency;

  public void Start() {
    _startTimestamp = Stopwatch.GetTimestamp();
    _count = 0;
    _hitchInjected = false;
    _active = true;
    MelonLogger.Msg(
        "Fixed-pose smoothness test started: one second stationary, " +
        "three seconds moving at 1 m/s, one second stationary."
    );
  }

  public void Update() {
    if (!_active || _hitchInjected)
      return;
    var elapsed = SecondsSinceStart(Stopwatch.GetTimestamp());
    if (elapsed < 2.25)
      return;
    _hitchInjected = true;
    MelonLogger.Msg(
        "Fixed-pose smoothness test injecting a 60 ms render-thread hitch."
    );
    Thread.Sleep(60);
  }

  public TrackedPoseValue PoseAt(long timestamp) => new TrackedPoseValue {
    IsValid = true,
    IsConnected = true,
    TrackingResult = 200,
    X = PositionAt(timestamp),
    Y = 1.7f + JumpHeightAt(timestamp),
    Z = 0f,
    Qw = 1f,
  };

  public void Record(
      int unityFrame, int tickInFrame, float fixedTime,
      long targetTimestamp, PoseBracketStatus status,
      TrackedPoseValue source, bool headApplied, Vector3 observedPosition,
      float headVerticalVelocity, bool jumpProtectionActive,
      bool forwardCorrectionSuppressed, long timelineCorrectionTicks
  ) {
    if (!_active || _count >= _rows.Length)
      return;
    var now = Stopwatch.GetTimestamp();
    _rows[_count++] = new Row {
      UnityFrame = unityFrame,
      TickInFrame = tickInFrame,
      FixedTime = fixedTime,
      RecordedTimestamp = now,
      TargetTimestamp = targetTimestamp,
      IdealTargetX = PositionAt(targetTimestamp),
      SourceX = source.X,
      ObservedX = observedPosition.x,
      CurrentIdealX = PositionAt(now),
      IdealTargetY = 1.7f + JumpHeightAt(targetTimestamp),
      SourceY = source.Y,
      ObservedY = observedPosition.y,
      CurrentIdealY = 1.7f + JumpHeightAt(now),
      Status = status,
      HeadApplied = headApplied,
      HeadVerticalVelocity = headVerticalVelocity,
      JumpProtectionActive = jumpProtectionActive,
      ForwardCorrectionSuppressed = forwardCorrectionSuppressed,
      TimelineCorrectionTicks = timelineCorrectionTicks,
    };
  }

  public string CompleteAndWrite() {
    _active = false;
    var directory = Path.Combine(
        MelonUtils.UserDataDirectory, BuildInfo.NAME, "smoothness-tests",
        DateTime.Now.ToString(
            "yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture
        )
    );
    Directory.CreateDirectory(directory);
    var metrics = CalculateMetrics();
    WriteCsv(Path.Combine(directory, "fixed-hmd-motion.csv"));
    File.WriteAllText(
        Path.Combine(directory, "summary.txt"), MetricsText(metrics)
    );
    File.WriteAllText(
        Path.Combine(directory, "plot.html"), BuildHtml(metrics)
    );
    MelonLogger.Msg(
        "Fixed-pose smoothness test " + (metrics.Passed ? "PASSED" : "FAILED") +
        ": " + directory
    );
    MelonLogger.Msg(MetricsText(metrics).Replace(Environment.NewLine, "; "));
    return directory;
  }

  private float PositionAt(long timestamp) {
    if (!_active || _startTimestamp == 0)
      return 0f;
    var elapsed = SecondsSinceStart(timestamp);
    if (elapsed <= StationaryBeforeSeconds)
      return 0f;
    return (float)Math.Min(
        MovementSeconds,
        Math.Max(0.0, elapsed - StationaryBeforeSeconds)
    ) * SpeedMetresPerSecond;
  }

  private float JumpHeightAt(long timestamp) {
    var elapsed = SecondsSinceStart(timestamp);
    if (elapsed <= 1.5)
      return 0f;
    if (elapsed <= 2.5)
      return (float)(elapsed - 1.5);
    if (elapsed <= 3.5)
      return (float)(3.5 - elapsed);
    return 0f;
  }

  private double SecondsSinceStart(long timestamp) =>
      (timestamp - _startTimestamp) / (double)Stopwatch.Frequency;

  private Metrics CalculateMetrics() {
    var applied = 0;
    var movementPairs = 0;
    var catchUpFrames = 0;
    var maximumTicksInFrame = 0;
    var monotonicViolations = 0;
    var interpolationErrorMax = 0.0;
    var applicationErrorMax = 0.0;
    var velocitySquaredError = 0.0;
    var velocityErrorMax = 0.0;
    var lagValues = new double[_count];
    var lagCount = 0;
    var jumpProtectedTicks = 0;
    var suppressedCorrectionTicks = 0;
    var resumedCorrectionTicks = 0;
    var protectedRiseVelocityPairs = 0;
    var protectedRiseVelocityMaxError = 0.0;
    var currentFrame = -1;
    var ticksInFrame = 0;
    Row? previousMovement = null;
    Row? previousProtectedRise = null;
    for (var i = 0; i < _count; i++) {
      var row = _rows[i];
      if (row.UnityFrame != currentFrame) {
        if (ticksInFrame > 1)
          catchUpFrames++;
        maximumTicksInFrame = Math.Max(maximumTicksInFrame, ticksInFrame);
        currentFrame = row.UnityFrame;
        ticksInFrame = 0;
      }
      ticksInFrame++;
      if (!row.HeadApplied)
        continue;
      applied++;
      if (row.JumpProtectionActive)
        jumpProtectedTicks++;
      if (row.ForwardCorrectionSuppressed)
        suppressedCorrectionTicks++;
      applicationErrorMax = Math.Max(
          applicationErrorMax, Math.Abs(row.ObservedX - row.SourceX)
      );
      if (SecondsSinceStart(row.RecordedTimestamp) >= 2.6 &&
          row.TimelineCorrectionTicks > 0)
        resumedCorrectionTicks++;
      var targetTime = SecondsSinceStart(row.TargetTimestamp);
      if (row.JumpProtectionActive && targetTime >= 1.7 &&
          targetTime <= 2.4) {
        if (previousProtectedRise.HasValue) {
          var previous = previousProtectedRise.Value;
          var deltaTime = row.FixedTime - previous.FixedTime;
          if (deltaTime > 0.000001f) {
            var velocity = (row.ObservedY - previous.ObservedY) / deltaTime;
            protectedRiseVelocityMaxError = Math.Max(
                protectedRiseVelocityMaxError, Math.Abs(velocity - 1.0)
            );
            protectedRiseVelocityPairs++;
          }
        }
        previousProtectedRise = row;
      } else {
        previousProtectedRise = null;
      }
      if (row.IdealTargetX >= 0.20f &&
          row.IdealTargetX <= MovementSeconds - 0.20f) {
        interpolationErrorMax = Math.Max(
            interpolationErrorMax,
            Math.Abs(row.SourceX - row.IdealTargetX)
        );
        lagValues[lagCount++] = Math.Abs(
            row.CurrentIdealX - row.ObservedX
        );
        if (previousMovement.HasValue) {
          var previous = previousMovement.Value;
          var deltaTime = row.FixedTime - previous.FixedTime;
          if (deltaTime > 0.000001f) {
            var velocity = (row.ObservedX - previous.ObservedX) / deltaTime;
            var error = Math.Abs(velocity - SpeedMetresPerSecond);
            velocitySquaredError += error * error;
            velocityErrorMax = Math.Max(velocityErrorMax, error);
            movementPairs++;
            if (row.ObservedX + 0.0001f < previous.ObservedX)
              monotonicViolations++;
          }
        }
        previousMovement = row;
      }
    }
    if (ticksInFrame > 1)
      catchUpFrames++;
    maximumTicksInFrame = Math.Max(maximumTicksInFrame, ticksInFrame);
    Array.Sort(lagValues, 0, lagCount);
    var lagP95 = lagCount == 0 ? double.PositiveInfinity :
        lagValues[Math.Min(lagCount - 1, (int)(lagCount * 0.95))];
    var lagMax = lagCount == 0 ? double.PositiveInfinity :
        lagValues[lagCount - 1];
    var velocityRms = movementPairs == 0 ? double.PositiveInfinity :
        Math.Sqrt(velocitySquaredError / movementPairs);
    var appliedRate = _count == 0 ? 0.0 : applied / (double)_count;
    var passed = _count >= 200 && appliedRate >= 0.99 &&
        interpolationErrorMax <= 0.0005 &&
        applicationErrorMax <= 0.0005 && velocityRms <= 0.15 &&
        velocityErrorMax <= 0.30 && lagP95 <= 0.10 &&
        lagMax <= 0.16 && monotonicViolations == 0 &&
        maximumTicksInFrame >= 2 && suppressedCorrectionTicks >= 1 &&
        resumedCorrectionTicks >= 1 && protectedRiseVelocityPairs >= 20 &&
        protectedRiseVelocityMaxError <= 0.02;
    return new Metrics {
      Passed = passed,
      Rows = _count,
      Applied = applied,
      AppliedRate = appliedRate,
      MovementPairs = movementPairs,
      CatchUpFrames = catchUpFrames,
      MaximumTicksInFrame = maximumTicksInFrame,
      MonotonicViolations = monotonicViolations,
      InterpolationErrorMax = interpolationErrorMax,
      ApplicationErrorMax = applicationErrorMax,
      VelocityRmsError = velocityRms,
      VelocityMaxError = velocityErrorMax,
      LagP95 = lagP95,
      LagMax = lagMax,
      JumpProtectedTicks = jumpProtectedTicks,
      SuppressedCorrectionTicks = suppressedCorrectionTicks,
      ResumedCorrectionTicks = resumedCorrectionTicks,
      ProtectedRiseVelocityPairs = protectedRiseVelocityPairs,
      ProtectedRiseVelocityMaxError = protectedRiseVelocityMaxError,
    };
  }

  private struct Metrics {
    public bool Passed;
    public int Rows;
    public int Applied;
    public double AppliedRate;
    public int MovementPairs;
    public int CatchUpFrames;
    public int MaximumTicksInFrame;
    public int MonotonicViolations;
    public double InterpolationErrorMax;
    public double ApplicationErrorMax;
    public double VelocityRmsError;
    public double VelocityMaxError;
    public double LagP95;
    public double LagMax;
    public int JumpProtectedTicks;
    public int SuppressedCorrectionTicks;
    public int ResumedCorrectionTicks;
    public int ProtectedRiseVelocityPairs;
    public double ProtectedRiseVelocityMaxError;
  }

  private static string MetricsText(Metrics metrics) =>
      "result=" + (metrics.Passed ? "PASS" : "FAIL") + Environment.NewLine +
      "fixed_ticks=" + metrics.Rows + Environment.NewLine +
      "head_applied_ticks=" + metrics.Applied + Environment.NewLine +
      "head_applied_rate=" + F(metrics.AppliedRate) + Environment.NewLine +
      "movement_velocity_pairs=" + metrics.MovementPairs +
      Environment.NewLine +
      "catch_up_render_frames=" + metrics.CatchUpFrames +
      Environment.NewLine +
      "maximum_ticks_in_render_frame=" + metrics.MaximumTicksInFrame +
      Environment.NewLine +
      "monotonic_violations=" + metrics.MonotonicViolations +
      Environment.NewLine +
      "maximum_interpolation_error_mm=" +
      F(metrics.InterpolationErrorMax * 1000.0) + Environment.NewLine +
      "maximum_application_error_mm=" +
      F(metrics.ApplicationErrorMax * 1000.0) + Environment.NewLine +
      "velocity_rms_error_mps=" + F(metrics.VelocityRmsError) +
      Environment.NewLine +
      "velocity_maximum_error_mps=" + F(metrics.VelocityMaxError) +
      Environment.NewLine +
      "position_lag_p95_mm=" + F(metrics.LagP95 * 1000.0) +
      Environment.NewLine +
      "position_lag_maximum_mm=" + F(metrics.LagMax * 1000.0) +
      Environment.NewLine +
      "jump_protected_ticks=" + metrics.JumpProtectedTicks +
      Environment.NewLine +
      "forward_correction_suppressed_ticks=" +
      metrics.SuppressedCorrectionTicks +
      Environment.NewLine +
      "post_rise_correction_ticks=" + metrics.ResumedCorrectionTicks +
      Environment.NewLine +
      "protected_rise_velocity_pairs=" +
      metrics.ProtectedRiseVelocityPairs + Environment.NewLine +
      "protected_rise_velocity_maximum_error_mps=" +
      F(metrics.ProtectedRiseVelocityMaxError) +
      Environment.NewLine;

  private void WriteCsv(string path) {
    using var writer = new StreamWriter(path, false);
    writer.WriteLine(
        "sample,unity_frame,tick_in_render_frame,fixed_time_s," +
        "target_time_s,recorded_time_s,ideal_target_x,source_x," +
        "observed_x,current_ideal_x,interpolation_error_mm," +
        "application_error_mm,position_lag_mm,status,head_applied," +
        "head_vertical_velocity_mps,jump_protection_active," +
        "forward_correction_suppressed,timeline_correction_us"
        + ",ideal_target_y,source_y,observed_y,current_ideal_y"
    );
    for (var i = 0; i < _count; i++) {
      var row = _rows[i];
      writer.Write(i);
      writer.Write(','); writer.Write(row.UnityFrame);
      writer.Write(','); writer.Write(row.TickInFrame);
      W(writer, row.FixedTime);
      W(writer, SecondsSinceStart(row.TargetTimestamp));
      W(writer, SecondsSinceStart(row.RecordedTimestamp));
      W(writer, row.IdealTargetX);
      W(writer, row.SourceX);
      W(writer, row.ObservedX);
      W(writer, row.CurrentIdealX);
      W(writer, (row.SourceX - row.IdealTargetX) * 1000.0);
      W(writer, (row.ObservedX - row.SourceX) * 1000.0);
      W(writer, (row.CurrentIdealX - row.ObservedX) * 1000.0);
      writer.Write(','); writer.Write(row.Status);
      writer.Write(','); writer.Write(row.HeadApplied ? 1 : 0);
      W(writer, row.HeadVerticalVelocity);
      writer.Write(','); writer.Write(row.JumpProtectionActive ? 1 : 0);
      writer.Write(','); writer.Write(
          row.ForwardCorrectionSuppressed ? 1 : 0
      );
      W(writer, row.TimelineCorrectionTicks /
                (Stopwatch.Frequency / 1000000.0));
      W(writer, row.IdealTargetY);
      W(writer, row.SourceY);
      W(writer, row.ObservedY);
      W(writer, row.CurrentIdealY);
      writer.WriteLine();
    }
  }

  private string BuildHtml(Metrics metrics) {
    const double width = 1200.0;
    const double height = 500.0;
    const double left = 70.0;
    const double top = 25.0;
    const double plotWidth = 1100.0;
    const double plotHeight = 420.0;
    var ideal = new StringBuilder();
    var observed = new StringBuilder();
    var current = new StringBuilder();
    var idealY = new StringBuilder();
    var observedY = new StringBuilder();
    var currentY = new StringBuilder();
    for (var i = 0; i < _count; i++) {
      var row = _rows[i];
      var time = Math.Max(0.0, Math.Min(
          TestSeconds, SecondsSinceStart(row.RecordedTimestamp)
      ));
      AppendPoint(ideal, left + time / TestSeconds * plotWidth,
                  top + (3.2 - row.IdealTargetX) / 3.2 * plotHeight);
      AppendPoint(observed, left + time / TestSeconds * plotWidth,
                  top + (3.2 - row.ObservedX) / 3.2 * plotHeight);
      AppendPoint(current, left + time / TestSeconds * plotWidth,
                  top + (3.2 - row.CurrentIdealX) / 3.2 * plotHeight);
      AppendPoint(idealY, left + time / TestSeconds * plotWidth,
                  top + (2.8 - row.IdealTargetY) / 1.2 * plotHeight);
      AppendPoint(observedY, left + time / TestSeconds * plotWidth,
                  top + (2.8 - row.ObservedY) / 1.2 * plotHeight);
      AppendPoint(currentY, left + time / TestSeconds * plotWidth,
                  top + (2.8 - row.CurrentIdealY) / 1.2 * plotHeight);
    }
    return "<!doctype html><meta charset=\"utf-8\"><title>Fixed HMD " +
        "smoothness test</title><style>body{font:15px system-ui;background:#111;" +
        "color:#ddd;margin:24px}h1{margin-bottom:8px}.pass{color:#63d98b}" +
        ".fail{color:#ff6b6b}.cards{display:flex;gap:12px;flex-wrap:wrap}" +
        ".card{background:#1d1d1d;padding:10px 14px;border-radius:7px}" +
        "svg{max-width:100%;background:#181818;border:1px solid #444}" +
        ".axis{stroke:#777}.grid{stroke:#333}.ideal{stroke:#67a9ff}" +
        ".observed{stroke:#ffae57}.current{stroke:#8b8b8b}</style>" +
        "<h1 class=\"" +
        (metrics.Passed ? "pass\">PASS" : "fail\">FAIL") +
        "</h1><p>Mock HMD moves sideways at 1 m/s. A 60 ms hitch is " +
        "injected while the mock headset is rising at 1 m/s.</p>" +
        "<div class=\"cards\">" +
        Card("Fixed ticks", metrics.Rows.ToString()) +
        Card("Max ticks/frame", metrics.MaximumTicksInFrame.ToString()) +
        Card("Velocity RMS error", F(metrics.VelocityRmsError) + " m/s") +
        Card("p95 position lag", F(metrics.LagP95 * 1000) + " mm") +
        Card("Max position lag", F(metrics.LagMax * 1000) + " mm") +
        Card("Suppressed corrections",
             metrics.SuppressedCorrectionTicks.ToString()) +
        Card("Post-rise corrections",
             metrics.ResumedCorrectionTicks.ToString()) +
        Card("Protected rise max velocity error",
             F(metrics.ProtectedRiseVelocityMaxError) + " m/s") +
        "</div><p><span style=\"color:#67a9ff\">ideal target</span> · " +
        "<span style=\"color:#ffae57\">FixedUpdate observed</span> · " +
        "<span style=\"color:#8b8b8b\">current wall-time pose</span></p>" +
        "<svg viewBox=\"0 0 " + width + " " + height + "\">" +
        "<path class=\"grid\" d=\"M70 25V445M290 25V445M510 25V445" +
        "M730 25V445M950 25V445M1170 25V445\"/>" +
        "<path class=\"axis\" d=\"M70 445H1170M70 25V445\"/>" +
        Polyline("current", current) + Polyline("ideal", ideal) +
        Polyline("observed", observed) +
        "<g fill=\"#aaa\"><text x=\"65\" y=\"470\">0 s</text>" +
        "<text x=\"1145\" y=\"470\">5 s</text>" +
        "<text x=\"25\" y=\"445\">0 m</text>" +
        "<text x=\"25\" y=\"35\">3.2 m</text></g></svg>" +
        "<h2>Vertical jump pose</h2><svg viewBox=\"0 0 " + width +
        " " + height + "\"><path class=\"grid\" d=\"M70 25V445" +
        "M290 25V445M510 25V445M730 25V445M950 25V445" +
        "M1170 25V445\"/><path class=\"axis\" d=\"M70 445H1170" +
        "M70 25V445\"/>" + Polyline("current", currentY) +
        Polyline("ideal", idealY) + Polyline("observed", observedY) +
        "<g fill=\"#aaa\"><text x=\"65\" y=\"470\">0 s</text>" +
        "<text x=\"1145\" y=\"470\">5 s</text>" +
        "<text x=\"25\" y=\"445\">1.6 m</text>" +
        "<text x=\"25\" y=\"35\">2.8 m</text></g></svg>" +
        "<pre>" + Escape(MetricsText(metrics)) + "</pre>";
  }

  private static string Card(string name, string value) =>
      "<div class=\"card\"><div>" + Escape(name) + "</div><strong>" +
      Escape(value) + "</strong></div>";

  private static string Polyline(string css, StringBuilder points) =>
      "<polyline class=\"" + css + "\" fill=\"none\" stroke-width=\"2\" " +
      "points=\"" + points + "\"/>";

  private static void AppendPoint(StringBuilder result, double x, double y) {
    if (result.Length > 0)
      result.Append(' ');
    result.Append(F(x)).Append(',').Append(F(y));
  }

  private static string Escape(string value) => value.Replace("&", "&amp;")
      .Replace("<", "&lt;").Replace(">", "&gt;");

  private static string F(double value) =>
      value.ToString("0.######", CultureInfo.InvariantCulture);

  private static void W(TextWriter writer, double value) {
    writer.Write(',');
    writer.Write(F(value));
  }
}
#endif
