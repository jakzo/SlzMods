using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sst.Utilities;
using SLZ.Marrow.Utilities;
using System.IO;

namespace Sst.HandTracking;

public class HeadLocomotion : Locomotion {
  // TODO: Account for or bypass the game's controller deadzone/scaling
  // TODO: Account for forwards HMD movement due to looking down or crouching
  private const float DEADZONE = 0.1f;
  private const float MAX_INPUT_DIST = DEADZONE + 0.1f;
  private const float MAX_OFFSET = MAX_INPUT_DIST + 0.2f;
  private const float DEFAULT_OFFSET = MAX_OFFSET;
  private const float LOCK_DISENGAGE_POINT = 0.25f;
  private const float WALK_SPEED = 0.4f;
  private const float RUN_REMAINDER = 1f - WALK_SPEED;
  // Amount added to offset per second while running is locked
  private const float RECALIBRATION_RATE = 0.025f;

  private RunningDetector _runningDetector = new();
  private float _offset;
  private bool _isLocked;

  public override void Init(HandTracker tracker) {}

  public override void Update() {
    if (LevelHooks.IsLoading) {
      _offset = 0f;
      return;
    }

    var hmd = MarrowGame.xr.HMD;
    var hmdRotY = hmd.Rotation.eulerAngles.y;
    var forwardsOnly = Mod.Preferences.ForwardsOnly.Value;
    var delta = forwardsOnly ? hmd.Position - hmd._lastPosition : hmd.Position;
    var direction = Rotate(new Vector2(delta.x, delta.z), hmdRotY);

    if (forwardsOnly) {
      _offset = Mathf.Clamp(
          _offset + direction.y +
              (_isLocked ? RECALIBRATION_RATE * Time.deltaTime : 0f),
          0f, MAX_OFFSET
      );
    }

    var magnitude = forwardsOnly ? _offset : direction.magnitude;
    var directionAmount =
        Mathf.Clamp01((magnitude - DEADZONE) / (MAX_INPUT_DIST - DEADZONE));

    if (directionAmount <= LOCK_DISENGAGE_POINT) {
      _isLocked = false;
    }

    var runningScore = _runningDetector.CalculateRunningScore(
        Time.timeAsDouble, hmd.Position.y
    );

    if (forwardsOnly) {
      var timeSinceLastHeadBob =
          Time.timeAsDouble - _runningDetector.PrevPhaseStart.Time;
      if (timeSinceLastHeadBob < RunningDetector.RESET_TIME) {
        _offset = DEFAULT_OFFSET;
      }
    }

    var numPhasesUntilLock = Mod.Preferences.LockRunning.Value;
    if (numPhasesUntilLock > 0 &&
        _runningDetector.PhaseCounter > numPhasesUntilLock) {
      _isLocked = true;
    }

    var runningSpeed = RUN_REMAINDER * (_isLocked ? 1f : runningScore);
    var stickAmount = directionAmount * (WALK_SPEED + runningSpeed);

    Axis = (forwardsOnly ? Vector2.up : direction.normalized) * stickAmount;

#if DEBUG
    UpdateRecordHmdPosition();
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
  private List<(float Time, float Y, float Z, float Pitch)> _data;
  private void UpdateRecordHmdPosition() {
    if (_data == null) {
      if (LevelHooks.RigManager != null) {
        _data = new();
        _startTime = Time.unscaledTime;
      }
    } else if (LevelHooks.RigManager == null) {
      File.WriteAllLines(
          Path.Combine(Application.persistentDataPath, "hmd-recording.csv"),
          _data.Select(item => item.Time + "," + item.Y + "," + item.Z)
      );
      _data = null;
    } else if (_data.Count < 100000) {
      _data?.Add((
          Time.unscaledTime - _startTime, MarrowGame.xr.HMD.Position.y,
          MarrowGame.xr.HMD.Position.z, MarrowGame.xr.HMD.Rotation.eulerAngles.x
      ));
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
  private const float MIN_DIST_AT_TIME_MIN = 0.01f;
  private const float MIN_DIST_AT_TIME_MAX = 0.12f;
  private const float DIST_WINDOW =
      (float)(MIN_DIST_AT_TIME_MAX - MIN_DIST_AT_TIME_MIN);
  // Controls how the minimum head bob distance ramps from the value at TIME_MIN
  // to the value at TIME_MAX with 1 being linear and above 1 being exponential
  private const float DIST_EXPONENT = 2f;
  // Number of phases required to register before running starts (to avoid
  // starting running when crouching/uncrouching)
  private const int PHASES_UNTIL_RUNNING = 2;
  // Resets the running state and phase counter after this amount of time
  // without a head bob
  public const double RESET_TIME = 0.6;

  // Number of head bobs up or down in the current run
  public int PhaseCounter;
  // Time and head position at the start of the previous phase (used to
  // retroactively update the previous phase if the head continues moving past
  // the minimum threshold which will have activated the next phase)
  public (double Time, float Y) PrevPhaseStart;

  // List of head positions during the current phase
  private Queue<(double Time, float Y)> _phaseWindow = new();
  // Whether the current phase is going up or down
  private bool _isUpPhase;
  // Time and head position at the start of the phase
  private (double Time, float Y) _phaseStart;

  // Returns 1 if running, else 0
  public float CalculateRunningScore(double time, float hmdY) {
    UpdateStartingPointIfNecessary(time, hmdY);

    if (IsPhaseComplete(time, hmdY)) {
      PrevPhaseStart = _phaseStart;
      _phaseStart = (time, hmdY);
      _phaseWindow.Clear();
      _phaseWindow.Enqueue(_phaseStart);
      PhaseCounter++;
      _isUpPhase = !_isUpPhase;
    }

    if (ShouldResetPhaseCounter(time)) {
      PhaseCounter = 0;
    }

    while (_phaseWindow.Count > 0 && time - _phaseWindow.Peek().Time > TIME_MAX
    ) {
      _phaseWindow.Dequeue();
    }
    _phaseWindow.Enqueue((time, hmdY));

    return PhaseCounter >= PHASES_UNTIL_RUNNING ? 1f : 0f;
  }

  // Once the head movement hits the minimum threshold to count as a head bob
  // the next phase starts, however the head will probably continue moving and
  // produce a larger head bob than the minimum threshold, so we check that the
  // current time/position is still within the threshold for a head bob from the
  // start of the previous phase (the one that just hit the minimum threshold)
  // and update the starting point of the current phase if so
  private void UpdateStartingPointIfNecessary(double time, float hmdY) {
    if (_phaseWindow.Count == 0)
      return;

    var phaseStartY = _phaseWindow.Peek().Y;
    var isHeadLowerThanWindowStart = hmdY < phaseStartY;
    if (_isUpPhase != isHeadLowerThanWindowStart)
      return;

    var duration = time - PrevPhaseStart.Time;
    var dist = Mathf.Abs(hmdY - PrevPhaseStart.Y);
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

  private bool ShouldResetPhaseCounter(double time) {
    if (PhaseCounter <= 0)
      return false;

    var timeSinceLastHeadBob = time - _phaseStart.Time;
    var resetTime =
        PhaseCounter >= PHASES_UNTIL_RUNNING ? RESET_TIME : TIME_MAX;
    return timeSinceLastHeadBob > resetTime;
  }
}
