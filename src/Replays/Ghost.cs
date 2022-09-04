using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SpeedrunTools.Replays {
class Ghost {
  public Replay Replay;
  public bool IsPlaying = false;
  public bool IsPaused = false;
  public bool IsFinishedPlaying = false;
  public GhostRig Rig;

  private FrameReader _frameReader;
  private float _relativeStartTime;
  private int _levelIdx;
  private int _frameIdx;
  private float? _loadStartTime;
  private Bwr.Frame _frameCur;
  private Bwr.Frame? _frameNext;

  public Ghost(Replay replay) {
    Replay = replay;
    _frameReader = replay.CreateFrameReader();
  }

  public void Start() {
    if (IsPlaying)
      return;

    var (l1, f1) = _frameReader.Read();
    if (l1.HasValue)
      _levelIdx = l1.Value;
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
    if (l2.HasValue)
      _levelIdx = l2.Value;
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

  public void OnLoadingScreen() { _loadStartTime = Time.time; }

  public void OnSceneChange() {
    if (_loadStartTime.HasValue) {
      _relativeStartTime += Time.time - _loadStartTime.Value;
      _loadStartTime = null;
    }
  }

  public void OnUpdate(int currentSceneIdx) {
    // Pause playback during loading screens
    if (!IsPlaying || _loadStartTime.HasValue)
      return;

    // Seek forward until _frameCur is <= now && _frameNext is > now (or none at
    // end)
    var time = Time.time - _relativeStartTime;
    var hasLevelChanged = false;
    while (_frameNext?.Time <= time) {
      _frameCur = _frameNext.Value;
      int? nextLevelIdx = null;
      (nextLevelIdx, _frameNext) = _frameReader.Read();
      if (nextLevelIdx.HasValue) {
        _levelIdx = nextLevelIdx.Value;
        hasLevelChanged = true;
      }
    }
    if (!_frameNext.HasValue) {
      Pause();
      IsFinishedPlaying = true;
      Utils.LogDebug("Finished playing");
      return;
    }

    // Render nothing if ghost is not in the current scene
    if (hasLevelChanged || currentSceneIdx != _levelIdx) {
      if (Rig != null) {
        Object.Destroy(Rig.Root);
        Rig = null;
      }
      return;
    }

    // Render head lerped between prev and next frames
    var ghostsContainer = GameObject.Find("SpeedrunTools_Ghosts");
    if (ghostsContainer == null)
      throw new System.Exception("Ghost container not found");
    if (Rig == null)
      Rig = GhostRig.Create(ghostsContainer.transform, Color.blue);
    var t = (time - _frameCur.Time) / (_frameNext.Value.Time - _frameCur.Time);
    Rig.Head.position = Vector3.Lerp(
        ToUnityVec3(_frameCur.VrInput.Value.PlayerPosition.Headset.Position),
        ToUnityVec3(
            _frameNext.Value.VrInput.Value.PlayerPosition.Headset.Position),
        t);
    Rig.Head.rotation = Quaternion.Lerp(
        ToUnityQuaternion(
            _frameCur.VrInput.Value.PlayerPosition.Headset.RotationEuler),
        ToUnityQuaternion(_frameNext.Value.VrInput.Value.PlayerPosition.Headset
                              .RotationEuler),
        t);
  }

  static private Vector3
  ToUnityVec3(Bwr.Vector3 vec) => new Vector3(vec.X, vec.Y, vec.Z);
  static private Quaternion
  ToUnityQuaternion(Bwr.Vector3 vec) => Quaternion.Euler(vec.X, vec.Y, vec.Z);
}
}
