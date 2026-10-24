using System;
using System.Diagnostics;
using System.Threading;
using HarmonyLib;
using MelonLoader;
using StressLevelZero.Rig;
using UnhollowerBaseLib;
using UnityEngine;
using Valve.VR;
using RigController = StressLevelZero.Rig.Controller;

namespace Sst.BoneworksFrameFix;

internal sealed partial class FixedUpdatePoseHistory
{
  private const int HistoryCapacity = 4096;

  private static FixedUpdatePoseHistory _instance;

  private readonly bool _enabled;
  private readonly int _sampleRate;
  private readonly int _delayTicks;
  private readonly bool _protectRisingHeadFromCatchUp;
  private readonly float _jumpRiseSpeedThreshold;
  private readonly long _jumpRiseWindowTicks;
  private readonly long _maximumCatchUpHistoryTicks;
  private readonly PoseHistoryBuffer _history =
      new PoseHistoryBuffer(HistoryCapacity);
  private readonly FixedTimeMapper _timeMapper = new FixedTimeMapper();
  private readonly FixedTickTimeline _tickTimeline;
  private readonly ManualResetEvent _stop = new ManualResetEvent(false);

  private CVRSystem _system;
  private Thread _thread;
  private volatile bool _running;

  private int _lastUnityFrame = -1;
  private bool _jumpCatchUpDebt;
  private readonly BackgroundButtonSampler _buttonSampler = new BackgroundButtonSampler();
  private readonly ButtonHistory _jumpButtons = new ButtonHistory();
  private readonly ButtonHistory _slowMotionButtons = new ButtonHistory();
  private long _buttonSequence;
  private bool _deliveringQueuedJump;
  private readonly SlowMotionReplayState _slowMotionReplay =
      new SlowMotionReplayState(Stopwatch.Frequency);
  private bool _suppressLiveSlowMotion;
  private SteamControllerRig _currentSteamRig;
  private CurrentFixedPose _current;
  private bool IsActive => _enabled && _running;

  private partial struct CurrentFixedPose
  {
    public bool Attempted, Available, HeadApplied, FixedJumpButtonHeld;
    public float FixedDeltaTime, TimeScale;
    public long TargetTimestamp;
    public bool JumpProtectionActive;
    public TrackedPoseValue Head;
  }

  public FixedUpdatePoseHistory(
      bool enabled, int sampleRate, int delayTicks, float catchUpSpeed,
      float catchUpHistorySeconds, bool protectRisingHeadFromCatchUp,
      float jumpRiseSpeedThreshold, float jumpRiseDetectionWindowSeconds
  )
  {
    _enabled = enabled;
    _sampleRate = Math.Max(1, Math.Min(1000, sampleRate));
    _delayTicks = Math.Max(1, Math.Min(8, delayTicks));
    _protectRisingHeadFromCatchUp = protectRisingHeadFromCatchUp;
    _jumpRiseSpeedThreshold =
        Math.Max(0.05f, Math.Min(5f, jumpRiseSpeedThreshold));
    _jumpRiseWindowTicks = (long)Math.Round(
        Math.Max(0.02f, Math.Min(0.5f, jumpRiseDetectionWindowSeconds)) *
        Stopwatch.Frequency
    );
    _maximumCatchUpHistoryTicks = (long)Math.Round(
        Math.Max(0.1f, Math.Min(5f, catchUpHistorySeconds)) *
        Stopwatch.Frequency
    );
    _tickTimeline = new FixedTickTimeline(_timeMapper, catchUpSpeed);
    _instance = this;
  }

  public bool IsJumpDetected =>
      IsActive && _current.JumpProtectionActive;

  public float InputTicksBehindCurrent
  {
    get
    {
      if (!_enabled || !_running || !_current.Attempted ||
          _current.TargetTimestamp == 0 || _current.FixedDeltaTime <= 0f ||
          _current.TimeScale <= 0.000001f)
        return -1f;
      var interval =
          _current.FixedDeltaTime / _current.TimeScale * Stopwatch.Frequency;
      if (interval <= 0.0)
        return -1f;
      var age =
          Math.Max(0L, Stopwatch.GetTimestamp() - _current.TargetTimestamp);
      return (float)(age / interval);
    }
  }

  public void ObserveRenderedFrame()
  {
    if (!_enabled)
      return;
    EnsureStarted();
    var now = Stopwatch.GetTimestamp();
    _timeMapper.ObserveFrame(now, Time.time, Time.timeScale);
#if DEBUG
    DebugObserveFrame(now);
#endif
  }

