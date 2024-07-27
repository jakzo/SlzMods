using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using SLZ.Rig;
using SLZ.Marrow.Utilities;
using Sst.Utilities;

namespace Sst.HandTracking;

public class Jumping {
  private const float WINDOW_DURATION = 0.3f;
  private const float HEIGHT_THRESHOLD = 0.3f;

  private Queue<(float Time, float Y)> _minHmdHeightInWindow = new();
  private float _lastAddedHmdHeight;
  private bool _isJumping = false;

  public void Init() {}

  // TODO: Handle short jumps and long uncrouches properly
  public void Update() {
    var y = MarrowGame.xr.HMD.Position.y;
    var windowStart = Time.unscaledTime - WINDOW_DURATION;

    while (_minHmdHeightInWindow.Count > 0 &&
           _minHmdHeightInWindow.Peek().Time < windowStart) {
      _minHmdHeightInWindow.Dequeue();
    }

    if (_minHmdHeightInWindow.Count == 0 || y > _lastAddedHmdHeight) {
      _minHmdHeightInWindow.Enqueue((Time.unscaledTime, y));
      _lastAddedHmdHeight = y;
    }

    if (_isJumping) {
      if (_minHmdHeightInWindow.Count <= 1)
        _isJumping = false;
    } else if (_minHmdHeightInWindow.Peek().Y <= y - HEIGHT_THRESHOLD) {
      _isJumping = true;
      JumpPressed();
    }
  }

  public void JumpPressed() {
    if (LevelHooks.RigManager == null)
      return;

    var seat = LevelHooks.RigManager.activeSeat;
    if (seat) {
      seat.EgressRig(false);
      Dbg.Log("Ejecting from seat");
      return;
    }

    LevelHooks.RigManager.remapHeptaRig.Jump();
    Dbg.Log("Jumping");
  }
}
