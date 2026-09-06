using System;
using System.Runtime.InteropServices;
using StressLevelZero.Rig;
using TMPro;
using UnityEngine;
using Valve.VR;

namespace Sst.BoneworksPerformance;

internal sealed class PerformanceHud {
  private const string ObjectName = "BoneworksPerformance stats HUD";
  private const float UpdateFrequencySeconds = 0.05f;

  private readonly bool _enabled;
  private readonly RateTimer _updateTimer = new RateTimer();
  private readonly RateTimer _fixedTimer = new RateTimer();
  private TextMeshPro _text;
  private float _nextAttachAt;
  private float _nextTextUpdateAt;

  public PerformanceHud(bool enabled) {
    _enabled = enabled;
  }

  public void ResetScene() {
    _updateTimer.Reset();
    _fixedTimer.Reset();
    _nextAttachAt = 0f;
    _nextTextUpdateAt = 0f;
    DestroyText();
  }

  public void OnFixedUpdate() {
    if (_enabled)
      _fixedTimer.Add(Time.realtimeSinceStartup);
  }

  public void OnUpdate(bool jumpDetected, float inputTicksBehind) {
    if (!_enabled)
      return;
    var now = Time.realtimeSinceStartup;
    _updateTimer.Add(now);
    if (!_text && now >= _nextAttachAt) {
      _nextAttachAt = now + 0.5f;
      TryAttach();
    }
    if (!_text || now < _nextTextUpdateAt)
      return;
    _nextTextUpdateAt = now + UpdateFrequencySeconds;
    _text.SetText(
        "FPS: " + _updateTimer.GetRate(now).ToString("N1") +
        "\nFixed: " + _fixedTimer.GetRate(now).ToString("N1") +
        "\nVR client: " + ReadClientPoseRate() +
        "\nDisplay: " + ReadDisplayRate() +
        "\nJump detected: " + (jumpDetected
            ? "<color=#55ff55>YES</color>"
            : "<color=#ff7777>NO</color>") +
        "\nInput behind: " + (inputTicksBehind < 0f
            ? "--"
            : inputTicksBehind.ToString("N1") + " ticks")
    );
  }

  public void Shutdown() {
    DestroyText();
  }

  private void TryAttach() {
    var rig = UnityEngine.Object.FindObjectOfType<RigManager>();
    var controller = rig && rig.ControllerRig
        ? rig.ControllerRig.leftController
        : null;
    if (!controller)
      return;
    var gameObject = GameObject.Find(ObjectName);
    if (!gameObject)
      gameObject = new GameObject(ObjectName);
    _text = gameObject.GetComponent<TextMeshPro>();
    if (!_text)
      _text = gameObject.AddComponent<TextMeshPro>();
    _text.alignment = TextAlignmentOptions.BottomRight;
    _text.fontSize = 0.35f;
    _text.rectTransform.sizeDelta = new Vector2(0.9f, 1.08f);
    _text.transform.SetParent(controller.transform, false);
    _text.rectTransform.localPosition = new Vector3(-0.36f, 0.3f, 0.03f);
    _text.rectTransform.localRotation = Quaternion.Euler(46f, 356f, 3f);
  }

  private static string ReadClientPoseRate() {
    var compositor = OpenVR.Compositor;
    if (compositor == null)
      return "--";
    var timing = new Compositor_FrameTiming {
      m_nSize = (uint)Marshal.SizeOf(typeof(Compositor_FrameTiming)),
    };
    if (!compositor.GetFrameTiming(ref timing, 0) ||
        timing.m_flClientFrameIntervalMs <= 0f)
      return "--";
    return (1000f / timing.m_flClientFrameIntervalMs).ToString("N1") +
           " Hz";
  }

  private static string ReadDisplayRate() {
    var system = OpenVR.System;
    if (system == null)
      return "--";
    var error = ETrackedPropertyError.TrackedProp_Success;
    var rate = system.GetFloatTrackedDeviceProperty(
        0, ETrackedDeviceProperty.Prop_DisplayFrequency_Float, ref error
    );
    if (error != ETrackedPropertyError.TrackedProp_Success || rate <= 0f)
      return "--";
    return rate.ToString("N1") + " Hz";
  }

  private void DestroyText() {
    if (_text)
      UnityEngine.Object.Destroy(_text.gameObject);
    _text = null;
  }

  private sealed class RateTimer {
    private const float WindowSeconds = 1f;
    private readonly float[] _times = new float[1000];
    private int _start;
    private int _end;

    public void Reset() {
      _start = 0;
      _end = 0;
    }

    public void Add(float time) {
      _times[_end] = time;
      _end = (_end + 1) % _times.Length;
      if (_end == _start)
        _start = (_start + 1) % _times.Length;
    }

    public float GetRate(float now) {
      var windowStart = now - WindowSeconds;
      while (_start != _end && _times[_start] < windowStart)
        _start = (_start + 1) % _times.Length;
      var count = _end >= _start
          ? _end - _start
          : _times.Length - _start + _end;
      return count / WindowSeconds;
    }
  }
}
