using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SpeedrunTools.Replays {
class Replay {
  private BinaryReader _reader;
  private int _framesEndIdx;

  public string FilePath;
  public Bwr.Metadata Metadata;

  public Replay(string filePath) {
    FilePath = filePath;

    _reader = new BinaryReader(File.OpenRead(filePath));

    var magicHeader = _reader.ReadBytes(Constants.MAGIC_HEADER_BYTES.Length);
    if (!magicHeader.SequenceEqual(Constants.MAGIC_HEADER_BYTES))
      throw new ReplayException("File is not a replay (header mismatch)");
    var metadataIdx = _reader.ReadInt32();
    if (metadataIdx == 0)
      throw new ReplayException("Metadata missing (incomplete replay file)");
    if (metadataIdx < 0 || metadataIdx >= _reader.BaseStream.Length)
      throw new ReplayException("Invalid metadata offset (out of bounds)");

    _framesEndIdx = metadataIdx;
    _reader.BaseStream.Position = metadataIdx;
    var metadataBytes = _reader.ReadBytes(
        (int)(_reader.BaseStream.Length - _reader.BaseStream.Position));
    _reader.BaseStream.Position = Constants.FILE_START_BYTES.Length;
    Metadata = Bwr.Metadata.GetRootAsMetadata(
        new FlatBuffers.ByteBuffer(metadataBytes));
    _reader.BaseStream.Position = Constants.FILE_START_BYTES.Length;
  }

  public void Close() {
    if (_reader != null) {
      _reader.Close();
      _reader = null;
    }
  }

  public FrameReader CreateFrameReader() => new FrameReader(_reader, Metadata,
                                                            _framesEndIdx);

  public System.DateTime
  GetStartTime() => System.DateTime.FromBinary(Metadata.StartTime);
  public System.TimeSpan
  GetDuration() => System.TimeSpan.FromSeconds(Metadata.Duration);
}

class ReplayException : System.Exception {
  public ReplayException(string message) : base($"Invalid replay: {message}") {}
}

class FrameReader {
  private BinaryReader _reader;
  private int _framesEndIdx;
  private int _nextLevelIdx;
  private Bwr.Level? _nextLevel;
  private Bwr.Metadata _metadata;

  public FrameReader(BinaryReader reader, Bwr.Metadata metadata,
                     int framesEndIdx) {
    _reader = reader;
    _framesEndIdx = framesEndIdx;
    _metadata = metadata;
    _nextLevelIdx = 0;
    _nextLevel = metadata.Levels(_nextLevelIdx);
  }

  public (int?, Bwr.Frame?) Read() {
    if (_reader.BaseStream.Position >= _framesEndIdx)
      return (null, null);
    int? sceneIdx = null;
    if (_reader.BaseStream.Position >= _nextLevel?.FrameOffset) {
      sceneIdx = _nextLevel?.SceneIndex;
      _nextLevel = ++_nextLevelIdx >= _metadata.LevelsLength
                       ? null
                       : _metadata.Levels(_nextLevelIdx);
    }
    var frameLen = _reader.ReadUInt16();
    if (frameLen == 0 ||
        _reader.BaseStream.Position + frameLen > _framesEndIdx) {
      var reason = frameLen == 0 ? "zero" : "past end";
      throw new System.Exception($"Invalid frame length ({reason})");
    }
    var frameBytes = new FlatBuffers.ByteBuffer(_reader.ReadBytes(frameLen));
    return (sceneIdx, Bwr.Frame.GetRootAsFrame(frameBytes));
  }
}
}
