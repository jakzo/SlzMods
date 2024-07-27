using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using StressLevelZero.Rig;

namespace Sst.Replays {
class Ghost {
  const string GHOSTS_CONTAINER_NAME = "SpeedrunTools_Ghosts";
  readonly static float TRANSPARENCY_THRESHOLD_FAR = 4;
  readonly static float TRANSPARENCY_THRESHOLD_NEAR = 2;

  public Replay Replay;
  public bool IsPlaying = false;
  public bool IsPaused = false;
  public bool IsFinishedPlaying = false;
  public System.Func<Transform, GhostRig> CreateRig;
  public bool HideWhenNear;
  public GhostRig Rig;
  public Color GhostColor;

  private FrameReader _frameReader;
  private float _relativeStartTime;
  private int _levelIdxCur;
  private int _levelIdxNext;
  private float? _loadStartTime;
  private Bwr.Frame _frameCur;
  private Bwr.Frame? _frameNext;

  private float _transparencyThresholdFar;
  private float _transparencyThresholdNear;

  public Ghost(
      Replay replay, bool hideWhenNear, Color ghostColor,
      System.Func<Transform, GhostRig> createRig
  ) {
    Replay = replay;
    HideWhenNear = hideWhenNear;
    GhostColor = ghostColor;
    CreateRig = createRig;

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
      Rig.Destroy();
      Rig = null;
    }
  }

  public void PlayFromSceneIndex(int sceneIdx) {
    for (var i = 0; i < Replay.Metadata.LevelsLength; i++) {
      var level = Replay.Metadata.Levels(i).Value;
      if (level.SceneIndex == sceneIdx) {
        PlayFromLevel(level);
        return;
      }
    }
    IsPlaying = IsPaused = IsFinishedPlaying = false;
  }
  public void PlayFromLevel(Bwr.Level level) {
    IsPlaying = true;
    IsPaused = IsFinishedPlaying = false;
    _relativeStartTime = Time.time - level.StartTime;
    _frameReader.Seek(level.FrameOffset);
    (_, _frameNext) = _frameReader.Read();
    if (!_frameNext.HasValue) {
      Stop();
      return;
    }
    _frameCur = _frameNext.Value;
    _levelIdxCur = level.SceneIndex;
    int? levelIdx = null;
    (levelIdx, _frameNext) = _frameReader.Read();
    _levelIdxNext = levelIdx ?? _levelIdxCur;
  }

  public void OnLoadingScreen() {
    _loadStartTime = Time.time;
    if (Rig != null) {
      Rig.Destroy();
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
    var time = Time.time - _relativeStartTime;
    _OnUpdate(time);

    if (Rig == null)
      return;

    var position = _frameNext.HasValue
        ? Vector3.Lerp(
              GhostRig.ToUnityVec3(_frameCur.PlayerState.Value.HeadPosition),
              GhostRig.ToUnityVec3(
                  _frameNext.Value.PlayerState.Value.HeadPosition
              ),
              (time - _frameCur.Time) / (_frameNext.Value.Time - _frameCur.Time)
          )
        : GhostRig.ToUnityVec3(_frameCur.PlayerState.Value.HeadPosition);

    var rigManager = Mod.GameState.rigManager;
    var sqrDist = rigManager != null
        ? (position - rigManager.ControllerRig.hmdTransform.position)
              .sqrMagnitude
        : 1000;
    if (sqrDist < _transparencyThresholdNear) {
      if (Rig.IsVisible) {
        Rig.SetVisible(false);
        Rig.IsVisible = false;
      }
      return;
    }
    if (!Rig.IsVisible) {
      Rig.SetVisible(true);
      Rig.IsVisible = true;
    }

    var newAlpha =
        Mathf.Min(
            (sqrDist - _transparencyThresholdNear) / _transparencyThresholdFar,
            1f
        ) /
        2f;
    if (Rig.color.a != newAlpha) {
      var newColor = new Color(Rig.color.r, Rig.color.g, Rig.color.b, newAlpha);
      Rig.SetColor(newColor);
      Rig.color = newColor;
    }
  }

  private void _OnUpdate(float time) {
    // Pause playback during loading screens
    if (!IsPlaying || _loadStartTime.HasValue)
      return;

    // Seek forward until _frameCur is <= now && _frameNext is > now (or none at
    // end)
    while (_frameNext?.Time <= time) {
      _frameCur = _frameNext.Value;
      _levelIdxCur = _levelIdxNext;
      int? nextLevelIdx = null;
      (nextLevelIdx, _frameNext) = _frameReader.Read();
      if (nextLevelIdx.HasValue)
        _levelIdxNext = nextLevelIdx.Value;
    }
    if (!_frameNext.HasValue) {
      Pause();
      IsFinishedPlaying = true;
      Dbg.Log("Finished playing");
      return;
    }

    // Render nothing if ghost is not in the current scene
    if (_levelIdxCur != _levelIdxNext ||
        Mod.GameState.currentSceneIdx != _levelIdxCur) {
      if (Rig != null) {
        Rig.Destroy();
        Rig = null;
      }
      return;
    }

    // Render ghost lerped between current and next frames
    var ghostsContainer = GameObject.Find(GHOSTS_CONTAINER_NAME);
    if (ghostsContainer == null)
      ghostsContainer = new GameObject(GHOSTS_CONTAINER_NAME);
    if (Rig == null) {
      Rig = CreateRig(ghostsContainer.transform);
      Rig.IsVisible = true;
      Rig.color = GhostColor;
      Rig.SetColor(Rig.color);
    }
    var t = (time - _frameCur.Time) / (_frameNext.Value.Time - _frameCur.Time);
    Rig.SetState(_frameCur, _frameNext.Value, t);
  }
}

public abstract class GhostRig {
  public bool IsVisible;
  public Color color;
  public abstract void SetColor(Color color);
  public abstract void
  SetState(Bwr.Frame currentFrame, Bwr.Frame nextFrame, float t);
  public abstract void SetVisible(bool isVisible);
  public abstract void Destroy();

  public static Vector3 ToUnityVec3(Bwr.Vector3 vec
  ) => new Vector3(vec.X, vec.Y, vec.Z);
  public static Quaternion ToUnityQuaternion(
      Bwr.Vector3 vec, float yOffset = 0
  ) => Quaternion.Euler(vec.X, vec.Y + yOffset, vec.Z);
}
}
