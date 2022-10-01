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
  public static readonly Dictionary<Bwr.GameMode, string> FILENAME_PREFIXES =
      new Dictionary<Bwr.GameMode, string> {
        [Bwr.GameMode.NONE] = "manual",
        [Bwr.GameMode.SPEEDRUN] = "run",
        [Bwr.GameMode.ANY_PERCENT] = "any",
        [Bwr.GameMode.NO_MAJOR_GLITCHES] = "nmg",
        [Bwr.GameMode.NEWGAME_PLUS] = "ngplus",
        [Bwr.GameMode.HUNDRED_PERCENT] = "hundred",
        [Bwr.GameMode.BLINDFOLDED] = "blindfolded",
        [Bwr.GameMode.GRIPLESS] = "gripless",
        [Bwr.GameMode.LEFT_CONTROLLER_GRIPLESS] = "leftctrlgripless",
        [Bwr.GameMode.RIGHT_CONTROLLER_GRIPLESS] = "rightctrlgripless",
        [Bwr.GameMode.ARMLESS] = "armless",
      };

  public bool IsRecording = false;
  public Bwr.GameMode GameMode;
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
  private FileStream _fileStream;
  private int _fileIdx = 0;
  private FlatBuffers.FlatBufferBuilder _metaBuilder;
  private GameStateSerializer _serializer;
  private Queue<byte[]> _queuedWrites = new Queue<byte[]>();
  private Task _writeQueueTask;

  public string FilePath;

  private void Close() {
    if (_fileStream != null) {
      _fileStream.Flush();
      _fileStream.Close();
      _fileStream = null;
    }
  }

  public Recorder(Bwr.GameMode gameMode, float maxFps = 15,
                  float maxDuration = 60 * 60 * 10) {
    GameMode = gameMode;
    MaxFps = maxFps;
    MaxDuration = maxDuration;

    _minFrameTime = 1f / MaxFps;
    _startTime = System.DateTime.Now;
    _relativeStartTime = Time.time;
    IsRecording = true;

    Directory.CreateDirectory(Utils.REPLAYS_DIR);
    _fileStream =
        new FileStream(TEMP_FILE_PATH, FileMode.Create, FileAccess.Write,
                       FileShare.Read, 4096, useAsync: true);
    QueueWrite(Constants.FILE_START_BYTES);

    _levelStartTime = Time.time;
    _levelStartFrameOffset = _fileIdx;
    _prevLoadDuration = 0;

    _metaBuilder = new FlatBuffers.FlatBufferBuilder(1024);
    _serializer = new GameStateSerializer();
  }

  public Replay Stop(bool didComplete) {
    if (!IsRecording) {
      throw new System.Exception("Cannot stop recording when not started.");
    }

    if (!_loadStartTime.HasValue && Mod.GameState.currentSceneIdx.HasValue)
      OnLevelEnd(Mod.GameState.currentSceneIdx.Value);
    _loadStartTime = null;

    IsRecording = false;
    var duration = _lastFrameTime - _relativeStartTime;

    var metadata = Bwr.Metadata.CreateMetadata(
        _metaBuilder, GameMode, _startTime.ToBinary(), didComplete, duration,
        Bwr.Metadata.CreateLevelsVector(_metaBuilder, _levels.ToArray()));
    _metaBuilder.Finish(metadata.Value);
    var metadataOffset = (uint)_fileIdx;
    QueueWrite(_metaBuilder.SizedByteArray()).Wait();
    // NOTE: Make sure we've waited for all queued writes to complete before
    // using _fileStream directly and modifying the position
    _fileStream.Position = Constants.METADATA_OFFSET_INDEX;
    var bytes = System.BitConverter.GetBytes(metadataOffset);
    _fileStream.Write(bytes, 0, bytes.Length);

    Close();
    var resultText = didComplete ? "" : "-unfinished";
    var durationTs = System.TimeSpan.FromSeconds(duration);
    FilePath = Path.Combine(
        Utils.REPLAYS_DIR,
        $"{FILENAME_PREFIXES[GameMode]}{resultText}-{_startTime:yyyy\\_MM\\_dd\\-HH\\_mm\\_ss}-{durationTs:h\\_mm\\_ss}.{Constants.REPLAY_EXTENSION}");
    File.Move(TEMP_FILE_PATH, FilePath);
    var replay = new Replay(FilePath);
    Features.Replay.AllReplays.Add(replay);
    return replay;
  }

  public void OnLevelEnd(int endedSceneIdx) {
    if (!IsRecording || _loadStartTime.HasValue)
      return;
    _levels.Add(Bwr.Level.CreateLevel(
        _metaBuilder, _levelStartTime - _relativeStartTime,
        Time.time - _levelStartTime, _prevLoadDuration,
        Mod.GameState.didPrevLevelComplete, endedSceneIdx,
        _levelStartFrameOffset));
    _loadStartTime = Time.time;
  }

  public void OnLevelStart() {
    // Resume recording after loading
    if (_loadStartTime.HasValue) {
      _levelStartTime = Time.time;
      _levelStartFrameOffset = _fileIdx;
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
      Stop(false);
      return;
    }

    // Record frame
    var frame = _serializer.BuildFrame(Time.time - _relativeStartTime);
    QueueWrite(System.BitConverter.GetBytes((ushort)frame.Length));
    QueueWrite(frame);
    _lastFrameTime = Time.time;
    // Utils.LogDebug($"Recorded frame: {currentSceneIdx}
    // {cam.position.ToString()}");
  }

  private Task QueueWrite(byte[] data) {
    _queuedWrites.Enqueue(data);
    _fileIdx += data.Length;
    if (_writeQueueTask?.IsCompleted ?? true)
      _writeQueueTask = ProcessWriteQueue();
    return _writeQueueTask;
  }

  private async Task ProcessWriteQueue() {
    while (_queuedWrites.Count > 0) {
      var data = _queuedWrites.Dequeue();
      await _fileStream.WriteAsync(data, 0, data.Length);
    }
  }
}
}
