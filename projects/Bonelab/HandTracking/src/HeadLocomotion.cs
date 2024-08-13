using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using Sst.Utilities;
using SLZ.Marrow.Utilities;
using System.IO;

namespace Sst.HandTracking;

public class HeadLocomotion : Locomotion {
  // TODO: Work with the game's controller deadzone
  private const float DEADZONE = 0.1f;
  private const float MAX_DIST = 0.1f;
  private const float WALK_SPEED = 0.4f;
  private const float RUN_REMAINDER = 1f - WALK_SPEED;

  private RunningDetector _runningDetector = new();
  private MelonPreferences_Entry<bool> _prefForwardsOnly;

  public HeadLocomotion(MelonPreferences_Entry<bool> prefForwardsOnly) {
    _prefForwardsOnly = prefForwardsOnly;
  }

  public override void Init(HandTracker tracker) {}

  public override void Update() {
    var hmd = MarrowGame.xr.HMD;
    // TODO: Account for controller-rotation movement setting
    var hmdRotY = hmd.Rotation.eulerAngles.y;
    var direction =
        Rotate(new Vector2(hmd.Position.x, hmd.Position.z), hmdRotY);
    var directionAmount =
        Mathf.Clamp01((direction.magnitude - DEADZONE) / MAX_DIST);

    var runningScore = _runningDetector.CalculateRunningScore(
        Time.timeAsDouble, hmd.Position.y
    );
    var runningSpeed = RUN_REMAINDER * Mathf.Clamp01(runningScore);

    var stickAmount = directionAmount * (WALK_SPEED + runningSpeed);

    Axis = direction.normalized * stickAmount;
    if (_prefForwardsOnly.Value) {
      Axis = new Vector2(0f, Mathf.Clamp01(Axis.y));
    }

#if DEBUG
    UpdateRecordHmdY();
#endif
  }

  public static Vector2 Rotate(Vector2 v, float degrees) {
    var radians = degrees * (float)Math.PI / 180f;
    var cos = Mathf.Cos(radians);
    var sin = Mathf.Sin(radians);
    return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
  }

#if DEBUG
  private float _startTime;
  private List<(float Time, float Y)> _heights;
  private void UpdateRecordHmdY() {
    if (_heights == null) {
      if (LevelHooks.RigManager != null) {
        _heights = new();
        _startTime = Time.unscaledTime;
      }
    } else if (LevelHooks.RigManager == null) {
      File.WriteAllLines(
          Path.Combine(Application.persistentDataPath, "hmd-y-recording.csv"),
          _heights.Select(item => item.Time + "," + item.Y)
      );
      _heights = null;
    } else if (_heights.Count < 100000) {
      _heights?.Add(
          (Time.unscaledTime - _startTime, MarrowGame.xr.HMD.Position.y)
      );
    }
  }
#endif
}

/// Detects running through the up/down bobbing of the player's head
public class RunningDetector {
  // Head bob threshold parameters
  // NOTE: These values are for a single up or down head bob phase (not both)
  private const double TIME_MIN = 0.1;
  private const double TIME_MAX = 0.3;
  private const float TIME_WINDOW = (float)(TIME_MAX - TIME_MIN);
  private const float MIN_DIST_AT_TIME_MIN = 0.02f;
  private const float MIN_DIST_AT_TIME_MAX = 0.12f;
  private const float DIST_WINDOW =
      (float)(MIN_DIST_AT_TIME_MAX - MIN_DIST_AT_TIME_MIN);
  // Controls how the minimum head bob distance ramps from the value at TIME_MIN
  // to the value at TIME_MAX with 1 being linear and above 1 being exponential
  private const float DIST_EXPONENT = 2f;

  // Number of phases required to register before running starts (to avoid
  // starting running when crouching/uncrouching)
  private const int WARMUP_PHASES = 2;
  // Resets the running state and phase counter after this amount of time
  // without a head bob
  private const double RESET_TIME = 0.6;

  // List of head positions during the current phase
  private Queue<(double Time, float Y)> _phaseWindow = new();
  // Whether the current phase is going up or down
  private bool _isUpPhase;
  // Number of head bobs up or down in the current run
  private int _phaseCounter;
  // Time and head position at the start of the phase
  private (double Time, float Y) _phaseStart;
  // Time and head position at the start of the previous phase (used to
  // retroactively update the previous phase if the head continues moving past
  // the minimum threshold which will have activated the next phase)
  private (double Time, float Y) _prevPhaseStart;

  // Returns 1 if running, else 0
  public float CalculateRunningScore(double time, float hmdY) {
    UpdateStartingPointIfNecessary(time, hmdY);

    if (IsPhaseComplete(time, hmdY)) {
      _prevPhaseStart = _phaseStart;
      _phaseStart = (time, hmdY);
      _phaseWindow.Clear();
      _phaseWindow.Enqueue(_phaseStart);
      _phaseCounter++;
      _isUpPhase = !_isUpPhase;
    }

    var resetTime = _phaseCounter >= WARMUP_PHASES ? RESET_TIME : TIME_MAX;
    if (_phaseCounter > 0 && time - _phaseStart.Time > resetTime) {
      _phaseCounter = 0;
    }

    while (_phaseWindow.Count > 0 && time - _phaseWindow.Peek().Time > TIME_MAX
    ) {
      _phaseWindow.Dequeue();
    }
    _phaseWindow.Enqueue((time, hmdY));

    // TODO: Return a number between 0 and 1 before warmup finishes?
    // TODO: Lock running or only require infrequent head bobs after starting?
    return _phaseCounter >= WARMUP_PHASES ? 1f : 0f;
  }

  // Once the head movement hits the minimum threshold to count as a head bob
  // the next phase starts, however the head will probably continue moving and
  // produce a larger head bob than the minimum threshold, so we check that the
  // current time/position is still within the threshold for a head bob from the
  // start of the previous phase and update the starting point of the current
  // phase if so
  private void UpdateStartingPointIfNecessary(double time, float hmdY) {
    if (_phaseWindow.Count == 0)
      return;

    var phaseStartY = _phaseWindow.Peek().Y;
    var isHeadLowerThanWindowStart = hmdY < phaseStartY;
    if (_isUpPhase != isHeadLowerThanWindowStart)
      return;

    var duration = time - _prevPhaseStart.Time;
    var dist = Mathf.Abs(hmdY - _prevPhaseStart.Y);
    if (!IsWithinHeadBobThreshold(duration, dist))
      return;

    _phaseStart = (time, hmdY);
    _phaseWindow.Clear();
    _phaseWindow.Enqueue(_phaseStart);
  }

  // Moves to the next head bob phase (up or down) if the minimum head bob
  // time/distance threshold has been reached
  private bool IsPhaseComplete(double time, float hmdY) {
    foreach (var (prevTime, prevY) in _phaseWindow) {
      var duration = time - prevTime;
      if (duration < TIME_MIN)
        return false;

      var isHmdGoingUp = hmdY > prevY;
      if (_isUpPhase != isHmdGoingUp)
        continue;

      var dist = Mathf.Abs(hmdY - prevY);
      if (!IsWithinHeadBobThreshold(duration, dist))
        continue;

      return true;
    }
    return false;
  }

  private bool IsWithinHeadBobThreshold(double duration, float dist) {
    if (duration < TIME_MIN || duration > TIME_MAX)
      return false;

    var minDist = MIN_DIST_AT_TIME_MIN +
        Mathf.Pow((float)(duration - TIME_MIN) / TIME_WINDOW, DIST_EXPONENT) *
            DIST_WINDOW;
    return dist >= minDist;
  }
}
