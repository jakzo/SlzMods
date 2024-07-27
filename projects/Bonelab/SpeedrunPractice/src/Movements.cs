using System;
using System.Collections;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using SLZ.Rig;
using SLZ.Marrow.Utilities;
using SLZ.Marrow.Input;

namespace Sst.SpeedrunPractice.Movements {
public abstract class Movement {
  public abstract IEnumerator Start();

  public float time = 0f;
}

public class Measurer {
  private const float WINDOW_DURATION = 1f;
  private const float STOPPED_DIST = 0.1f;

  private Vector3? _startPos;
  private float _windowStartTime;
  private Vector3 _windowStartPos;
  private float _maxDistSqr;
  private float _maxHeight;

  public void Start() {
    _startPos = Utils.State.rigManager.physicsRig.feet.transform.position;
    _maxHeight = 0f;
    StartWindow();
  }

  public void OnUpdate() {
    if (!_startPos.HasValue)
      return;

    UpdateMaximums();

    if (Time.time - _windowStartTime < WINDOW_DURATION)
      return;

    if (IsStopped()) {
      Finish();
    } else {
      StartWindow();
    }
  }

  private void StartWindow() {
    _windowStartTime = Time.time;
    _windowStartPos = Utils.State.rigManager.physicsRig.m_head.position;
    _maxDistSqr = 0f;
  }

  private void UpdateMaximums() {
    var headPos = Utils.State.rigManager.physicsRig.m_head.position;

    var distSqr = (headPos - _windowStartPos).sqrMagnitude;
    if (distSqr > _maxDistSqr)
      _maxDistSqr = distSqr;

    var height = headPos.y - _startPos.Value.y;
    if (height > _maxHeight)
      _maxHeight = height;
  }

  private bool IsStopped() => _maxDistSqr <= STOPPED_DIST * STOPPED_DIST;

  private void Finish() {
    var distFromStart =
        (Utils.State.rigManager.physicsRig.feet.transform.position -
         _startPos.Value)
            .magnitude;
    MelonLogger.Msg(
        $"Movement: dist from start = {distFromStart.ToString("F1")}, max height = {_maxHeight.ToString("F1")}"
    );
    _startPos = null;
  }
}

public class Mover {
  public static Func<Movement> ActiveMovement = () => new SuperJump();

  private static Movement _currentMovement;
  private static IEnumerator _currentMovementEnumerator;
  private static float _startTime;
  private static Measurer _measurer = new Measurer();

  public static void PerformMovement(Movement movement) {
    if (_currentMovement != null)
      return;
    _currentMovement = movement;
  }

  public static void PerformMovementAndMeasure(Movement movement) {
    if (_currentMovement != null)
      return;
    _currentMovement = movement;
    _measurer.Start();
  }

  public static void SetInputsFromCurrentMovement() {
    if (!Features.ScriptedMovement.Instance.IsEnabled ||
        ActiveMovement == null || !Utils.State.rigManager)
      return;

    var leftController = MarrowGame.xr.LeftController;
    if (_currentMovement == null &&
        (leftController.AButtonDown || leftController.AButton)) {
      var newMovement = ActiveMovement();
      MelonLogger.Msg($"Performing movement: {newMovement}");
      PerformMovement(newMovement);
    }

    leftController.AButton = false;
    leftController.AButtonDown = false;
    leftController.AButtonUp = false;

    if (_currentMovement == null) {
      _measurer.OnUpdate();
      return;
    }

    if (_currentMovementEnumerator == null) {
      _startTime = Time.unscaledTime;
      _currentMovement.time = 0f;
      _currentMovementEnumerator = _currentMovement.Start();
    } else {
      _currentMovement.time = Time.unscaledTime - _startTime;
      if (!_currentMovementEnumerator.MoveNext()) {
        _currentMovement = null;
        _currentMovementEnumerator = null;
        MelonLogger.Msg("Movement done");
      }
    }
  }
}

[HarmonyPatch(typeof(OpenController), nameof(OpenController.OnUpdate))]
class OpenController_OnUpdate_Patch {
  [HarmonyPrefix()]
  internal static void Prefix() {
    Mover.SetInputsFromCurrentMovement();
    ControllerSetter.Left.OnUpdate();
    ControllerSetter.Right.OnUpdate();
  }
}

[HarmonyPatch(typeof(OpenController), nameof(OpenController.OnVrFixedUpdate))]
class OpenController_OnVrFixedUpdate_Patch {
  [HarmonyPrefix()]
  internal static void Prefix() { Mover.SetInputsFromCurrentMovement(); }
}

public class ControllerSetter {
  public static ControllerSetter Left = new ControllerSetter() {
    GetController = () => MarrowGame.xr.LeftController,
  };
  public static ControllerSetter Right = new ControllerSetter() {
    GetController = () => MarrowGame.xr.RightController,
  };

