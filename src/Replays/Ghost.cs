using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SpeedrunTools.Replays {
class Ghost {
  private static readonly GameObject s_head = CreateHead();
  private static readonly GameObject s_controllerLeft = CreateController(true);
  private static readonly GameObject s_controllerRight =
      CreateController(false);
  private const float HEAD_WIDTH = 0.4f;
  private const float HEAD_HEIGHT = 0.25f;
  private const float HEAD_DEPTH = 0.15f;

  private static void AddCubeMeshFilter(ref GameObject gameObject, float left,
                                        float right, float top, float bottom,
                                        float front, float back) {
    var meshFilter = gameObject.AddComponent<MeshFilter>();
    meshFilter.mesh.Clear();
    meshFilter.mesh.vertices = new[] {
      new Vector3(left, top, back),     new Vector3(right, top, back),
      new Vector3(right, bottom, back), new Vector3(left, bottom, back),
      new Vector3(left, bottom, front), new Vector3(right, bottom, front),
      new Vector3(right, top, front),   new Vector3(left, top, front),
    };
    meshFilter.mesh.triangles = new[] {
      0, 2, 1, 0, 3, 2, // front
      2, 3, 4, 2, 4, 5, // top
      1, 2, 5, 1, 5, 6, // right
      0, 7, 4, 0, 4, 3, // left
      5, 4, 7, 5, 7, 6, // back
      0, 6, 7, 0, 1, 6, // bottom
    };
    meshFilter.mesh.Optimize();
    meshFilter.mesh.RecalculateNormals();
  }

  private static GameObject CreateHead() {
    var head = new GameObject("SpeedrunTools_Ghost_Head") {
      // https://gamedev.stackexchange.com/questions/71713/how-to-create-a-new-gameobject-without-adding-it-to-the-scene
      // hideFlags = HideFlags.HideInHierarchy,
      active = false,
    };
    Object.DontDestroyOnLoad(head);

    AddCubeMeshFilter(ref head, HEAD_WIDTH * -0.5f, HEAD_WIDTH * 0.5f,
                      HEAD_HEIGHT * -0.5f, HEAD_HEIGHT * 0.5f, 0, -HEAD_DEPTH);

    var meshRenderer = head.AddComponent<MeshRenderer>();
    meshRenderer.material.color = Color.blue;

    return head;
  }

  private static GameObject CreateController(bool isLeft) {
    var controller = new GameObject(
        $"SpeedrunTools_Ghost_Controller_{(isLeft ? "Left" : "right")}") {
      active = false,
    };
    Object.DontDestroyOnLoad(controller);

    AddCubeMeshFilter(ref controller, -0.07f, 0.07f, -0.07f, 0.12f, 0.07f,
                      -0.07f);

    var meshRenderer = controller.AddComponent<MeshRenderer>();
    meshRenderer.material.color = Color.blue;

    return controller;
  }

  private static (GameObject, GameObject, GameObject, GameObject)
      CreateGhostGameObjects() {
    var go = new GameObject("SpeedrunTools_Ghost");
    var head = Object.Instantiate(s_head, go.transform);
    head.active = true;
    var controllerLeft = Object.Instantiate(s_controllerLeft, go.transform);
    var controllerRight = Object.Instantiate(s_controllerRight, go.transform);
    return (go, head, controllerLeft, controllerRight);
  }

  public Replay Replay;
  public bool IsPlaying = false;
  public bool IsPaused = false;
  public bool IsFinishedPlaying = false;

  private FrameReader _frameReader;
  private float _relativeStartTime;
  private int _levelIdx;
  private int _frameIdx;
  private float _loadStartTime;
  private GameObject _go;
  private GameObject _head;
  private GameObject _controllerLeft;
  private GameObject _controllerRight;
  private Bwr.Frame _frameCur;
  private Bwr.Frame? _frameNext;
  private bool _isLoading = false;

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
    if (_head != null) {
      Object.Destroy(_head);
      _head = null;
    }
  }

  public void OnLoadingScreen() {
    _isLoading = true;
    _loadStartTime = Time.time;
  }

  public void OnSceneChange() {
    _isLoading = false;
    _relativeStartTime += Time.time - _loadStartTime;
  }

  public void OnUpdate(int currentSceneIdx) {
    // Pause playback during loading screens
    if (!IsPlaying || _isLoading)
      return;

    // Seek forward until _frameCur is <= now && _frameNext is > now (or none at
    // end)
    var time = Time.time - _relativeStartTime;
    var hasLevelChanged = false;
    while (_frameNext.HasValue && _frameNext.Value.Time <= time) {
      _frameCur = _frameNext.Value;
      int ? nextLevelIdx;
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
    }

    // Render nothing if ghost is not in the current scene
    if (hasLevelChanged || currentSceneIdx != _levelIdx) {
      if (_go != null) {
        Object.Destroy(_go);
        Object.Destroy(_head);
        Object.Destroy(_controllerLeft);
        Object.Destroy(_controllerRight);
        _go = _head = _controllerLeft = _controllerRight = null;
      }
      return;
    }

    // Render head lerped between prev and next frames
    if (_go == null) {
      (_go, _head, _controllerLeft, _controllerRight) =
          CreateGhostGameObjects();
    }
    var frameCurTime = _frameCur.Time - Replay.Metadata.StartTime;
    var frameNextTime = _frameNext.Value.Time - Replay.Metadata.StartTime;
    var t = (time - frameCurTime) / (frameNextTime - frameCurTime);
    _head.transform.position = Vector3.Lerp(
        ToUnityVec3(_frameCur.VrInput.Value.PlayerPosition.Headset.Position),
        ToUnityVec3(
            _frameNext.Value.VrInput.Value.PlayerPosition.Headset.Position),
        t);
    _head.transform.rotation = Quaternion.Lerp(
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
