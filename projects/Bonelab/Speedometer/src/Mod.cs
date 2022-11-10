using MelonLoader;
using UnityEngine;
using SLZ.Marrow.Warehouse;

namespace Sst.Speedometer {
public class Mod : MelonMod {
  private const string SPEEDOMETER_TEXT_NAME = "Speedometer";

  private enum Units { MS, KPH, MPH }

  private MelonPreferences_Entry<bool> _prefRightHand;
  private MelonPreferences_Entry<Units> _prefUnits;
  private MelonPreferences_Entry<float> _prefWindowDuration;
  private TMPro.TextMeshPro _tmp;
  private SpeedTracker _speedTracker;

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);

    var category = MelonPreferences.CreateCategory(BuildInfo.NAME);
    _prefRightHand = category.CreateEntry(
        "right_hand", false,
        "Show speed reading on right hand instead of left");
    _prefUnits =
        category.CreateEntry("units", Units.MS, "Units to measure speed in");
    _prefWindowDuration = category.CreateEntry(
        "window_duration", 0.1f, "Number of seconds to average the speed over");

    Utilities.LevelHooks.OnLevelStart += OnLevelStart;
  }

  public void OnLevelStart(LevelCrate level) {
    var windowDuration = _prefWindowDuration.Value;
    _speedTracker = new SpeedTracker() {
      WindowDuration = windowDuration,
      BufferSize = Mathf.CeilToInt(windowDuration * 240),
    };

    _tmp = Utilities.Bonelab.CreateTextOnWrist(SPEEDOMETER_TEXT_NAME,
                                               _prefRightHand.Value);
  }

  public override void OnUpdate() {
    if (Utilities.LevelHooks.IsLoading || _speedTracker == null)
      return;

    var pos = Utilities.LevelHooks.RigManager.physicsRig.m_head.position;
    _speedTracker.OnFrame(Time.time, pos.x, pos.z);
    _tmp?.SetText(GetSpeedText(_speedTracker.GetSpeed()));
  }

  private string GetSpeedText(float speedMs) {
    switch (_prefUnits.Value) {
    default:
    case Units.MS:
      return $"{speedMs:N2}m/s";
    case Units.KPH:
      return $"{(speedMs * 3.6f):N1}kph";
    case Units.MPH:
      return $"{(speedMs * 2.237f):N1}mph";
    }
  }
}
}
