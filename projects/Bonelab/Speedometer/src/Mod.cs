using MelonLoader;
using UnityEngine;
using SLZ.Marrow.Warehouse;
using SLZ.Rig;

namespace Sst {
public class Mod : MelonMod {
  private const string SPEEDOMETER_TEXT_NAME = "SpeedrunTools_Speedometer_Text";

  private MelonPreferences_Entry<float> _prefWindowDuration;
  private TMPro.TextMeshPro _tmp;
  private SpeedTracker _speedTracker;
  private RigManager _rigManager;

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);

    var category = MelonPreferences.CreateCategory(BuildInfo.NAME);
    _prefWindowDuration = category.CreateEntry(
        "windowDuration", 0.1f, "Number of seconds to average the speed over");

    Utilities.LevelHooks.OnLevelStart.AddListener(
        new System.Action<LevelCrate>(OnLevelStart));
  }

  public void OnLevelStart(LevelCrate level) {
    var windowDuration = _prefWindowDuration.Value;
    _speedTracker = new SpeedTracker() {
      WindowDuration = windowDuration,
      BufferSize = Mathf.CeilToInt(windowDuration * 240),
    };

    _rigManager = Utilities.Bonelab.GetRigManager();
    var speedometerText = new GameObject(SPEEDOMETER_TEXT_NAME);
    _tmp = speedometerText.AddComponent<TMPro.TextMeshPro>();
    _tmp.alignment = TMPro.TextAlignmentOptions.BottomRight;
    _tmp.fontSize = 0.5f;
    _tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.5f);
    speedometerText.transform.SetParent(
        _rigManager.ControllerRig.leftController.transform);
    _tmp.rectTransform.localPosition = new Vector3(-0.36f, 0.24f, 0f);
    _tmp.rectTransform.localRotation = Quaternion.Euler(46f, 356f, 3f);
  }

  public override void OnUpdate() {
    Utilities.LevelHooks.OnUpdate();

    if (Utilities.LevelHooks.IsLoading || _speedTracker == null)
      return;

    var pos = _rigManager.physicsRig.m_head.position;
    _speedTracker.OnFrame(Time.time, pos.x, pos.z);
    _tmp?.SetText($"{_speedTracker.GetSpeed():N2}m/s");
  }
}

class SpeedTracker {
  public int BufferSize = 1000;
  public float WindowDuration = 1;

  private (float, float, float)[] _frames;
  private int _idxStart = 0;
  private int _idxEnd = 0;

  public SpeedTracker() { _frames = new(float, float, float)[BufferSize]; }

  public void OnFrame(float time, float posX, float posZ) {
    _frames[_idxEnd] = (time, posX, posZ);
    _idxEnd++;
    if (_idxEnd >= _frames.Length)
      _idxEnd = 0;
    if (_idxEnd == _idxStart) {
      _idxStart++;
      if (_idxStart >= _frames.Length)
        _idxStart = 0;
    }
  }

  public float GetSpeed() {
    if (_idxEnd == _idxStart)
      return 0;
    var frameEnd = _frames[(_idxEnd <= 0 ? _frames.Length : _idxEnd) - 1];
    var windowStart = frameEnd.Item1 - WindowDuration;
    while (_idxStart != _idxEnd && _frames[_idxStart].Item1 < windowStart) {
      _idxStart++;
      if (_idxStart >= _frames.Length)
        _idxStart = 0;
    }
    var frameStart = _frames[_idxStart];
    var dx = frameEnd.Item2 - frameStart.Item2;
    var dz = frameEnd.Item3 - frameStart.Item3;
    var duration = frameEnd.Item1 - frameStart.Item1;
    return Mathf.Sqrt(dx * dx + dz * dz) / duration;
  }
}
}
