using System;
using System.Diagnostics;
using StressLevelZero.Player;
using StressLevelZero.Rig;
using UnityEngine;
using Valve.VR;

namespace Sst.BoneworksPerformance;

internal static class JumpButtonTiming {
  public static BaseController Controller(ControllerRig rig) =>
      !rig ? null : rig.isRightHanded
          ? rig.rightController
          : rig.leftController;

  public static bool UsesAButton(BaseController controller) =>
      controller && controller.controllerType !=
      ControllerInfo.Type.VIVE_WANDS && controller.controllerType !=
      ControllerInfo.Type.UNDEFINED;

  public static SteamVR_Action_Boolean Action(BaseController controller) {
    if (!UsesAButton(controller))
      return null;
    return controller.controllerType == ControllerInfo.Type.OCCULUS_TOUCH
        ? SteamVR_Actions.oculustouch_AClick
        : SteamVR_Actions.default_AClick;
  }

  public static SteamVR_Input_Sources Source(ControllerRig rig) =>
      rig != null && rig.isRightHanded
          ? SteamVR_Input_Sources.RightHand
          : SteamVR_Input_Sources.LeftHand;

  public static float ReadChangedTime(ControllerRig rig) {
    try {
      var action = Action(Controller(rig));
      return action == null ? 0f : action.GetTimeLastChanged(Source(rig));
    } catch {
      return 0f;
    }
  }

  public static long ReadEdgeTimestamp(
      ControllerRig rig, long observedTimestamp, float observedRealtime
  ) {
    TryReadEdgeTimestamp(
        rig, observedTimestamp, observedRealtime, out var timestamp
    );
    return timestamp;
  }

  public static bool TryReadEdgeTimestamp(
      ControllerRig rig, long observedTimestamp, float observedRealtime,
      out long timestamp
  ) {
    var eventRealtime = ReadChangedTime(rig);
    var age = observedRealtime - eventRealtime;
    if (eventRealtime <= 0f || age < -0.1f || age > 10f) {
      timestamp = observedTimestamp;
      return false;
    }
    timestamp = observedTimestamp -
        (long)Math.Round(age * Stopwatch.Frequency);
    return true;
  }

  public static long UnityRealtimeToStopwatch(
      float eventRealtime, float observedRealtime, long observedTimestamp
  ) {
    var age = observedRealtime - eventRealtime;
    if (eventRealtime <= 0f || age < -0.1f || age > 10f)
      return observedTimestamp;
    return observedTimestamp - (long)Math.Round(age * Stopwatch.Frequency);
  }
}
