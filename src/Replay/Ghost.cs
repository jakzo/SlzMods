using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SpeedrunTools.Replay
{
  class Ghost
  {
    private static readonly GameObject s_head = CreateHead();
    private const float HEAD_WIDTH = 0.4f;
    private const float HEAD_HEIGHT = 0.25f;
    private const float HEAD_DEPTH = 0.15f;

    private static GameObject CreateHead()
    {
      Vector3[] vertices = {
        new Vector3(HEAD_WIDTH * -0.5f, HEAD_HEIGHT * -0.5f, HEAD_DEPTH * (-0.5f - 0.5f)),
        new Vector3(HEAD_WIDTH *  0.5f, HEAD_HEIGHT * -0.5f, HEAD_DEPTH * (-0.5f - 0.5f)),
        new Vector3(HEAD_WIDTH *  0.5f, HEAD_HEIGHT *  0.5f, HEAD_DEPTH * (-0.5f - 0.5f)),
        new Vector3(HEAD_WIDTH * -0.5f, HEAD_HEIGHT *  0.5f, HEAD_DEPTH * (-0.5f - 0.5f)),
        new Vector3(HEAD_WIDTH * -0.5f, HEAD_HEIGHT *  0.5f, HEAD_DEPTH * ( 0.5f - 0.5f)),
        new Vector3(HEAD_WIDTH *  0.5f, HEAD_HEIGHT *  0.5f, HEAD_DEPTH * ( 0.5f - 0.5f)),
        new Vector3(HEAD_WIDTH *  0.5f, HEAD_HEIGHT * -0.5f, HEAD_DEPTH * ( 0.5f - 0.5f)),
        new Vector3(HEAD_WIDTH * -0.5f, HEAD_HEIGHT * -0.5f, HEAD_DEPTH * ( 0.5f - 0.5f)),
      };

      int[] triangles = {
        0, 2, 1, 0, 3, 2, // front
        2, 3, 4, 2, 4, 5, // top
        1, 2, 5, 1, 5, 6, // right
        0, 7, 4, 0, 4, 3, // left
        5, 4, 7, 5, 7, 6, // back
        0, 6, 7, 0, 1, 6, // bottom
      };

      var head = new GameObject("SpeedrunTools_Ghost_Head")
      {
        // https://gamedev.stackexchange.com/questions/71713/how-to-create-a-new-gameobject-without-adding-it-to-the-scene
        hideFlags = HideFlags.HideInHierarchy,
        active = false,
      };
      Object.DontDestroyOnLoad(head);

      var meshFilter = head.AddComponent<MeshFilter>();
      meshFilter.mesh.Clear();
      meshFilter.mesh.vertices = vertices;
      meshFilter.mesh.triangles = triangles;
      meshFilter.mesh.Optimize();
      meshFilter.mesh.RecalculateNormals();

      var meshRenderer = head.AddComponent<MeshRenderer>();
      meshRenderer.material.color = Color.blue;

      return head;
    }

    public Replay Replay;
    public bool IsPlaying = false;
    public bool IsPaused = false;
    public bool IsFinishedPlaying = false;

    private FrameReader _frameReader;
    private float _relativeStartTime;
    private int _levelIdx;
    private int _frameIdx;
    private float? _loadStartTime;
    private GameObject _head;
    // private Bwr.Level _level;
    private Bwr.Frame _frameCur;
    private Bwr.Frame? _frameNext;

    public Ghost(Replay replay)
    {
      Replay = replay;
      _frameReader = replay.CreateFrameReader();
    }

    public void Start()
    {
      IsPlaying = true;
      IsPaused = false;
      IsFinishedPlaying = false;
      _relativeStartTime = Time.time;

      var (l1, f1) = _frameReader.Read();
      if (l1.HasValue) _levelIdx = l1.Value;
      if (f1.HasValue) _frameCur = f1.Value;

      var (l2, f2) = _frameReader.Read();
      if (l2.HasValue) _levelIdx = l2.Value;
      if (f2.HasValue) _frameCur = f2.Value;
    }

    public void Pause()
    {
      IsPlaying = false;
      IsPaused = true;
    }

    public void Stop()
    {
      IsPlaying = false;
      IsPaused = false;
      IsFinishedPlaying = false;
      if (_head != null)
      {
        Object.Destroy(_head);
        _head = null;
      }
    }

    public void OnUpdate(int currentSceneIdx)
    {
      if (!IsPlaying) return;

      // Pause playback during loading screens
      if (SceneLoader.loading)
      {
        if (!_loadStartTime.HasValue) _loadStartTime = Time.time;
        return;
      }
      if (_loadStartTime.HasValue)
      {
        _relativeStartTime += Time.time - _loadStartTime.Value;
        _loadStartTime = null;
      }

      // Seek forward until _frameCur is <= now && _frameNext is > now (or none at end)
      var time = Time.time - _relativeStartTime;
      var hasLevelChanged = false;
      while (_frameNext.HasValue && _frameNext.Value.Time <= time)
      {
        _frameCur = _frameNext.Value;
        int? nextLevelIdx;
        (nextLevelIdx, _frameNext) = _frameReader.Read();
        if (nextLevelIdx.HasValue)
        {
          _levelIdx = nextLevelIdx.Value;
          hasLevelChanged = true;
        } else
        {
          hasLevelChanged = false;
        }
      }
      if (!_frameNext.HasValue)
      {
        Pause();
        IsFinishedPlaying = true;
        Utils.LogDebug("Finished playing");
      }

      // Render nothing if ghost is not in the current scene
      if (hasLevelChanged || currentSceneIdx != _levelIdx)
      {
        if (_head != null)
        {
          Object.Destroy(_head);
          _head = null;
        }
        return;
      }

      // Render head lerped between prev and next frames
      if (_head == null)
      {
        _head = Object.Instantiate(s_head);
        _head.active = true;
      }
      var frameCurTime = _frameCur.Time - Replay.Metadata.StartTime;
      var frameNextTime = _frameNext.Value.Time - Replay.Metadata.StartTime;
      var t = (time - frameCurTime) / (frameNextTime - frameCurTime);
      _head.transform.position = Vector3.Lerp(
        ToUnityVec3(_frameCur.Player.Value.Position.Value),
        ToUnityVec3(_frameNext.Value.Player.Value.Position.Value),
        t
      );
      _head.transform.rotation = Quaternion.Lerp(
        ToUnityQuaternion(_frameCur.Player.Value.Headset.Value.RotationEuler.Value),
        ToUnityQuaternion(_frameNext.Value.Player.Value.Headset.Value.RotationEuler.Value),
        t
      );
    }

    static private Vector3 ToUnityVec3(Bwr.Vector3 vec) =>
      new Vector3(vec.X, vec.Y, vec.Z);
    static private Quaternion ToUnityQuaternion(Bwr.Vector3 vec) =>
      Quaternion.Euler(vec.X, vec.Y, vec.Z);
  }
}