  public void ResetScene()
  {
    _current = new CurrentFixedPose();
    _lastUnityFrame = -1;
    _jumpCatchUpDebt = false;
    _buttonSampler.Reset();
    ResetButtonHistory();
    _suppressLiveSlowMotion = false;
    _currentSteamRig = null;
    _tickTimeline.Reset();
#if DEBUG
    DebugResetScene();
#endif
  }

  public void Shutdown()
  {
    _running = false;
    _stop.Set();
    if (_thread != null && !_thread.Join(2000))
      MelonLogger.Warning("Fixed-pose sampler did not stop within two seconds."
      );
    _thread = null;
#if DEBUG
    EndDiagnosticRun();
#endif
    if (ReferenceEquals(_instance, this))
      _instance = null;
  }

  private void EnsureStarted()
  {
    if (_running || _thread != null)
      return;
    try
    {
      var synthetic = false;
#if DEBUG
      DebugPrepareSampler(ref synthetic);
#endif
      if (!synthetic)
      {
        _system = OpenVR.System;
        if (_system == null)
          return;
      }
      _running = true;
      _thread = new Thread(SampleLoop)
      {
        IsBackground = true,
        Name = "Boneworks fixed-pose history",
        Priority = System.Threading.ThreadPriority.AboveNormal,
      };
      _thread.Start();
#if DEBUG
      MelonLogger.Msg(
          "Fixed-update pose history active at " + _sampleRate + " Hz with " +
          _delayTicks + " tick interpolation delay."
      );
#endif
    } catch (Exception exception)
    {
      _system = null;
      _running = false;
      _thread = null;
      MelonLogger.Warning(
          "Could not start fixed-update pose history: " + exception.Message
      );
    }
  }
  private void SampleLoop()
  {
    try
    {
#if DEBUG
      if (DebugSampleLoop())
        return;
#endif
      using (Il2CppThreadRegistration.Attach())
      {
        using var wait = new SamplerWait(_stop);
        var poses = new Il2CppStructArray<TrackedDevicePose_t>(
            OpenVR.k_unMaxTrackedDeviceCount
        );
        var interval = Math.Max(1L, Stopwatch.Frequency / _sampleRate);
        var next = Stopwatch.GetTimestamp();
        while (_running)
        {
          var now = Stopwatch.GetTimestamp();
          var remaining = next - now;
          if (remaining > 0)
          {
            if (!wait.Until(next))
              break;
            continue;
          }
#if DEBUG
          DebugSamplerTick(now, now - next, interval);
#endif
          SamplePoses(poses);
          // Never burst-poll missed deadlines: those historical samples cannot be recovered.
          next = SamplerSchedule.Next(next, Stopwatch.GetTimestamp(), interval);
        }
      }
    } catch (Exception exception)
    {
      MelonLogger.Warning(
          "Fixed-pose sampler stopped unexpectedly: " + exception.Message
      );
    } finally
    {
      _running = false;
#if DEBUG
      DebugSamplerStopped();
#endif
    }
  }

  private void SamplePoses(Il2CppStructArray<TrackedDevicePose_t> poses)
  {
    var sample = new PoseHistorySample();
    var started = Stopwatch.GetTimestamp();
    try
    {
      _system.GetDeviceToAbsoluteTrackingPose(
          ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, poses
      );
      var ended = Stopwatch.GetTimestamp();
      sample.Timestamp = started + (ended - started) / 2;
      sample.Head = ReadPose(poses, 0);
#if DEBUG
      DebugPopulateSample(ref sample, poses, ended - started);
#endif
    } catch
    {
#if DEBUG
      DebugQueryFailed();
#endif
      sample.Timestamp = Stopwatch.GetTimestamp();
    }
    try
    {
      sample.Buttons = _buttonSampler.Sample(_system);
    } catch
    {
#if DEBUG
      DebugQueryFailed();
#endif
    }
    _history.Add(sample);
  }

