using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SpeedrunTools.PluginRecord
{
  class PluginRecord : Plugin
  {
    private const float MIN_FRAME_TIME = 0.1f;
    private const float MAX_RECORDING_DURATION = 60 * 60;
    private const int SCENE_IDX_MENU = 1;
    private const int SCENE_IDX_START = 2;
    private const int SCENE_IDX_THRONE = 15;
    private const string GHOST_FILENAME = "ghost.vrr";

    private static readonly string GHOST_FILE_PATH = Path.Combine(Utils.DIR, GHOST_FILENAME);

    private static Replay.Recorder s_hotkey_recorder;
    private static Replay.Replay s_hotkey_replay;
    private static Ghost s_hotkey_ghost;

    private static Replay.Recorder s_recorder;
    private static Replay.Replay s_replay;
    private static Ghost s_ghost;

    private static int s_currentSceneIdx = -1;

    public static readonly Pref<bool> PrefHotkeys = new Pref<bool>()
    {
      Id = "enableRecordingHotkeys",
      Name = "Start/stop recording with left B + right B and playback with right thumbstick",
      DefaultValue = false
    };

    public readonly Hotkey HotkeyStart = new Hotkey()
    {
      Predicate = (cl, cr) => cl.GetBButton() && cr.GetBButton() && PrefHotkeys.Read(),
      Handler = () =>
      {
        if (s_hotkey_recorder != null && s_hotkey_recorder.IsRecording)
        {
          s_hotkey_recorder.Stop();
          MelonLogger.Msg("Recording stopped");
        } else
        {
          s_hotkey_recorder = new Replay.Recorder();
          s_hotkey_recorder.Start(MIN_FRAME_TIME, MAX_RECORDING_DURATION);
          MelonLogger.Msg("Recording started");
        }
      }
    };

    public readonly Hotkey HotkeyPlay = new Hotkey()
    {
      Predicate = (cl, cr) => cr.GetThumbStick() && PrefHotkeys.Read(),
      Handler = () =>
      {
        if (s_hotkey_replay == null) return;
        if (s_hotkey_ghost != null && (s_hotkey_ghost.IsPlaying || s_hotkey_ghost.IsFinishedPlaying))
        {
          s_hotkey_ghost.Stop();
          MelonLogger.Msg("Playback stopped");
        } else
        {
          s_hotkey_ghost = new Ghost(s_hotkey_replay);
          s_hotkey_ghost.Start();
          MelonLogger.Msg("Playback started");
        }
      }
    };

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      if (buildIndex == SCENE_IDX_MENU)
      {
        Replay.Replay.ReadFromFile(GHOST_FILE_PATH);
      }

      Utils.LogDebug($"buildIndex = {buildIndex}");
      if (buildIndex == SCENE_IDX_START && (s_recorder == null || !s_recorder.IsRecording))
      {
        MelonLogger.Msg("Recording started");
        s_recorder = new Replay.Recorder();
        s_recorder.Start(MIN_FRAME_TIME, MAX_RECORDING_DURATION);

        try
        {
          s_replay = Replay.Replay.ReadFromFile(GHOST_FILE_PATH);
          s_ghost = new Ghost(s_replay);
          s_ghost.Start();
        } catch (FileNotFoundException) { }
      } else if (buildIndex != s_currentSceneIdx + 1 && (s_recorder != null && s_recorder.IsRecording))
      {
        s_recorder.Stop();
        var replay = s_recorder.GetReplay();
        if (s_ghost != null) s_ghost.Stop();
        if (buildIndex == SCENE_IDX_MENU && s_currentSceneIdx == SCENE_IDX_THRONE)
        {
          // TODO: Make this async
          var startTime = System.DateTime.FromBinary(replay.File.Metadata.Value.StartTime);
          var filename = $"run-{startTime:yyyy\\_MM\\_dd\\-HH\\_mm\\_ss}-{replay.GetDuration():m\\_ss}.vrr";
          replay.SaveToFile(filename);
          if (!File.Exists(GHOST_FILE_PATH))
          {
            File.Copy(Path.Combine(Utils.DIR, filename), GHOST_FILE_PATH);
            MelonLogger.Msg("Replay also saved as ghost.vrr since it didn't exist");
          }
        }
      }

      s_currentSceneIdx = buildIndex;
    }

    public override void OnUpdate()
    {
      if (s_hotkey_recorder != null) s_hotkey_recorder.OnUpdate(s_currentSceneIdx);
      if (s_hotkey_ghost != null) s_hotkey_ghost.OnUpdate(s_currentSceneIdx);

      if (s_recorder != null) s_recorder.OnUpdate(s_currentSceneIdx);
      if (s_ghost != null) s_ghost.OnUpdate(s_currentSceneIdx);
    }
  }

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

    public Replay.Replay Replay;
    public bool IsPlaying = false;
    public bool IsPaused = false;
    public bool IsFinishedPlaying = false;

    private float _startTime;
    private int _levelIdx;
    private int _frameIdx;
    private float? _loadStartTime;
    private GameObject _head;
    private Bwr.Level _level;

    public Ghost(Replay.Replay replay)
    {
      Replay = replay;
    }

    public void Start()
    {
      IsPlaying = true;
      IsPaused = false;
      IsFinishedPlaying = false;
      _startTime = Time.time;
      _levelIdx = 0;
      _frameIdx = 0;
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
        _startTime += Time.time - _loadStartTime.Value;
        _loadStartTime = null;
      }

      // Seek forward until _frameIdx is frame <= now && _frameIdx+1 is frame > now (or none at end)
      var time = Time.time - _startTime;
      Bwr.Frame? nextFrame;
      while ((nextFrame = _level.Frames(_frameIdx + 1)).HasValue && nextFrame.Value.Time <= time) _frameIdx++;
      if (!nextFrame.HasValue && _levelIdx + 1 >= Replay.File.LevelsLength)
      {
        Pause();
        IsFinishedPlaying = true;
        Utils.LogDebug("Finished playing");
      }

      // Render nothing if ghost is not in the current scene
      if (currentSceneIdx != _level.SceneIndex)
      {
        if (_head != null)
        {
          Object.Destroy(_head);
          _head = null;
        }
        return;
      }

      // Render head lerped between prev and next frames
      var curFrame = _level.Frames(_frameIdx).Value;
      if (_head == null)
      {
        _head = Object.Instantiate(s_head);
        _head.active = true;
      }
      // if (!nextFrame.HasValue)
      // {
      //   var pos = curFrame.Player.Value.Position.Value;
      //   _head.transform.position = new Vector3(pos.X, pos.Y, pos.Z);
      //   _head.transform.rotation = Quaternion.Euler(0, curFrame.Player.Value.Rotation, 0);
      // } else
      // {
      //   var t = (time - curFrame.Time) / (frame2.Time - curFrame.Time);
      //   _head.transform.position = Vector3.Lerp(
      //     curFrame.Position,
      //     frame2.Position,
      //     t
      //   );
      //   _head.transform.rotation = Quaternion.Lerp(
      //     curFrame.Rotation,
      //     frame2.Rotation,
      //     t
      //   );
      // }
    }
  }
}
