using MelonLoader;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;

namespace SpeedrunTools.Replay
{
  class Recorder
  {
    private static readonly string TEMP_FILENAME = $"temp.{Constants.REPLAY_EXTENSION}";
    private static readonly string TEMP_FILE_PATH = Path.Combine(Utils.REPLAYS_DIR, TEMP_FILENAME);

    public bool IsRecording = false;
    public string FilenamePrefix;
    public float MaxFps;
    public float MaxDuration;

    private float _minFrameTime;
    private int _numFrames = 0;
    private System.DateTime _startTime;
    private List<FlatBuffers.Offset<Bwr.Level>> _levels = new List<FlatBuffers.Offset<Bwr.Level>>();
    private float _levelStartTime;
    private int _levelStartFrameOffset;
    private float? _loadStartTime;
    private float _relativeStartTime;
    private float _lastFrameTime;
    private Camera _cam;
    private FileStream _fs;
    private int _fileIdx = 0;
    private FlatBuffers.FlatBufferBuilder _metaBuilder;
    private GameStateSerializer _serializer;

    public string FilePath;

    private void WriteFile(byte[] bytes)
    {
      // TODO: Should we queue this async?
      _fs.Write(bytes, _fileIdx, bytes.Length);
      _fileIdx += bytes.Length;
    }

    private void CloseFile()
    {
      if (_fs == null) return;
      _fs.Close();
      _fs = null;
    }

    private Transform GetPlayerTransform()
    {
      if (_cam == null)
      {
        var rig = Object.FindObjectOfType<StressLevelZero.Rig.RigManager>();
        _cam = rig.ControllerRig.GetComponentInChildren<Camera>();
      }
      return _cam.transform;
    }

    private Transform GetHeadsetTransform()
    {
      // TODO
      return _cam.transform;
    }

    private Transform GetControllerTransform(string name)
    {
      // TODO
      return _cam.transform;
    }

    public Recorder(string filenamePrefix = "run", float maxFps = 144, float maxDuration = 60 * 60 * 10)
    {
      FilenamePrefix = filenamePrefix;
      MaxFps = maxFps;
      MaxDuration = maxDuration;

      _minFrameTime = 1 / maxFps;
      _startTime = System.DateTime.Now;
      _loadStartTime = Time.time;
      _relativeStartTime = Time.time;
      IsRecording = true;

      try
      {
        File.Delete(TEMP_FILE_PATH);
      } catch { }
      _fs = File.Create(TEMP_FILE_PATH);
      _fileIdx = 0;
      WriteFile(Constants.FILE_START_BYTES);

      _metaBuilder = new FlatBuffers.FlatBufferBuilder(1024);
      _serializer = new GameStateSerializer();
    }

    public string Stop()
    {
      if (!IsRecording)
      {
        throw new System.Exception("Cannot stop recording when not started.");
      }

      IsRecording = false;
      var duration = _lastFrameTime - _relativeStartTime;

      var metadata = Bwr.Metadata.CreateMetadata(
        _metaBuilder,
        _startTime.ToBinary(),
        duration,
        Bwr.Metadata.CreateLevelsVector(_metaBuilder, _levels.ToArray())
      );
      _metaBuilder.Finish(metadata.Value);
      var metadataOffset = _fileIdx;
      WriteFile(_metaBuilder.SizedByteArray());
      _fs.Write(System.BitConverter.GetBytes(metadataOffset), Constants.METADATA_OFFSET_INDEX, sizeof(int));

      CloseFile();
      var durationTs = System.TimeSpan.FromSeconds(duration);
      FilePath = Path.Combine(
        Utils.REPLAYS_DIR,
        $"{FilenamePrefix}-{_startTime:yyyy\\_MM\\_dd\\-HH\\_mm\\_ss}-{durationTs:h\\_mm\\_ss}.{Constants.REPLAY_EXTENSION}"
      );
      File.Move(TEMP_FILE_PATH, FilePath);
      return FilePath;
    }

    public void OnSceneChange()
    {
      _serializer.OnSceneChange();
    }

    public void OnUpdate(int currentSceneIdx)
    {
      if (!IsRecording) return;
      if (Time.time - _relativeStartTime >= MaxDuration)
      {
        MelonLogger.Warning("Max recording length reached. Stopping recording.");
        Stop();
        return;
      }

      // Pause recording during loading screens
      if (SceneLoader.loading)
      {
        if (!_loadStartTime.HasValue)
        {
          _levels.Add(Bwr.Level.CreateLevel(_metaBuilder, _levelStartTime, currentSceneIdx, _levelStartFrameOffset));

          _loadStartTime = Time.time;
          _cam = null; // seems like camera is replaced on scene change
        }
        return;
      }

      // Resume recording after loading
      if (_loadStartTime.HasValue)
      {
        _levelStartTime = Time.time;
        _levelStartFrameOffset = _fileIdx;
        _loadStartTime = null;
      }

      // Only record frame if time since last frame is greater than max FPS
      if (Time.time < _lastFrameTime + _minFrameTime) return;

      // Record frame
      var frame = _serializer.BuildFrame();
      WriteFile(System.BitConverter.GetBytes((ushort)frame.Length));
      WriteFile(frame);
      _lastFrameTime = Time.time;
      //Utils.LogDebug($"Recorded frame: {currentSceneIdx} {cam.position.ToString()}");
    }
  }
}
