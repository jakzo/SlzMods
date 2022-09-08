using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using StressLevelZero.Rig;

namespace SpeedrunTools.Replays {
class Ghost {
  const string GHOSTS_CONTAINER_NAME = "SpeedrunTools_Ghosts";
  readonly static float TRANSPARENCY_THRESHOLD_FAR = 4;
  readonly static float TRANSPARENCY_THRESHOLD_NEAR = 2;

  public Replay Replay;
  public bool IsPlaying = false;
  public bool IsPaused = false;
  public bool IsFinishedPlaying = false;
  public bool HideWhenNear;
  public GhostRig Rig;

  private FrameReader _frameReader;
  private float _relativeStartTime;
  private int _levelIdxCur;
  private int _levelIdxNext;
  private float? _loadStartTime;
  private Bwr.Frame _frameCur;
  private Bwr.Frame? _frameNext;

  private float _transparencyThresholdFar;
  private float _transparencyThresholdNear;

  public Ghost(Replay replay, bool hideWhenNear) {
    Replay = replay;
    HideWhenNear = hideWhenNear;
    _frameReader = replay.CreateFrameReader();

    _transparencyThresholdNear =
        TRANSPARENCY_THRESHOLD_NEAR * TRANSPARENCY_THRESHOLD_NEAR;
    _transparencyThresholdFar =
        TRANSPARENCY_THRESHOLD_FAR * TRANSPARENCY_THRESHOLD_FAR -
        _transparencyThresholdNear;
  }

  public void Start() {
    if (IsPlaying)
      return;

    var (l1, f1) = _frameReader.Read();
    _levelIdxCur = l1 ?? 0;
    if (f1.HasValue) {
      _frameCur = f1.Value;
    } else {
      IsFinishedPlaying = true;
      IsPlaying = IsPaused = false;
      return;
    }

    IsPlaying = true;
    IsPaused = IsFinishedPlaying = false;
    _relativeStartTime = Time.time;

    var (l2, f2) = _frameReader.Read();
    _levelIdxNext = l2 ?? _levelIdxCur;
    if (f2.HasValue)
      _frameNext = f2.Value;
  }

  public void Pause() {
    IsPlaying = false;
    IsPaused = true;
  }

  public void Stop() {
    IsPlaying = false;
    IsPaused = false;
    IsFinishedPlaying = false;
    if (Rig != null) {
      Object.Destroy(Rig.Root);
      Rig = null;
    }
  }

  public void OnLoadingScreen() {
    _loadStartTime = Time.time;
    if (Rig != null) {
      Object.Destroy(Rig.Root);
      Rig = null;
    }
  }

  public void OnLevelStart() {
    if (_loadStartTime.HasValue) {
      _relativeStartTime += Time.time - _loadStartTime.Value;
      _loadStartTime = null;
    }
  }

  public void OnUpdate() {
    _OnUpdate();

    if (Rig == null)
      return;

    var ghostTransforms =
        new[] { Rig.Head, Rig.ControllerLeft, Rig.ControllerRight };
    var rigManager = Mod.GameState.rigManager;
    var sqrDist = rigManager != null
                      ? (Rig.Head.position -
                         rigManager.ControllerRig.hmdTransform.position)
                            .sqrMagnitude
                      : 1000;
    if (sqrDist < _transparencyThresholdNear) {
      foreach (var transform in ghostTransforms) {
        if (!transform.gameObject.active)
          break;
        transform.gameObject.active = false;
      }
      return;
    }

    var newAlpha = Mathf.Min((sqrDist - _transparencyThresholdNear) /
                                 _transparencyThresholdFar,
                             1f) /
                   2f;
    foreach (var transform in ghostTransforms) {
      transform.gameObject.active = true;
      var material = transform.GetComponent<MeshRenderer>().material;
      if (material.color.a == newAlpha)
        break;
      material.color = new Color(material.color.r, material.color.g,
                                 material.color.b, newAlpha);
    }
  }

  private void _OnUpdate() {
    // Pause playback during loading screens
    if (!IsPlaying || _loadStartTime.HasValue)
      return;

    // Seek forward until _frameCur is <= now && _frameNext is > now (or none at
    // end)
    var time = Time.time - _relativeStartTime;
    while (_frameNext?.Time <= time) {
      _frameCur = _frameNext.Value;
      _levelIdxCur = _levelIdxNext;
      int? nextLevelIdx = null;
      (nextLevelIdx, _frameNext) = _frameReader.Read();
      if (nextLevelIdx.HasValue) {
        _levelIdxNext = nextLevelIdx.Value;
      }
    }
    if (!_frameNext.HasValue) {
      Pause();
      IsFinishedPlaying = true;
      Utils.LogDebug("Finished playing");
      return;
    }

    // Render nothing if ghost is not in the current scene
    if (_levelIdxCur != _levelIdxNext ||
        Mod.GameState.currentSceneIdx != _levelIdxCur) {
      if (Rig != null) {
        Object.Destroy(Rig.Root);
        Rig = null;
      }
      return;
    }

    // Render head lerped between prev and next frames
    var ghostsContainer = GameObject.Find(GHOSTS_CONTAINER_NAME);
    if (ghostsContainer == null)
      ghostsContainer = new GameObject(GHOSTS_CONTAINER_NAME);
    if (Rig == null)
      Rig = GhostRig.Create(ghostsContainer.transform,
                            new Color(0.2f, 0.2f, 0.8f, 0.5f));
    var t = (time - _frameCur.Time) / (_frameNext.Value.Time - _frameCur.Time);
    Rig.Head.position = Vector3.Lerp(
        ToUnityVec3(_frameCur.PlayerState.Value.HeadPosition),
        ToUnityVec3(_frameNext.Value.PlayerState.Value.HeadPosition), t);
    Rig.Head.rotation = Quaternion.Lerp(
        ToUnityQuaternion(
            _frameCur.VrInput.Value.Headset.Transform.RotationEuler,
            _frameCur.PlayerState.Value.RootRotation),
        ToUnityQuaternion(
            _frameNext.Value.VrInput.Value.Headset.Transform.RotationEuler,
            _frameNext.Value.PlayerState.Value.RootRotation),
        t);
    foreach (var (controller, handCur, handNext) in new[] {
               (Rig.ControllerLeft, _frameCur.PlayerState.Value.LeftHand,
                _frameNext.Value.PlayerState.Value.LeftHand),
               (Rig.ControllerRight, _frameCur.PlayerState.Value.RightHand,
                _frameNext.Value.PlayerState.Value.RightHand),
             }) {
      controller.position = Vector3.Lerp(ToUnityVec3(handCur.Position),
                                         ToUnityVec3(handNext.Position), t);
      controller.rotation =
          Quaternion.Lerp(ToUnityQuaternion(handCur.RotationEuler),
                          ToUnityQuaternion(handNext.RotationEuler), t);
    }
  }

  static private Vector3
  ToUnityVec3(Bwr.Vector3 vec) => new Vector3(vec.X, vec.Y, vec.Z);
  static private Quaternion ToUnityQuaternion(Bwr.Vector3 vec,
                                              float yOffset = 0) =>
      Quaternion.Euler(vec.X, vec.Y + yOffset, vec.Z);
}
}
