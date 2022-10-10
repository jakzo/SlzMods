using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MelonLoader;

namespace Sst.Replays {
class Replay {
  private FileStream _fileStream;
  private BinaryReader _reader;
  private int _framesEndIdx;

  public string FilePath;
  public Bwr.Metadata Metadata;

  public Replay(string filePath) {
    FilePath = filePath;

    _fileStream =
        new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                       FrameReader.BUFFER_SIZE, FileOptions.SequentialScan);
    _reader = new BinaryReader(_fileStream);

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

  public FrameReader
  CreateFrameReader() => new FrameReader(_fileStream, Metadata, _framesEndIdx);

  public System.DateTime
  GetStartTime() => System.DateTime.FromBinary(Metadata.StartTime);
  public System.TimeSpan
  GetDuration() => System.TimeSpan.FromSeconds(Metadata.Duration);
}

class ReplayException : System.Exception {
  public ReplayException(string message) : base($"Invalid replay: {message}") {}
}

class FrameReader {
  // Stores two alternating buffers of this size
  // Must be larger than the size of a frame
  public const int BUFFER_SIZE = 128 * 1024;

  private FileStream _fileStream;
  private Bwr.Metadata _metadata;
  private int _framesEndIdx;
  private byte[][] _buffers;
  private int _bufferNum;
  private int _bufferIdx;
  private long _fileIdx;
  private int _nextLevelIdx;
  private Bwr.Level? _nextLevel;

  public FrameReader(FileStream fileStream, Bwr.Metadata metadata,
                     int framesEndIdx) {
    _fileStream = fileStream;
    _metadata = metadata;
    _framesEndIdx = framesEndIdx;
    Seek(metadata.Levels(0).Value.FrameOffset);
  }

  public (int?, Bwr.Frame?) Read() {
    if (_fileIdx >= _framesEndIdx)
      return (null, null);
    int? sceneIdx = null;
    if (_fileIdx >= _nextLevel?.FrameOffset) {
      sceneIdx = _nextLevel?.SceneIndex;
      _nextLevel = ++_nextLevelIdx >= _metadata.LevelsLength
                       ? null
                       : _metadata.Levels(_nextLevelIdx);
    }
    var frameLen = ReadUInt16();
    if (frameLen == 0 || _fileIdx + frameLen > _framesEndIdx) {
      var reason = frameLen == 0 ? "zero" : "past end";
      throw new System.Exception($"Invalid frame length ({reason})");
    }
    var frameBytes = new FlatBuffers.ByteBuffer(ReadBytes(frameLen));
    return (sceneIdx, Bwr.Frame.GetRootAsFrame(frameBytes));
  }

  public void Seek(long offset) {
    _fileIdx = _fileStream.Position = offset;
    _buffers = new byte[][] { new byte[BUFFER_SIZE], new byte[BUFFER_SIZE] };
    foreach (var buffer in _buffers)
      _fileStream.Read(buffer, 0, buffer.Length);
    _bufferNum = _bufferIdx = 0;

    for (var i = 0; i < _metadata.LevelsLength; i++) {
      var level = _metadata.Levels(i).Value;
      if (level.FrameOffset > offset) {
        _nextLevelIdx = i;
        _nextLevel = level;
        return;
      }
    }
    _nextLevelIdx = _metadata.LevelsLength;
    _nextLevel = null;
  }

  private ushort ReadUInt16() {
    AssertBufferAvailable(_bufferNum);
    var buffer1 = _buffers[_bufferNum];
    int b1 = buffer1[_bufferIdx];
    if (++_bufferIdx >= buffer1.Length)
      SwitchBuffer();

    AssertBufferAvailable(_bufferNum);
    var buffer2 = _buffers[_bufferNum];
    int b2 = buffer2[_bufferIdx];
    if (++_bufferIdx >= buffer2.Length)
      SwitchBuffer();

    _fileIdx += 2;
    return (ushort)((b2 << 8) | b1);
  }

  private byte[] ReadBytes(int count) {
    AssertBufferAvailable(_bufferNum);
    var result = new byte[count];
    var bufferRemainingSize = _buffers[_bufferNum].Length - _bufferIdx;
    if (count < bufferRemainingSize) {
      System.Array.Copy(_buffers[_bufferNum], _bufferIdx, result, 0, count);
      _bufferIdx += count;
    } else {
      // NOTE: Assumes we only have two buffers and the read is smaller than the
      // buffer size
      var firstCopySize = bufferRemainingSize;
      System.Array.Copy(_buffers[_bufferNum], _bufferIdx, result, 0,
                        firstCopySize);
      SwitchBuffer();
      AssertBufferAvailable(_bufferNum);
      var secondCopySize = count - firstCopySize;
      System.Array.Copy(_buffers[_bufferNum], 0, result, firstCopySize,
                        secondCopySize);
      _bufferIdx = secondCopySize;
    }
    _fileIdx += count;
    return result;
  }

  private void SwitchBuffer() {
    FillBuffer(_bufferNum);
    if (++_bufferNum >= _buffers.Length)
      _bufferNum = 0;
    _bufferIdx = 0;
  }

  private async void FillBuffer(int bufferNum) {
    MelonLogger.Msg("Filling buffer");
    var buffer = _buffers[bufferNum];
    _buffers[bufferNum] = null;
    await _fileStream.ReadAsync(buffer, 0, buffer.Length);
    _buffers[bufferNum] = buffer;
  }

  private void AssertBufferAvailable(int bufferNum) {
    if (_buffers[_bufferNum] == null)
      throw new System.Exception(
          "Replay data not read from disk in time (frames will be dropped)");
  }
}
}
