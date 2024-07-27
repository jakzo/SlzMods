using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sst.Utilities;
using SLZ.Marrow.Utilities;

namespace Sst.HandTracking;

public class HeadLocomotion : Locomotion {
  // TODO: Work with the game's controller deadzone
  private const float DEADZONE = 0.1f;
  private const float MAX_DIST = 0.1f;
  private const float WALK_SPEED = 0.4f;
  private const float HEAD_BOB_DEADZONE = 0.01f;
  private const float HEAD_BOB_MAX = 0.05f;
  private const float HEAD_BOB_MULTIPLIER = 100f;
  private const float RUN_SCORE_MAX = 1.2f;
  private const float RUN_SCORE_DECAY = 2f;
  private const float RUN_REMAINDER = 1f - WALK_SPEED;

  // Only run forwards instead of any direction (easier to control)
  private const bool LOCK_FORWARDS = true;

  // TODO: Running start still seems a bit too sensitive
  private float _startY = 0f;
  private bool _isHeadDirUp = false;
  private bool _isRunning = false;
  private float _runningScore = 0f;

  public override void Init(HandTracker tracker) {}

  public override void Update() {
    var hmd = MarrowGame.xr.HMD;

    // TODO: Account for controller-rotation movement setting
    var direction = Rotate(new Vector2(hmd.Position.x, hmd.Position.z),
                           hmd.Rotation.eulerAngles.y);
    var directionAmount =
        Mathf.Clamp01((direction.magnitude - DEADZONE) / MAX_DIST);

    var y = hmd.Position.y;
    var prevY = hmd._lastPosition.y;
    if ((y > prevY) != _isHeadDirUp) {
      _isHeadDirUp = !_isHeadDirUp;
      _startY = prevY;
      if (_runningScore > 0f)
        _isRunning = true;
    }
    var deadzoneDist =
        Mathf.Max(HEAD_BOB_DEADZONE - Mathf.Abs(_startY - prevY), 0f);
    var dist = Mathf.Max(Mathf.Abs(y - prevY) - deadzoneDist, 0f);
    var prevTotalDist = Mathf.Abs(prevY - _startY);
    var distMultiplier =
        Mathf.Clamp01(1f - (prevTotalDist - HEAD_BOB_DEADZONE) / HEAD_BOB_MAX);
    var addedScore = dist * distMultiplier * HEAD_BOB_MULTIPLIER;
    var decay = RUN_SCORE_DECAY * Time.deltaTime;
    var newRunningScore =
        Mathf.Clamp(_runningScore + addedScore - decay, 0f, RUN_SCORE_MAX);

    if (_runningScore > 0f && newRunningScore <= 0f)
      _isRunning = false;
    _runningScore = newRunningScore;

    var runningSpeed =
        _isRunning ? RUN_REMAINDER * Mathf.Clamp01(_runningScore) : 0f;

    var stickAmount = directionAmount * (WALK_SPEED + runningSpeed);
    Axis = LOCK_FORWARDS ? new Vector2(0f, stickAmount)
                         : direction.normalized *stickAmount;

    // Mod.Instance.TrackerLeft.LogToWrist(
    //     "dir=" + direction.ToString() + ", rs=" +
    //     _runningScore.ToString("N1") +
    //     ", dm=" + distMultiplier.ToString("N1") + ", as=" +
    //     addedScore.ToString("N1") + ", da=" +
    //     directionAmount.ToString("N1"));
  }

  public static Vector2 Rotate(Vector2 v, float degrees) {
    var radians = degrees * (float)Math.PI / 180f;
    var cos = Mathf.Cos(radians);
    var sin = Mathf.Sin(radians);
    return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
  }
}