  private void PrepareFixedTick(SteamControllerRig rig)
  {
    _current = new CurrentFixedPose();
    _currentSteamRig = rig;
    if (!_enabled || !_running)
      return;
    var runFixedUpdates = rig._runFixedUpdates;
#if DEBUG
    DebugAllowSyntheticUpdates(ref runFixedUpdates);
#endif
    if (!runFixedUpdates)
      return;
    DrainButtonSamples();
    var frame = Time.frameCount;
    var firstTickInFrame = frame != _lastUnityFrame;
    _lastUnityFrame = frame;
    var fixedTime = Time.fixedTime;
    var fixedDelta = Time.fixedDeltaTime;
    var timeScale = Time.timeScale;
    _current = new CurrentFixedPose
    {
      Attempted = true,
      FixedDeltaTime = fixedDelta,
      TimeScale = timeScale,
    };
#if DEBUG
    DebugBeginFixedTick(frame, firstTickInFrame, fixedTime);
#endif
    var headVelocityAvailable = _history.TryGetHeadVerticalVelocity(
        _jumpRiseWindowTicks, Stopwatch.Frequency, out var headVerticalVelocity
    );
    var jumpProtectionActive = _protectRisingHeadFromCatchUp &&
        headVelocityAvailable &&
        headVerticalVelocity >= _jumpRiseSpeedThreshold;
    if (!_tickTimeline.TryAdvance(
            fixedTime, fixedDelta, timeScale, Stopwatch.Frequency,
            firstTickInFrame, jumpProtectionActive, out var simulationTimestamp,
            out var independentlyMappedTimestamp, out var reanchored
        ))
    {
#if DEBUG
      DebugUnavailable();
#endif
      return;
    }
    _current.JumpProtectionActive = jumpProtectionActive;
    if (_tickTimeline.LastForwardCorrectionSuppressed)
      _jumpCatchUpDebt = true;
    var targetTimestamp = simulationTimestamp -
        (long)Math.Round(
            _delayTicks * fixedDelta / Math.Max(0.000001f, timeScale) *
            Stopwatch.Frequency
        );
    var now = Stopwatch.GetTimestamp();
    var fixedIntervalTicks = (long)Math.Round(
        fixedDelta / Math.Max(0.000001f, timeScale) * Stopwatch.Frequency
    );
    var normalMaximumHistoryAgeTicks =
        fixedIntervalTicks * _delayTicks + Stopwatch.Frequency / 4;
    var extendedHistoryAge = jumpProtectionActive || _jumpCatchUpDebt;
    var maximumHistoryAgeTicks = extendedHistoryAge
        ? Math.Max(
              normalMaximumHistoryAgeTicks,
              fixedIntervalTicks * _delayTicks + _maximumCatchUpHistoryTicks
          )
        : normalMaximumHistoryAgeTicks;
    var targetAgeTicks = Math.Max(0, now - targetTimestamp);
    if (_jumpCatchUpDebt && !jumpProtectionActive &&
        targetAgeTicks <= normalMaximumHistoryAgeTicks)
      _jumpCatchUpDebt = false;
    _current.TargetTimestamp = targetTimestamp;
#if DEBUG
    DebugClockMapped(
        simulationTimestamp, independentlyMappedTimestamp, reanchored,
        headVelocityAvailable, headVerticalVelocity, targetAgeTicks,
        maximumHistoryAgeTicks
    );
#endif
    if (targetAgeTicks > maximumHistoryAgeTicks)
    {
#if DEBUG
      DebugUnavailablePose(now, default, default, PoseBracketStatus.Stale);
#endif
      return;
    }
    if (!_history.TryGetBracket(
            targetTimestamp, out var before, out var after, out var status
        ))
    {
#if DEBUG
      DebugUnavailablePose(now, before, after, status);
#endif
      return;
    }
    if (status == PoseBracketStatus.AfterHistory &&
        _history.TryGetLatestPair(out before, out after))
    {
      var predictionTicks = targetTimestamp - after.Timestamp;
      var maximumPredictionTicks = Stopwatch.Frequency / 10;
      if (predictionTicks > 0 && predictionTicks <= maximumPredictionTicks)
      {
        status = PoseBracketStatus.Extrapolated;
      }
    }
    if (status != PoseBracketStatus.Exact &&
        status != PoseBracketStatus.Interpolated &&
        status != PoseBracketStatus.Extrapolated)
    {
#if DEBUG
      DebugUnavailablePose(now, before, after, status);
#endif
      return;
    }
    var span = after.Timestamp - before.Timestamp;
    var amount = span <= 0
        ? 0f
        : (float)((targetTimestamp - before.Timestamp) / (double)span);
    if (status != PoseBracketStatus.Extrapolated)
      amount = Math.Max(0f, Math.Min(1f, amount));
    var head = status == PoseBracketStatus.Extrapolated
        ? PoseHistoryMath.Extrapolate(before.Head, after.Head, amount)
        : PoseHistoryMath.Interpolate(before.Head, after.Head, amount);
    _current.Available = true;
    _current.Head = head;
#if DEBUG
    DebugSetBracket(before, after, status, amount);
#endif
  }

