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
  private const float RUN_SCORE_MAX = 1.2f;
  private const float RUN_SCORE_DECAY = 2f;
  private const float RUN_REMAINDER = 1f - WALK_SPEED;

  private const float HEAD_BOB_DEADZONE = 0.01f;
  private const float HEAD_BOB_MAX = 0.05f;
  private const float HEAD_BOB_MULTIPLIER = 100f;

  private const int FFT_BUFFER_SIZE = 64;
  private const float FFT_REFRESH_RATE = 90f;
  private const float FFT_SAMPLE_DURATION = 1f / FFT_REFRESH_RATE;
  private const float FFT_HALF_SAMPLE_DURATION = FFT_SAMPLE_DURATION / 2f;
  private const float FFT_WINDOW_DURATION =
      FFT_SAMPLE_DURATION * FFT_BUFFER_SIZE; // 0.71 seconds
  private const float HEAD_BOB_MIN_DURATION = 0.2f;
  private const float HEAD_BOB_MAX_DURATION = 0.5f;
  private static readonly int HEAD_BOB_MIN_FFT_BIN = Mathf.RoundToInt(
      FFT_BUFFER_SIZE / (HEAD_BOB_MAX_DURATION * FFT_WINDOW_DURATION)
  );
  private static readonly int HEAD_BOB_MAX_FFT_BIN = Mathf.RoundToInt(
      FFT_BUFFER_SIZE / (HEAD_BOB_MIN_DURATION * FFT_WINDOW_DURATION)
  );
  private const float HEAD_BOB_THRESHOLD_STEEPNESS = 10f;
  private const float HEAD_BOB_BASE_TIME = 0.25f;
  private const float HEAD_BOB_METERS_PER_SECOND = 0.7f;

  // TODO: Running start still seems a bit too sensitive
  private float _startY = 0f;
  private bool _isHeadDirUp = false;
  private bool _isRunning = false;
  private float _runningScore = 0f;
  private RollingFft _fft;
  private double _lastFftUpdate;
  private float _lastFftResult;
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

    var addedScore = CalculateAddedRunningScoreFft();
    var decay = RUN_SCORE_DECAY * Time.deltaTime;
    var newRunningScore =
        Mathf.Clamp(_runningScore + addedScore - decay, 0f, RUN_SCORE_MAX);

    if (_runningScore > 0f && newRunningScore <= 0f)
      _isRunning = false;
    _runningScore = newRunningScore;

    var runningSpeed =
        _isRunning ? RUN_REMAINDER * Mathf.Clamp01(_runningScore) : 0f;

    var stickAmount = directionAmount * (WALK_SPEED + runningSpeed);

    Axis = direction.normalized * stickAmount;
    if (_prefForwardsOnly.Value) {
      Axis = new Vector2(0f, Mathf.Clamp01(Axis.Value.y));
    }

    // Mod.Instance.TrackerLeft.LogToWrist(
    //     "dir=" + direction.ToString() + ", rs=" +
    //     _runningScore.ToString("N1") +
    //     ", dm=" + distMultiplier.ToString("N1") + ", as=" +
    //     addedScore.ToString("N1") + ", da=" +
    //     directionAmount.ToString("N1"));

#if DEBUG
    UpdateRecordHmdY();
#endif
  }

  private float CalculateAddedRunningScoreFast() {
    var y = MarrowGame.xr.HMD.Position.y;
    var prevY = MarrowGame.xr.HMD._lastPosition.y;
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
    return dist * distMultiplier * HEAD_BOB_MULTIPLIER;
  }

  // TODO: Only compute FFT every 0.1s or so
  private float CalculateAddedRunningScoreFft() {
    if (_fft == null) {
      _fft = new(FFT_BUFFER_SIZE);
      _lastFftUpdate = Time.timeAsDouble;
    }

    var y = MarrowGame.xr.HMD.Position.y;
    System.Numerics.Complex[] result = null;
    while (Math.Abs(_lastFftUpdate - Time.timeAsDouble) >
           FFT_HALF_SAMPLE_DURATION) {
      result = _fft.Add(y);
      _lastFftUpdate += FFT_SAMPLE_DURATION;
    }
    if (result == null)
      return _lastFftResult;

    var totalScore = 0f;
    for (var i = HEAD_BOB_MIN_FFT_BIN; i <= HEAD_BOB_MAX_FFT_BIN; i++) {
      var binDuration = FFT_WINDOW_DURATION / i;
      var binAmplitude = (float)result[i].Magnitude / FFT_WINDOW_DURATION;
      var binThreshold =
          (binDuration - HEAD_BOB_BASE_TIME) / HEAD_BOB_METERS_PER_SECOND;
      if (binAmplitude > binThreshold)
        totalScore += (binAmplitude - binThreshold) * binDuration;
    }

    // TODO: Sigmoid activation
    return totalScore * 2f;
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
