#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using MelonLoader;
using StressLevelZero.Rig;
using UnityEngine;
using Valve.VR;

namespace Sst.BoneworksPerformance;

internal sealed class SuperJumpDiagnostics {
  private const int FixedPreludeCapacity = 300;
  private const int RenderPreludeCapacity = 180;
  private const double CompletionSeconds = 2.5;
  private static SuperJumpDiagnostics _instance;

  private readonly bool _enabled;
  private string _directory;
  private readonly Queue<FixedRow> _fixedPrelude =
      new Queue<FixedRow>(FixedPreludeCapacity);
  private readonly Queue<RenderRow> _renderPrelude =
      new Queue<RenderRow>(RenderPreludeCapacity);
  private readonly List<Attempt> _completed = new List<Attempt>();
  private Attempt _active;
  private int _nextAttemptId = 1;
  private string _sceneName = "unknown";
  private bool _lastButtonHeld;
  private bool _haveButtonState;
  private RigManager _rigManager;
  private float _nextRigSearchAt;
  private bool _recording;

  public bool IsEnabled => _enabled;
  public bool IsRecording => _recording;

  private sealed class Attempt {
    public int Id;
    public string Scene;
    public long PressObservedTimestamp;
    public long PressHardwareTimestamp;
    public long ReleaseObservedTimestamp;
    public long ReleaseHardwareTimestamp;
    public long JumpCallTimestamp;
    public long ActualJumpPoseTimestamp;
    public long ActualJumpSimulationTimestamp;
    public bool ActualJumpTickFound;
    public bool CompletedNormally;
    public readonly List<FixedRow> Fixed = new List<FixedRow>(512);
    public readonly List<RenderRow> Render = new List<RenderRow>(256);
  }

  private struct FixedRow {
    public int AttemptId;
    public int UnityFrame;
    public int TickInFrame;
    public float FixedTime;
    public long ObservedTimestamp;
    public long SimulationTimestamp;
    public long PoseTargetTimestamp;
    public bool PoseAvailable;
    public float HistoricalHeadY;
    public float AppliedHeadY;
    public float LatestHeadY;
    public float HmdWorldY;
    public float PhysicsHeadY;
    public float PelvisY;
    public float FeetY;
    public float PelvisVelocityY;
    public bool ButtonHeld;
    public bool ChargeInput;
    public bool Jumping;
    public int JumpStage;
    public float JumpCharge;
    public float FeetOffset;
  }

  private struct RenderRow {
    public int AttemptId;
    public int UnityFrame;
    public long Timestamp;
    public float Realtime;
    public bool ButtonHeld;
    public bool ButtonDown;
    public bool ButtonUp;
    public float SteamVrChangedTime;
    public long SteamVrChangedTimestamp;
    public float HmdWorldY;
    public float PhysicsHeadY;
    public float PelvisY;
    public float FeetY;
  }

  public SuperJumpDiagnostics(bool enabled) {
    _enabled = enabled;
    if (!_enabled)
      return;
    _instance = this;
    MelonLogger.Msg("Super-jump recorder ready. Press the right stick to start.");
  }