  public Func<XRController> GetController;
  private XRController _controller { get => GetController(); }

  public void OnUpdate() {
    if (ADown) {
      if (!_a) {
        _controller.AButtonDown = true;
        _a = true;
      }
      ADown = false;
    }
    if (AUp) {
      if (_a) {
        _controller.AButtonUp = true;
        _a = false;
      }
      AUp = false;
    }
    if (_a)
      _controller.AButton = _a;

    if (BDown) {
      if (!_b) {
        _controller.BButtonDown = true;
        _b = true;
      }
      BDown = false;
    }
    if (BUp) {
      if (_b) {
        _controller.BButtonUp = true;
        _b = false;
      }
      BUp = false;
    }
    if (_b)
      _controller.BButton = _b;

    if (ThumbDown) {
      if (!_thumb) {
        _controller.JoystickButtonDown = true;
        _thumb = true;
      }
      ThumbDown = false;
    }
    if (ThumbUp) {
      if (_thumb) {
        _controller.JoystickButtonUp = true;
        _thumb = false;
      }
      ThumbUp = false;
    }
    if (_thumb)
      _controller.JoystickButton = _thumb;
  }

  private bool _a;
  public bool ADown;
  public bool AUp;

  private bool _b;
  public bool BDown;
  public bool BUp;

  private bool _thumb;
  public bool ThumbDown;
  public bool ThumbUp;
}

public class SuperJump : Movement {
  public override IEnumerator Start() {
    ControllerSetter.Right.ADown = true;
    while (time < 1f) {
      MarrowGame.xr.RightController.Joystick2DAxis = new Vector2(0f, -1f);
      yield return null;
    }
    ControllerSetter.Right.AUp = true;
  }
}
}

// === Test movement in Unity Explorer:

// 1. Copy-paste usings from top of this file

// 2. Copy-paste code below and modify as desired
namespace Sst.SpeedrunPractice.Movements {
public class TestMovement : Movement {
  public override IEnumerator Start() {
    ControllerSetter.Right.ADown = true;
    ControllerSetter.Left.ThumbDown = true;
    while (time < 1f) {
      MarrowGame.xr.LeftController.Joystick2DAxis = new Vector2(0f, 1f);
      MarrowGame.xr.RightController.Joystick2DAxis = new Vector2(0f, -1f);
      yield return null;
    }
    ControllerSetter.Right.AUp = true;
    while (time < 2f) {
      MarrowGame.xr.LeftController.Joystick2DAxis = new Vector2(0f, 1f);
    }
    ControllerSetter.Left.ThumbUp = true;
  }
}
}

// 3. Copy-paste code below
/*
Sst.SpeedrunPractice.Movements.Mover.PerformMovement(new
Sst.SpeedrunPractice.Movements.TestMovement());

Sst.SpeedrunPractice.Movements.Mover.PerformMovementAndMeasure(new
Sst.SpeedrunPractice.Movements.TestMovement());
*/