  private long ButtonMaximumAge =>
      Math.Max(Stopwatch.Frequency / 20, Stopwatch.Frequency * 3L / _sampleRate);

  private void ResetButtonHistory()
  {
    _buttonSequence = _history.TotalWritten;
    _jumpButtons.Reset();
    _slowMotionButtons.Reset();
    _slowMotionReplay.Reset();
  }

  private void DrainButtonSamples()
  {
    while (_history.TryReadNext(ref _buttonSequence, out var sample, out var lost))
    {
      if (lost)
      {
        _jumpButtons.Reset();
        _slowMotionButtons.Reset();
        _slowMotionReplay.Reset();
#if DEBUG
        DebugButtonHistoryLost();
#endif
      }
      var buttons = sample.Buttons;
      if (buttons.Generation != _buttonSampler.Generation)
        continue;
      var jumpChanged = _jumpButtons.Observe(buttons.Timestamp, buttons.Jump);
      var slowChanged = _slowMotionButtons.Observe(buttons.Timestamp, buttons.SlowMotion);
#if DEBUG
      DebugObserveButtons(buttons, jumpChanged, slowChanged);
#endif
    }
  }

  private void ObserveJumpButton(SteamControllerRig rig)
  {
    if (!IsActive || !rig)
      return;
    if (!rig._runFixedUpdates)
    {
      _buttonSampler.Reset();
      ResetButtonHistory();
      return;
    }
    if (_buttonSampler.Configure(rig))
    {
      ResetButtonHistory();
#if DEBUG
      DebugButtonConfiguration(rig);
#endif
    }
    DrainButtonSamples();
    var controller = JumpButtonTiming.Controller(rig);
    var now = Stopwatch.GetTimestamp();
    if (!JumpButtonTiming.UsesAButton(controller) || _jumpButtons.IsFresh(now, ButtonMaximumAge))
      return;
    var timestamp = JumpButtonTiming.ReadEdgeTimestamp(rig, now, Time.realtimeSinceStartup);
    _jumpButtons.Observe(timestamp, controller.GetAButton() ? SampledButton.Held : SampledButton.Released);
  }

  private void BeginControllerEarlyUpdate(ControllerRig rig)
  {
    _suppressLiveSlowMotion = false;
    if (!IsActive || !rig || !rig._runFixedUpdates)
      return;
    var controller = SlowMotionButtonTiming.Controller(rig);
    if (!SlowMotionButtonTiming.IsSupported(controller))
      return;
    _suppressLiveSlowMotion = true;
  }

  private void EndControllerEarlyUpdate(ControllerRig rig)
  {
    if (!IsActive || !_suppressLiveSlowMotion || !rig)
      return;
    ObserveLiveSlowMotionButton(rig);
    rig._timeInput = false;
    _suppressLiveSlowMotion = false;
  }

  private void ObserveLiveSlowMotionButton(ControllerRig rig)
  {
    DrainButtonSamples();
    var controller = SlowMotionButtonTiming.Controller(rig);
    var now = Stopwatch.GetTimestamp();
    if (!SlowMotionButtonTiming.IsSupported(controller) ||
        _slowMotionButtons.IsFresh(now, ButtonMaximumAge))
      return;
    var action = SlowMotionButtonTiming.Action(controller);
    var changedTime = action == null ? 0f :
        action.GetTimeLastChanged(SlowMotionButtonTiming.Source(rig));
    var timestamp = JumpButtonTiming.UnityRealtimeToStopwatch(
        changedTime, Time.realtimeSinceStartup, now);
    _slowMotionButtons.Observe(timestamp, SlowMotionButtonTiming.IsPressed(controller)
        ? SampledButton.Held : SampledButton.Released);
  }

