using System;
using UnityEngine;
using SLZ.Rig;
using Sst.Utilities;

namespace Sst.HandTracking;

public abstract class Locomotion {
  public Vector2 Axis;
  public abstract void Init(HandTracker tracker);
  public abstract void Update();
}

public class HandLocomotion : Locomotion {
  private const float DEFAULT_HEIGHT = 1.75f;
  private const float CONFIDENCE_BUILD_RATE = 4f;
  private const float CONFIDENCE_DRAIN_RATE = 1f;
  private const float VELOCITY_MIN = 1f;
  private const float VELOCITY_MAX = 2f;
  private const float VELOCITY_FACTOR = 1f / (VELOCITY_MAX - VELOCITY_MIN);
  private const float DIVERGENCE_MIN = 0.4f;
  private const float DIVERGENCE_MAX = 1.4f;
  private const float DIVERGENCE_FACTOR =
      1f / (DIVERGENCE_MAX - DIVERGENCE_MIN);
  private const float HEIGHT_MIN = 0.2f;
  private const float HEIGHT_MAX = 0.5f;
  private const float HEIGHT_FACTOR = 1f / (HEIGHT_MAX - HEIGHT_MIN);

  private LocoHandState _left;
  private LocoHandState _right;
  private float _lastCorrespondence = 0f;
  private float _confidence = 0f;

  public override void Init(HandTracker tracker) {
    var state = new LocoHandState() { Tracker = tracker };
    if (tracker.Opts.isLeft) {
      _left = state;
    } else {
      _right = state;
    }
  }

  public override void Update() {
    var isConfident = _left.IsTrackedConfident && _right.IsTrackedConfident;

    var player =
        LevelHooks.RigManager?.controllerRig.TryCast<OpenControllerRig>()
            ?.player;
    var scale = player != null ? DEFAULT_HEIGHT / player.realWorldHeight : 1f;

    _left.Update(scale);
    _right.Update(scale);

    var (stateMax, stateMin) =
        Mathf.Abs(_left.Velocity) > Mathf.Abs(_right.Velocity)
        ? (_left, _right)
        : (_right, _left);
    var scoreMaxVelocity =
        (Mathf.Abs(stateMax.Velocity) - VELOCITY_MIN) * VELOCITY_FACTOR;
    var scoreCorrespondence = isConfident
        ? Mathf.Clamp01(
              (DIVERGENCE_MAX - Mathf.Abs(stateMax.Velocity + stateMin.Velocity)
              ) *
              DIVERGENCE_FACTOR
          )
        : _lastCorrespondence;
    var scoreSwing = Mathf.Clamp01(
        (Mathf.Max(_left.SwingSize, _right.SwingSize) - HEIGHT_MIN) *
        HEIGHT_FACTOR
    );

    _lastCorrespondence = scoreCorrespondence;

    var confidenceChangeRate = scoreMaxVelocity * scoreCorrespondence *
            scoreSwing * CONFIDENCE_BUILD_RATE -
        CONFIDENCE_DRAIN_RATE;
    if (isConfident) {
      _confidence = Mathf.Clamp(
          _confidence + confidenceChangeRate * Time.deltaTime, 0f, 1.2f
      );
    }

    Axis = new Vector2(0f, Mathf.Clamp01(_confidence));
  }
}

public class LocoHandState {
  public HandTracker Tracker;
  public bool IsTrackedConfident;
  public float Velocity;
  public float SwingSize;

  private float _predictedNextHeight;
  private float _height;
  private float _minHeight;
  private float _maxHeight;
  private float _lastTime;

  public void Update(float scale) {
    // TODO: Is there something else to say hand position is low confidence?
    IsTrackedConfident = Tracker.IsControllerConnected() ||
        Tracker.HandState.IsActive() && Tracker.HandState.HasState &&
            Tracker.HandState.HandConfidence ==
                OVRPlugin.TrackingConfidence.High;
    if (!IsTrackedConfident || Time.deltaTime == 0f)
      return;

    var elapsed = Time.unscaledTime - _lastTime;
    _lastTime = Time.unscaledTime;

    var prevHeight = _height;
    _height = scale *
        (Tracker.HandState.IsActive() ? Tracker.ProxyController
                                      : Tracker.Opts.marrowController)
            .Position.y;

    var prevVelocity = Velocity;
    Velocity = (_height - prevHeight) / Time.deltaTime;

    if (elapsed < 0.2f) {
      if (prevVelocity <= 0f && Velocity > 0f) {
        _minHeight = _predictedNextHeight;
      } else if (prevVelocity >= 0f && Velocity < 0f) {
        _maxHeight = _predictedNextHeight;
      }
      SwingSize = _maxHeight - _minHeight;
    }

    _predictedNextHeight = _height + Velocity;
  }
}
