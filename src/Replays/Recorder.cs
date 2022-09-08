using MelonLoader;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;

namespace SpeedrunTools.Replays {
class Recorder {
  private static readonly string TEMP_FILENAME = "temp_replay";
  private static readonly string TEMP_FILE_PATH =
      Path.Combine(Utils.REPLAYS_DIR, TEMP_FILENAME);

  public bool IsRecording = false;
  public string FilenamePrefix;
  public float MaxFps;
  public float MaxDuration;

  private float _minFrameTime;
  private System.DateTime _startTime;
  private List<FlatBuffers.Offset<Bwr.Level>> _levels =
      new List<FlatBuffers.Offset<Bwr.Level>>();
  private float? _loadStartTime;
  private float _prevLoadDuration;
  private float _levelStartTime;
  private int _levelStartFrameOffset;
  private float _relativeStartTime;
  private float _lastFrameTime;
  private BinaryWriter _writer;
  private FlatBuffers.FlatBufferBuilder _metaBuilder;
  private GameStateSerializer _serializer;

  public string FilePath;

  private void Close() {
    if (_writer != null) {
      _writer.Flush();
      _writer.Close();
      _writer = null;
    }
  }

  public Recorder(string filenamePrefix = "run", float maxFps = 5,
                  float maxDuration = 60 * 60 * 10) {
    FilenamePrefix = filenamePrefix;
    MaxFps = maxFps;
    MaxDuration = maxDuration;

    _minFrameTime = 1f / MaxFps;
    _startTime = System.DateTime.Now;
    _relativeStartTime = Time.time;
    IsRecording = true;

    Directory.CreateDirectory(Utils.REPLAYS_DIR);
    _writer = new BinaryWriter(File.Open(TEMP_FILE_PATH, FileMode.Create));
    _writer.Write(Constants.FILE_START_BYTES);

    _levelStartTime = Time.time;
    _levelStartFrameOffset = (int)_writer.BaseStream.Position;
    _prevLoadDuration = 0;

    _metaBuilder = new FlatBuffers.FlatBufferBuilder(1024);
    _serializer = new GameStateSerializer();
  }

  public string Stop() {
    if (!IsRecording) {
      throw new System.Exception("Cannot stop recording when not started.");
    }

    if (!_loadStartTime.HasValue && Mod.GameState.currentSceneIdx.HasValue)
      OnLevelEnd(Mod.GameState.currentSceneIdx.Value);
    _loadStartTime = null;

    IsRecording = false;
    var duration = _lastFrameTime - _relativeStartTime;

    var metadata = Bwr.Metadata.CreateMetadata(
        _metaBuilder, _startTime.ToBinary(), duration,
        Bwr.Metadata.CreateLevelsVector(_metaBuilder, _levels.ToArray()));
    _metaBuilder.Finish(metadata.Value);
    var metadataOffset = (uint)_writer.BaseStream.Position;
    _writer.Write(_metaBuilder.SizedByteArray());
    _writer.BaseStream.Position = Constants.METADATA_OFFSET_INDEX;
    _writer.Write(metadataOffset);

    Close();
    var durationTs = System.TimeSpan.FromSeconds(duration);
    FilePath = Path.Combine(
        Utils.REPLAYS_DIR,
        $"{FilenamePrefix}-{_startTime:yyyy\\_MM\\_dd\\-HH\\_mm\\_ss}-{durationTs:h\\_mm\\_ss}.{Constants.REPLAY_EXTENSION}");
    File.Move(TEMP_FILE_PATH, FilePath);
    return FilePath;
  }

  public void OnLevelEnd(int endedSceneIdx) {
    if (!IsRecording || _loadStartTime.HasValue)
      return;
    _levels.Add(Bwr.Level.CreateLevel(
        _metaBuilder, _levelStartTime - _relativeStartTime,
        Time.time - _levelStartTime, _prevLoadDuration, endedSceneIdx,
        _levelStartFrameOffset));
    _loadStartTime = Time.time;
  }

  public void OnLevelStart() {
    // Resume recording after loading
    if (_loadStartTime.HasValue) {
      _levelStartTime = Time.time;
      _levelStartFrameOffset = (int)_writer.BaseStream.Position;
      _prevLoadDuration = _levelStartTime - _loadStartTime.Value;
      _relativeStartTime += _prevLoadDuration;
      _loadStartTime = null;
    }
  }

  public void OnUpdate() {
    if (!IsRecording)
      return;

    // Pause recording during loading screens
    if (_loadStartTime.HasValue)
      return;

    // Only record frame if time since last frame is greater than max FPS
    if (Time.time < _lastFrameTime + _minFrameTime)
      return;

    if (Time.time - _relativeStartTime >= MaxDuration) {
      MelonLogger.Warning("Max recording length reached. Stopping recording.");
      Stop();
      return;
    }

    // Record frame
    var frame = _serializer.BuildFrame(Time.time - _relativeStartTime);
    // TODO: Should we queue this async?
    _writer.Write((ushort)frame.Length);
    _writer.Write(frame);
    _lastFrameTime = Time.time;
    // Utils.LogDebug($"Recorded frame: {currentSceneIdx}
    // {cam.position.ToString()}");
  }
}
}
