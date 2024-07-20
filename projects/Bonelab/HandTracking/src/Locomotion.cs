using System;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace Sst.HandTracking;

public class LocoState {
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

  public Vector2? Axis;

  private LocoHandState _left;
  private LocoHandState _right;
  private float _confidence = 0f;

  public void Init(HandTracker tracker) {
    var state = new LocoHandState() { Tracker = tracker };
    if (tracker.Opts.isLeft) {
      _left = state;
    } else {
      _right = state;
    }
    tracker.LocoState = this;
  }

  public void Update() {
    // Apparently the game just swaps the MarrowGame.xr controllers when the
    // left handed setting is enabled so the left tracker always has the
    // locomotion stick
    var locoTracker = _left.Tracker;
    if (locoTracker.IsControllerConnected() || _left == null ||
        _right == null) {
      Axis = null;
      return;
    }

    _left.Update();
    _right.Update();

    var (stateMax, stateMin) =
        Mathf.Abs(_left.Velocity) > Mathf.Abs(_right.Velocity)
            ? (_left, _right)
            : (_right, _left);
    var scoreMaxVelocity =
        (Mathf.Abs(stateMax.Velocity) - VELOCITY_MIN) * VELOCITY_FACTOR;
    var scoreCorrespondence = Mathf.Clamp01(
        (DIVERGENCE_MAX - Mathf.Abs(stateMax.Velocity + stateMin.Velocity)) *
        DIVERGENCE_FACTOR);
    var scoreSwing = Mathf.Clamp01(
        (Mathf.Max(_left.SwingSize, _right.SwingSize) - HEIGHT_MIN) *
        HEIGHT_FACTOR);

    // TODO: Allow one handed running after building confidence
    var confidenceChangeRate = scoreMaxVelocity * scoreCorrespondence *
                                   scoreSwing * CONFIDENCE_BUILD_RATE -
                               CONFIDENCE_DRAIN_RATE;
    _confidence = Mathf.Clamp(
        _confidence + confidenceChangeRate * Time.deltaTime, 0f, 1.2f);

    // Dbg.Log(string.Join(
    //     ", ",
    //     new[] {
    //       ("c", _confidence),
    //       ("sts", stateMax.SwingSize),
    //       ("stv", stateMax.Velocity),
    //       ("smv", scoreMaxVelocity),
    //       ("sc", scoreCorrespondence),
    //       ("ss", scoreSwing),
    //     }
    //         .Select(x => $"{x.Item1}={x.Item2.ToString(" 0.00;-0.00")}")));

    Axis = new Vector2(0f, Mathf.Clamp01(_confidence));
  }
}

public class LocoHandState {
  public HandTracker Tracker;
  public float Velocity;
  public float SwingSize;

  private bool _isTracked;
  private float _predictedNextHeight;
  public float _height;
  private float _minHeight;
  private float _maxHeight;

  // TODO: Continue running with low confidence hand states
  public void Update() {
    _isTracked =
        Tracker.IsControllerConnected() || Tracker.HandState.IsActive();
    if (!_isTracked || Time.deltaTime == 0f)
      return;

    var prevHeight = _height;
    // TODO: Convert to game units (meters if player is 1.78m tall)
    _height = (Tracker.HandState.IsActive() ? Tracker.ProxyController
                                            : Tracker.Opts.marrowController)
                  .Position.y;

    var prevVelocity = Velocity;
    Velocity = (_height - prevHeight) / Time.deltaTime;

    if (prevVelocity <= 0f && Velocity > 0f) {
      _minHeight = _predictedNextHeight;
    } else if (prevVelocity >= 0f && Velocity < 0f) {
      _maxHeight = _predictedNextHeight;
    }
    SwingSize = _maxHeight - _minHeight;

    _predictedNextHeight = _height + Velocity;
  }
}