  private void DeliverSlowMotionEvents(ControllerRig rig)
  {
    if (!IsActive || !_current.Attempted || _current.TargetTimestamp == 0 ||
        !rig)
      return;
    var manager = rig.manager;
    var health = manager && manager.playerHealth ? manager.playerHealth : null;
    var globalTime = health ? health.globalTimeControl : null;
    if (!globalTime)
      return;
    while (true)
    {
      var edgeTimestamp = _slowMotionButtons.NextTimestamp;
      var toggleTimestamp = _slowMotionReplay.PendingToggleTimestamp == 0
          ? long.MaxValue
          : _slowMotionReplay.PendingToggleTimestamp;
      var nextTimestamp = Math.Min(edgeTimestamp, toggleTimestamp);
      if (nextTimestamp > _current.TargetTimestamp)
        break;
      if (toggleTimestamp <= edgeTimestamp)
      {
        if (_slowMotionReplay.ConsumeToggleAt(_current.TargetTimestamp) ==
            SlowMotionReplayCommand.ToggleTimeScale)
          globalTime.TOGGLE_TIMESCALE();
#if DEBUG
        DebugSlowMotionToggled();
#endif
        continue;
      }
      if (!_slowMotionButtons.TryConsume(_current.TargetTimestamp, out var edge))
        break;
      if (edge.IsInitial)
        continue;
      var command = _slowMotionReplay.Apply(edge);
      if (command == SlowMotionReplayCommand.DecreaseTimeScale)
      {
        if (rig.slowMoEnabled)
          globalTime.DECREASE_TIMESCALE();
      }
#if DEBUG
      DebugSlowMotionEdge(command);
#endif
    }
    rig._timeInput = false;
  }

  private bool
  ShouldSuppressLiveSlowMotion() => IsActive && _suppressLiveSlowMotion;

  private bool InterceptJump(ControllerRig rig)
  {
    if (_deliveringQueuedJump || !IsActive || !rig)
      return true;
    var controller = JumpButtonTiming.Controller(rig);
    if (!JumpButtonTiming.UsesAButton(controller) || !controller.GetAButtonUp())
      return true;
    // The worker is authoritative while fresh. Otherwise retain the action-timestamp fallback.
    var now = Stopwatch.GetTimestamp();
    if (!_jumpButtons.IsFresh(now, ButtonMaximumAge))
    {
      var timestamp = JumpButtonTiming.ReadEdgeTimestamp(rig, now, Time.realtimeSinceStartup);
      _jumpButtons.Observe(timestamp, SampledButton.Released);
    }
    return false;
  }

  private void DeliverQueuedJump(ControllerRig rig)
  {
    if (!IsActive || !rig || !_current.Attempted || _current.TargetTimestamp == 0)
      return;
    while (_jumpButtons.TryConsume(_current.TargetTimestamp, out var edge))
    {
#if DEBUG
      if (!edge.IsInitial && edge.Pressed)
        _superJumpDiagnostics?.ObserveFixedJumpPress(edge.Timestamp);
#endif
      if (edge.IsInitial || edge.Pressed)
        continue;
#if DEBUG
      DebugJumpReleased(edge.Timestamp);
      _superJumpDiagnostics?.ObserveJumpRelease(rig, Stopwatch.GetTimestamp(), edge.Timestamp);
#endif
      _deliveringQueuedJump = true;
      try
      {
        rig.Jump();
      } finally
      {
        _deliveringQueuedJump = false;
      }
    }
    _current.FixedJumpButtonHeld = _jumpButtons.Held;
  }

  private void OverrideJumpCharge(ControllerRig rig, ref bool chargeInput)
  {
    if (!IsActive || !_current.Attempted || _current.TargetTimestamp == 0 ||
        !rig)
      return;
    chargeInput = _current.FixedJumpButtonHeld;
  }

  private void BeforeController(RigController controller)
  {
    if (_current.Available && controller && !_current.HeadApplied)
      ApplyHead(_currentSteamRig);
  }

  private void ApplyHead(SteamControllerRig rig)
  {
    if (!_current.Available || !rig)
      return;
    var poses = rig.poses;
    var hmd = rig.hmdTransform;
    var target = hmd ? hmd.parent : null;
#if DEBUG
    if (DebugApplySyntheticHead(target))
      return;
#endif
    if (poses != null && poses.Length > 0 && target && _current.Head.IsValid)
    {
      var live = ReadPose(poses, 0);
      if (live.IsValid)
      {
        ApplyCalibratedPosition(
            live, _current.Head, target.localPosition, out var position
        );
        target.localPosition = position;
        _current.HeadApplied = true;
#if DEBUG
        DebugHeadApplied(position, target.localRotation);
#endif
      }
    }
  }

  private void FinishFixedTick()
  {
    if (!_current.Attempted)
      return;
#if DEBUG
    DebugFinishFixedTick();
#endif

    _current.Available = false;
    _currentSteamRig = null;
  }