  public void BeginRecording() {
    if (!_enabled || _recording)
      return;
    _directory = Path.Combine(
        MelonUtils.UserDataDirectory, BuildInfo.NAME, "jump-diagnostics",
        DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)
    );
    Directory.CreateDirectory(_directory);
    _completed.Clear();
    _fixedPrelude.Clear();
    _renderPrelude.Clear();
    _active = null;
    _nextAttemptId = 1;
    _haveButtonState = false;
    _recording = true;
    MelonLogger.Msg("Super-jump recording started: " + _directory + ".");
  }

  public void EndRecording() {
    if (!_recording)
      return;
    CompleteActive(false);
    WriteResults();
    _recording = false;
    MelonLogger.Msg("Super-jump recording stopped: " + _directory + ".");
  }

  public void ResetScene(string sceneName) {
    if (!_enabled)
      return;
    if (_recording)
      CompleteActive(false);
    _sceneName = string.IsNullOrEmpty(sceneName) ? "unknown" : sceneName;
    _fixedPrelude.Clear();
    _renderPrelude.Clear();
    _haveButtonState = false;
    _rigManager = null;
    _nextRigSearchAt = 0f;
  }

  public void ObserveRenderedFrame() {
    if (!_recording)
      return;
    var rigManager = AcquireRigManager();
    var rig = rigManager && rigManager.ControllerRig
        ? rigManager.ControllerRig
        : null;
    if (!rig)
      return;
    var controller = JumpController(rig);
    if (!controller)
      return;

    var now = Stopwatch.GetTimestamp();
    var realtime = Time.realtimeSinceStartup;
    var held = controller.GetAButton();
    var down = controller.GetAButtonDown();
    var up = controller.GetAButtonUp();
    var changedTime = ReadChangedTime(rig);
    var changedTimestamp = UnityRealtimeToStopwatch(
        changedTime, realtime, now
    );
    var row = ReadRenderRow(
        rigManager, rig, now, realtime, held, down, up,
        changedTime, changedTimestamp
    );
    AddPrelude(_renderPrelude, row, RenderPreludeCapacity);

    if (!_haveButtonState) {
      _lastButtonHeld = held;
      _haveButtonState = true;
    }
    if (down || held && !_lastButtonHeld)
      StartAttempt(now, changedTimestamp);
    if (_active != null)
      if (_active.Render.Count == 0 ||
          _active.Render[_active.Render.Count - 1].Timestamp != row.Timestamp)
        _active.Render.Add(WithAttempt(row, _active.Id));
    _lastButtonHeld = held;

    if (_active != null && _active.ReleaseObservedTimestamp != 0 &&
        Seconds(now - _active.ReleaseObservedTimestamp) >= CompletionSeconds)
      CompleteActive(true);
  }

  public void RecordFixedTick(
      ControllerRig rig, int unityFrame, int tickInFrame, float fixedTime,
      long simulationTimestamp, long targetTimestamp, bool poseAvailable,
      TrackedPoseValue historicalHead, Vector3 appliedHead,
      TrackedPoseValue latestHead
  ) {
    if (!_recording || !rig)
      return;
    var manager = rig.manager;
    var physics = manager && manager.physicsRig ? manager.physicsRig : null;
    var body = physics && physics.physBody ? physics.physBody : null;
    var controller = JumpController(rig);
    var row = new FixedRow {
      UnityFrame = unityFrame,
      TickInFrame = tickInFrame,
      FixedTime = fixedTime,
      ObservedTimestamp = Stopwatch.GetTimestamp(),
      SimulationTimestamp = simulationTimestamp,
      PoseTargetTimestamp = targetTimestamp,
      PoseAvailable = poseAvailable,
      HistoricalHeadY = historicalHead.IsValid ? historicalHead.Y : float.NaN,
      AppliedHeadY = poseAvailable ? appliedHead.y : float.NaN,
      LatestHeadY = latestHead.IsValid ? latestHead.Y : float.NaN,
      HmdWorldY = rig.hmdTransform ? rig.hmdTransform.position.y : float.NaN,
      PhysicsHeadY = physics && physics.m_head
          ? physics.m_head.position.y : float.NaN,
      PelvisY = body && body.rbPelvis
          ? body.rbPelvis.position.y : float.NaN,
      FeetY = body && body.rbFeet ? body.rbFeet.position.y : float.NaN,
      PelvisVelocityY = body && body.rbPelvis
          ? body.rbPelvis.velocity.y : float.NaN,
      ButtonHeld = controller && controller.GetAButton(),
      ChargeInput = rig._chargeInput,
      Jumping = rig._jumping,
      JumpStage = rig._jumpStage,
      JumpCharge = rig._jumpCharge,
      FeetOffset = rig.feetOffset,
    };
    AddPrelude(_fixedPrelude, row, FixedPreludeCapacity);
    if (_active == null)
      return;
    row.AttemptId = _active.Id;
    _active.Fixed.Add(row);
    if (!_active.ActualJumpTickFound && _active.JumpCallTimestamp != 0 &&
        row.Jumping) {
      _active.ActualJumpTickFound = true;
      _active.ActualJumpPoseTimestamp = targetTimestamp;
      _active.ActualJumpSimulationTimestamp = simulationTimestamp;
    }
  }

  public void Shutdown() {
    if (!_enabled)
      return;
    EndRecording();
    if (ReferenceEquals(_instance, this))
      _instance = null;
  }

  private void OnJump(ControllerRig rig) {
    if (!_recording || !rig)
      return;
    var now = Stopwatch.GetTimestamp();
    var realtime = Time.realtimeSinceStartup;
    var changedTime = ReadChangedTime(rig);
    var controller = JumpController(rig);
    var hardwareTimestamp = controller && controller.GetAButtonUp()
        ? UnityRealtimeToStopwatch(changedTime, realtime, now)
        : 0;
    if (_active == null)
      StartAttempt(now, 0);
    _active.ReleaseObservedTimestamp = now;
    _active.ReleaseHardwareTimestamp = hardwareTimestamp;
    _active.JumpCallTimestamp = now;
  }

  private void StartAttempt(long observedTimestamp, long hardwareTimestamp) {
    if (_active != null) {
      CompleteActive(_active.ReleaseObservedTimestamp != 0);
    }
    _active = new Attempt {
      Id = _nextAttemptId++,
      Scene = _sceneName,
      PressObservedTimestamp = observedTimestamp,
      PressHardwareTimestamp = hardwareTimestamp,
    };
    foreach (var row in _fixedPrelude)
      _active.Fixed.Add(WithAttempt(row, _active.Id));
    foreach (var row in _renderPrelude)
      _active.Render.Add(WithAttempt(row, _active.Id));
  }

  private void CompleteActive(bool completedNormally) {
    if (_active == null)
      return;
    _active.CompletedNormally = completedNormally;
    _completed.Add(_active);
    MelonLogger.Msg(
        "Recorded super-jump attempt " + _active.Id + " with " +
        _active.Fixed.Count + " fixed ticks."
    );
    _active = null;
    WriteResults();
  }

  private void WriteResults() {
    if (!_enabled || string.IsNullOrEmpty(_directory))
      return;
    try {
      WriteFixedCsv();
      WriteRenderCsv();
      WriteSummaryCsv();
      WriteHtml();
    } catch (Exception exception) {
      MelonLogger.Warning(
          "Could not write super-jump diagnostics: " + exception.Message
      );
    }
  }

  private void WriteFixedCsv() {
    using var writer = new StreamWriter(Path.Combine(
        _directory, "fixed-ticks.csv"
    ));
    writer.WriteLine(
        "attempt,unity_frame,tick_in_frame,fixed_time_s,observed_us," +
        "simulation_us,pose_target_us,pose_available,historical_head_y," +
        "applied_head_y,latest_head_y,hmd_world_y,physics_head_y,pelvis_y," +
        "feet_y,pelvis_velocity_y,button_held,charge_input,jumping," +
        "jump_stage,jump_charge,feet_offset"
    );
    foreach (var attempt in _completed)
      foreach (var row in attempt.Fixed)
        WriteFixed(writer, row);
  }

  private void WriteRenderCsv() {
    using var writer = new StreamWriter(Path.Combine(
        _directory, "rendered-frames.csv"
    ));
    writer.WriteLine(
        "attempt,unity_frame,timestamp_us,realtime_s,button_held," +
        "button_down,button_up,steamvr_changed_time_s," +
        "steamvr_changed_timestamp_us,hmd_world_y,physics_head_y," +
        "pelvis_y,feet_y"
    );
    foreach (var attempt in _completed)
      foreach (var row in attempt.Render)
        WriteRender(writer, row);
  }

  private void WriteSummaryCsv() {
    using var writer = new StreamWriter(Path.Combine(
        _directory, "jumps.csv"
    ));
    writer.WriteLine(
        "attempt,scene,complete,press_observed_us,press_hardware_us," +
        "release_observed_us,release_hardware_us,jump_call_us," +
        "release_observation_latency_ms,physical_head_rise_m," +
        "player_pelvis_rise_m,release_to_head_apex_ms," +
        "expected_pose_aligned_tick_us,expected_simulation_tick_us," +
        "actual_jump_pose_tick_us,actual_jump_simulation_tick_us," +
        "pose_aligned_error_ms,simulation_error_ms"
    );
    foreach (var attempt in _completed)
      WriteSummary(writer, attempt);
  }

  private void WriteHtml() {
    var html = new StringBuilder(8192);
    html.Append("<!doctype html><meta charset=\"utf-8\"><title>BONEWORKS jump timing</title>");
    html.Append("<style>body{font:14px system-ui;background:#111;color:#ddd;margin:24px}a{color:#8cf}table{border-collapse:collapse;margin:12px 0 28px}th,td{padding:6px 9px;border:1px solid #444;text-align:right}th:first-child,td:first-child{text-align:left}h2{margin-top:32px}.bad{color:#ff8a8a}.good{color:#91e6a7}svg{background:#181818;border:1px solid #444;max-width:1100px;width:100%;height:260px}.raw{stroke:#6cf}.applied{stroke:#fc6}.pelvis{stroke:#9e7}.release{stroke:#f66;stroke-dasharray:5 4}.axis{stroke:#555}.legend{color:#aaa}</style>");
    html.Append("<h1>BONEWORKS super-jump timing</h1><p>");
    html.Append("The blue line is the timestamped OpenVR head pose selected for each physics tick. Orange is the pose applied to BONEWORKS, and green is the pelvis. The red line is the hardware-timestamped A-button release. Times are also available in <a href=\"jumps.csv\">jumps.csv</a>, <a href=\"fixed-ticks.csv\">fixed-ticks.csv</a>, and <a href=\"rendered-frames.csv\">rendered-frames.csv</a>.</p>");
    html.Append("<table><thead><tr><th>Jump</th><th>Scene</th><th>Head rise</th><th>Pelvis rise</th><th>Release to head apex</th><th>Game observed edge late</th><th>Pose-clock jump error</th></tr></thead><tbody>");
    foreach (var attempt in _completed) {
      var summary = Summarize(attempt);
      html.Append("<tr><td>").Append(attempt.Id).Append("</td><td>")
          .Append(Escape(attempt.Scene)).Append("</td><td>")
          .Append(F(summary.HeadRise)).Append(" m</td><td>")
          .Append(F(summary.PelvisRise)).Append(" m</td><td>")
          .Append(F(summary.ReleaseToApexMs)).Append(" ms</td><td>")
          .Append(F(summary.ObservationLatencyMs)).Append(" ms</td><td>")
          .Append(F(summary.PoseAlignedErrorMs)).Append(" ms</td></tr>");
    }
    html.Append("</tbody></table>");
    foreach (var attempt in _completed) {
      html.Append("<h2>Jump ").Append(attempt.Id).Append("</h2>");
      AppendPlot(html, attempt);
    }
    File.WriteAllText(Path.Combine(_directory, "report.html"), html.ToString());
  }

  private static void AppendPlot(StringBuilder html, Attempt attempt) {
    var rows = attempt.Fixed.Where(row => row.PoseAvailable).ToArray();
    if (rows.Length < 2) {
      html.Append("<p>No usable fixed-pose rows.</p>");
      return;
    }
    var start = rows[0].PoseTargetTimestamp;
    var end = rows[rows.Length - 1].PoseTargetTimestamp;
    var minimum = rows.Min(row => MinFinite(
        row.HistoricalHeadY, row.AppliedHeadY, row.PelvisY
    ));
    var maximum = rows.Max(row => MaxFinite(
        row.HistoricalHeadY, row.AppliedHeadY, row.PelvisY
    ));
    if (maximum - minimum < 0.01f)
      maximum = minimum + 0.01f;
    html.Append("<svg viewBox=\"0 0 1000 260\">");
    html.Append("<line class=\"axis\" x1=\"40\" y1=\"230\" x2=\"980\" y2=\"230\"/>");
    AppendPolyline(html, rows, start, end, minimum, maximum, "raw",
                   row => row.HistoricalHeadY);
    AppendPolyline(html, rows, start, end, minimum, maximum, "applied",
                   row => row.AppliedHeadY);
    AppendPolyline(html, rows, start, end, minimum, maximum, "pelvis",
                   row => row.PelvisY);
    if (attempt.ReleaseHardwareTimestamp != 0) {
      var x = X(attempt.ReleaseHardwareTimestamp, start, end);
      html.Append("<line class=\"release\" x1=\"").Append(F(x))
          .Append("\" y1=\"15\" x2=\"").Append(F(x))
          .Append("\" y2=\"230\"/>");
    }
    html.Append("</svg><p class=\"legend\">Blue: historical head. Orange: applied head. Green: pelvis. Red: A-button release.</p>");
  }

  private static void AppendPolyline(
      StringBuilder html, FixedRow[] rows, long start, long end,
      float minimum, float maximum, string css,
      Func<FixedRow, float> selector
  ) {
    html.Append("<polyline class=\"").Append(css)
        .Append("\" fill=\"none\" stroke-width=\"2\" points=\"");
    foreach (var row in rows) {
      var value = selector(row);
      if (!Finite(value))
        continue;
      html.Append(F(X(row.PoseTargetTimestamp, start, end))).Append(',')
          .Append(F(230f - (value - minimum) / (maximum - minimum) * 210f))
          .Append(' ');
    }
    html.Append("\"/>");
  }

  private struct Summary {
    public double ObservationLatencyMs;
    public float HeadRise;
    public float PelvisRise;
    public double ReleaseToApexMs;
    public long ExpectedPoseTick;
    public long ExpectedSimulationTick;
    public double PoseAlignedErrorMs;
    public double SimulationErrorMs;
  }

  private static Summary Summarize(Attempt attempt) {
    var rows = attempt.Fixed.Where(row => row.PoseAvailable).ToArray();
    var headRows = rows.Where(row => Finite(row.HistoricalHeadY)).ToArray();
    var pelvisRows = rows.Where(row => Finite(row.PelvisY)).ToArray();
    var headMinimum = headRows.Length == 0 ? float.NaN :
        headRows.Min(row => row.HistoricalHeadY);
    var apex = headRows.Length == 0 ? new FixedRow() :
        headRows.OrderByDescending(row => row.HistoricalHeadY).First();
    var pelvisBaselineRows = pelvisRows
        .Where(row => attempt.ReleaseHardwareTimestamp == 0 ||
                      row.PoseTargetTimestamp <=
                      attempt.ReleaseHardwareTimestamp)
        .ToArray();
    var pelvisBaseline = pelvisRows.Length == 0 ? float.NaN :
        pelvisBaselineRows.Length > 0
            ? pelvisBaselineRows[pelvisBaselineRows.Length - 1].PelvisY
            : pelvisRows[0].PelvisY;
    var expectedPose = FirstTimestampAtOrAfter(
        rows, attempt.ReleaseHardwareTimestamp,
        row => row.PoseTargetTimestamp
    );
    var expectedSimulation = FirstTimestampAtOrAfter(
        rows, attempt.ReleaseHardwareTimestamp,
        row => row.SimulationTimestamp
    );
    return new Summary {
      ObservationLatencyMs = Milliseconds(
          attempt.ReleaseObservedTimestamp - attempt.ReleaseHardwareTimestamp
      ),
      HeadRise = headRows.Length == 0 ? float.NaN :
          apex.HistoricalHeadY - headMinimum,
      PelvisRise = pelvisRows.Length == 0 ? float.NaN :
          pelvisRows.Max(row => row.PelvisY) - pelvisBaseline,
      ReleaseToApexMs = headRows.Length == 0 ? double.NaN :
          Milliseconds(apex.PoseTargetTimestamp -
                       attempt.ReleaseHardwareTimestamp),
      ExpectedPoseTick = expectedPose,
      ExpectedSimulationTick = expectedSimulation,
      PoseAlignedErrorMs = expectedPose == 0 ||
          !attempt.ActualJumpTickFound ? double.NaN : Milliseconds(
              attempt.ActualJumpPoseTimestamp - expectedPose
          ),
      SimulationErrorMs = expectedSimulation == 0 ||
          !attempt.ActualJumpTickFound ? double.NaN : Milliseconds(
              attempt.ActualJumpSimulationTimestamp - expectedSimulation
          ),
    };
  }

  private static long FirstTimestampAtOrAfter(
      FixedRow[] rows, long timestamp, Func<FixedRow, long> selector
  ) {
    if (timestamp == 0)
      return 0;
    foreach (var row in rows) {
      var candidate = selector(row);
      if (candidate >= timestamp)
        return candidate;
    }
    return 0;
  }

  private static void WriteSummary(TextWriter writer, Attempt attempt) {
    var summary = Summarize(attempt);
    writer.Write(attempt.Id); writer.Write(','); Csv(writer, attempt.Scene);
    writer.Write(','); writer.Write(attempt.CompletedNormally ? 1 : 0);
    WriteTimestamp(writer, attempt.PressObservedTimestamp);
    WriteTimestamp(writer, attempt.PressHardwareTimestamp);
    WriteTimestamp(writer, attempt.ReleaseObservedTimestamp);
    WriteTimestamp(writer, attempt.ReleaseHardwareTimestamp);
    WriteTimestamp(writer, attempt.JumpCallTimestamp);
    Number(writer, summary.ObservationLatencyMs);
    Number(writer, summary.HeadRise); Number(writer, summary.PelvisRise);
    Number(writer, summary.ReleaseToApexMs);
    WriteTimestamp(writer, summary.ExpectedPoseTick);
    WriteTimestamp(writer, summary.ExpectedSimulationTick);
    WriteTimestamp(writer, attempt.ActualJumpPoseTimestamp);
    WriteTimestamp(writer, attempt.ActualJumpSimulationTimestamp);
    Number(writer, summary.PoseAlignedErrorMs);
    Number(writer, summary.SimulationErrorMs);
    writer.WriteLine();
  }

  private static void WriteFixed(TextWriter writer, FixedRow row) {
    writer.Write(row.AttemptId); writer.Write(','); writer.Write(row.UnityFrame);
    writer.Write(','); writer.Write(row.TickInFrame); Number(writer, row.FixedTime);
    WriteTimestamp(writer, row.ObservedTimestamp);
    WriteTimestamp(writer, row.SimulationTimestamp);
    WriteTimestamp(writer, row.PoseTargetTimestamp);
    writer.Write(','); writer.Write(row.PoseAvailable ? 1 : 0);
    Number(writer, row.HistoricalHeadY); Number(writer, row.AppliedHeadY);
    Number(writer, row.LatestHeadY); Number(writer, row.HmdWorldY);
    Number(writer, row.PhysicsHeadY); Number(writer, row.PelvisY);
    Number(writer, row.FeetY); Number(writer, row.PelvisVelocityY);
    writer.Write(','); writer.Write(row.ButtonHeld ? 1 : 0);
    writer.Write(','); writer.Write(row.ChargeInput ? 1 : 0);
    writer.Write(','); writer.Write(row.Jumping ? 1 : 0);
    writer.Write(','); writer.Write(row.JumpStage);
    Number(writer, row.JumpCharge); Number(writer, row.FeetOffset);
    writer.WriteLine();
  }

  private static void WriteRender(TextWriter writer, RenderRow row) {
    writer.Write(row.AttemptId); writer.Write(','); writer.Write(row.UnityFrame);
    WriteTimestamp(writer, row.Timestamp); Number(writer, row.Realtime);
    writer.Write(','); writer.Write(row.ButtonHeld ? 1 : 0);
    writer.Write(','); writer.Write(row.ButtonDown ? 1 : 0);
    writer.Write(','); writer.Write(row.ButtonUp ? 1 : 0);
    Number(writer, row.SteamVrChangedTime);
    WriteTimestamp(writer, row.SteamVrChangedTimestamp);
    Number(writer, row.HmdWorldY); Number(writer, row.PhysicsHeadY);
    Number(writer, row.PelvisY); Number(writer, row.FeetY);
    writer.WriteLine();
  }

  private static RenderRow ReadRenderRow(
      RigManager manager, ControllerRig rig, long timestamp, float realtime,
      bool held, bool down, bool up, float changedTime,
      long changedTimestamp
  ) {
    var physics = manager && manager.physicsRig ? manager.physicsRig : null;
    var body = physics && physics.physBody ? physics.physBody : null;
    return new RenderRow {
      UnityFrame = Time.frameCount,
      Timestamp = timestamp,
      Realtime = realtime,
      ButtonHeld = held,
      ButtonDown = down,
      ButtonUp = up,
      SteamVrChangedTime = changedTime,
      SteamVrChangedTimestamp = changedTimestamp,
      HmdWorldY = rig.hmdTransform ? rig.hmdTransform.position.y : float.NaN,
      PhysicsHeadY = physics && physics.m_head
          ? physics.m_head.position.y : float.NaN,
      PelvisY = body && body.rbPelvis ? body.rbPelvis.position.y : float.NaN,
      FeetY = body && body.rbFeet ? body.rbFeet.position.y : float.NaN,
    };
  }

  private static BaseController JumpController(ControllerRig rig) =>
      rig.isRightHanded ? rig.rightController : rig.leftController;

  private RigManager AcquireRigManager() {
    if (_rigManager)
      return _rigManager;
    var now = UnityEngine.Time.realtimeSinceStartup;
    if (now < _nextRigSearchAt)
      return null;
    _nextRigSearchAt = now + 0.5f;
    _rigManager = UnityEngine.Object.FindObjectOfType<RigManager>();
    return _rigManager;
  }

  private static float ReadChangedTime(ControllerRig rig) {
    try {
      var action = SteamVR_Actions.default_AClick;
      if (action == null)
        return 0f;
      return action.GetTimeLastChanged(
          rig.isRightHanded
              ? SteamVR_Input_Sources.RightHand
              : SteamVR_Input_Sources.LeftHand
      );
    } catch {
      return 0f;
    }
  }

  private static long UnityRealtimeToStopwatch(
      float eventRealtime, float observedRealtime, long observedTimestamp
  ) {
    var age = observedRealtime - eventRealtime;
    if (eventRealtime <= 0f || age < -0.1f || age > 10f)
      return 0;
    return observedTimestamp - (long)Math.Round(age * Stopwatch.Frequency);
  }

  private static void AddPrelude<T>(Queue<T> queue, T row, int capacity) {
    while (queue.Count >= capacity)
      queue.Dequeue();
    queue.Enqueue(row);
  }

  private static FixedRow WithAttempt(FixedRow row, int attemptId) {
    row.AttemptId = attemptId;
    return row;
  }

  private static RenderRow WithAttempt(RenderRow row, int attemptId) {
    row.AttemptId = attemptId;
    return row;
  }

  private static double Seconds(long ticks) =>
      ticks / (double)Stopwatch.Frequency;

  private static double Milliseconds(long ticks) =>
      ticks * 1000.0 / Stopwatch.Frequency;

  private static void WriteTimestamp(TextWriter writer, long timestamp) =>
      Number(writer, timestamp == 0 ? double.NaN :
          timestamp * 1000000.0 / Stopwatch.Frequency);

  private static void Number(TextWriter writer, double value) {
    writer.Write(','); writer.Write(F(value));
  }

  private static string F(double value) =>
      double.IsNaN(value) || double.IsInfinity(value) ? "" :
      value.ToString("0.######", CultureInfo.InvariantCulture);

  private static void Csv(TextWriter writer, string value) {
    writer.Write('"'); writer.Write((value ?? "").Replace("\"", "\"\""));
    writer.Write('"');
  }

  private static bool Finite(float value) =>
      !float.IsNaN(value) && !float.IsInfinity(value);

  private static float MinFinite(float a, float b, float c) {
    var result = float.PositiveInfinity;
    if (Finite(a)) result = Math.Min(result, a);
    if (Finite(b)) result = Math.Min(result, b);
    if (Finite(c)) result = Math.Min(result, c);
    return float.IsPositiveInfinity(result) ? 0f : result;
  }

  private static float MaxFinite(float a, float b, float c) {
    var result = float.NegativeInfinity;
    if (Finite(a)) result = Math.Max(result, a);
    if (Finite(b)) result = Math.Max(result, b);
    if (Finite(c)) result = Math.Max(result, c);
    return float.IsNegativeInfinity(result) ? 0f : result;
  }

  private static float X(long timestamp, long start, long end) =>
      40f + (float)((timestamp - start) / (double)Math.Max(1, end - start)) *
      940f;

  private static string Escape(string value) =>
      (value ?? "").Replace("&", "&amp;").Replace("<", "&lt;")
          .Replace(">", "&gt;").Replace("\"", "&quot;");

  [HarmonyPatch(typeof(ControllerRig), nameof(ControllerRig.Jump))]
  private static class ControllerRig_Jump_Patch {
    [HarmonyPrefix]
    private static void Prefix(ControllerRig __instance) =>
        _instance?.OnJump(__instance);
  }
}
#endif