  internal static void ApplyCalibratedPosition(
      TrackedPoseValue live, TrackedPoseValue historical,
      Vector3 liveOutputPosition, out Vector3 position
  )
  {
    position = liveOutputPosition + new Vector3(
        historical.X - live.X, historical.Y - live.Y, live.Z - historical.Z);
  }

  private static TrackedPoseValue
  ReadPose(Il2CppStructArray<TrackedDevicePose_t> poses, int deviceIndex)
  {
    if (poses == null || deviceIndex < 0 || deviceIndex >= poses.Length)
      return new TrackedPoseValue();
    var pose = poses[deviceIndex];
    var matrix = pose.mDeviceToAbsoluteTracking;
    var result = new TrackedPoseValue
    {
      X = matrix.m3, Y = matrix.m7, Z = matrix.m11,
      IsValid = pose.bPoseIsValid,
    };
#if DEBUG
    DebugReadRotation(ref result, matrix, pose.bDeviceIsConnected, (int)pose.eTrackingResult);
#endif
    return result;
  }

  [HarmonyPatch(
      typeof(SteamControllerRig), nameof(SteamControllerRig.OnFixedUpdate)
  )]
  private static class SteamControllerRig_OnFixedUpdate_Patch
  {
    [HarmonyPrefix]
    private static bool Prefix(SteamControllerRig __instance)
    {
      var instance = _instance;
      if (instance == null)
        return true;
      instance.PrepareFixedTick(__instance);
      instance.DeliverSlowMotionEvents(__instance);
      instance.DeliverQueuedJump(__instance);
#if DEBUG
      if (instance.DebugRunSyntheticFixedTick())
        return false;
#endif
      return true;
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
#if DEBUG
      if (_instance != null && _instance.DebugSkipFixedPostfix())
        return;
#endif
      _instance?.FinishFixedTick();
    }
  }

  [HarmonyPatch(typeof(ControllerRig), nameof(ControllerRig.OnEarlyUpdate))]
  private static class ControllerRig_OnEarlyUpdate_Patch
  {
    [HarmonyPrefix]
    private static void Prefix(ControllerRig __instance
    ) => _instance?.BeginControllerEarlyUpdate(__instance);

    [HarmonyPostfix]
    private static void Postfix(ControllerRig __instance
    ) => _instance?.EndControllerEarlyUpdate(__instance);

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception)
    {
      if (_instance != null)
        _instance._suppressLiveSlowMotion = false;
      return __exception;
    }
  }

  [HarmonyPatch(
      typeof(Control_GlobalTime), nameof(Control_GlobalTime.DECREASE_TIMESCALE)
  )]
  private static class ControlGlobalTime_DecreaseTimeScale_Patch
  {
    [HarmonyPrefix]
    private static bool Prefix() => _instance == null ||
        !_instance.ShouldSuppressLiveSlowMotion();
  }

  [HarmonyPatch(
      typeof(Control_GlobalTime), nameof(Control_GlobalTime.TOGGLE_TIMESCALE)
  )]
  private static class ControlGlobalTime_ToggleTimeScale_Patch
  {
    [HarmonyPrefix]
    private static bool Prefix() => _instance == null ||
        !_instance.ShouldSuppressLiveSlowMotion();
  }

  [HarmonyPatch(
      typeof(SteamControllerRig), nameof(SteamControllerRig.OnEarlyUpdate)
  )]
  private static class SteamControllerRig_OnEarlyUpdate_Patch
  {
    [HarmonyPostfix]
    private static void Postfix(SteamControllerRig __instance
    ) => _instance?.ObserveJumpButton(__instance);
  }

  [HarmonyPatch(typeof(ControllerRig), nameof(ControllerRig.JumpCharge))]
  private static class ControllerRig_JumpCharge_Patch
  {
    [HarmonyPrefix]
    private static void Prefix(
        ControllerRig __instance, ref bool chargeInput
    ) => _instance?.OverrideJumpCharge(__instance, ref chargeInput);
  }

  [HarmonyPatch(typeof(ControllerRig), nameof(ControllerRig.Jump))]
  private static class ControllerRig_Jump_Patch
  {
    [HarmonyPrefix]
    private static bool Prefix(ControllerRig __instance) => _instance == null
        || _instance.InterceptJump(__instance);
  }

  [HarmonyPatch(typeof(RigController), nameof(RigController.OnVrFixedUpdate))]
  private static class Controller_OnVrFixedUpdate_Patch
  {
    [HarmonyPrefix]
    private static void Prefix(RigController __instance
    ) => _instance?.BeforeController(__instance);
  }
}
