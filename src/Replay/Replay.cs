using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SpeedrunTools.Replay
{
  class Replay
  {
    private FileStream _fs;
    private int _framesEndIdx;

    public string FilePath;
    public Bwr.Metadata Metadata;

    public Replay(string filePath)
    {
      FilePath = filePath;

      _fs = File.OpenRead(filePath);

      var headerBytes = new byte[Constants.FILE_START_BYTES.Length];
      _fs.Read(headerBytes, 0, Constants.FILE_START_BYTES.Length);
      var magicHeader = headerBytes.Take(Constants.MAGIC_HEADER_BYTES.Length);
      if (magicHeader.SequenceEqual(Constants.MAGIC_HEADER_BYTES))
        throw new ReplayException("File is not a replay (header mismatch)");
      var metadataIdx = System.BitConverter.ToInt32(headerBytes, Constants.METADATA_OFFSET_INDEX);
      if (metadataIdx == 0)
        throw new ReplayException("Metadata missing (incomplete replay file)");
      if (metadataIdx < 0 || metadataIdx >= _fs.Length)
        throw new ReplayException("Invalid metadata offset (out of bounds)");

      _framesEndIdx = metadataIdx;
      var metadataBytes = new byte[_fs.Length - metadataIdx];
      _fs.Read(metadataBytes, metadataIdx, metadataBytes.Length);
      Metadata = Bwr.Metadata.GetRootAsMetadata(new FlatBuffers.ByteBuffer(metadataBytes));
    }

    public void Close()
    {
      _fs.Close();
    }

    public FrameReader CreateFrameReader() =>
      new FrameReader(_fs, Metadata, _framesEndIdx);

    public System.DateTime GetStartTime() =>
      System.DateTime.FromBinary(Metadata.StartTime);
    public System.TimeSpan GetDuration() =>
      System.TimeSpan.FromSeconds(Metadata.Duration);
  }

  class ReplayException : System.Exception
  {
    public ReplayException(string message) : base($"Invalid replay: {message}") { }
  }

  class FrameReader
  {
    private int _idx;
    private FileStream _fs;
    private int _framesEndIdx;
    private int _levelIdx;
    private Bwr.Level? _nextLevel;
    private Bwr.Metadata _metadata;

    public FrameReader(FileStream fs, Bwr.Metadata metadata, int framesEndIdx)
    {
      _fs = fs;
      _framesEndIdx = framesEndIdx;
      _metadata = metadata;
      _levelIdx = 0;
      _nextLevel = metadata.Levels(_levelIdx);
    }

    private byte[] _readBytes(int count)
    {
      var bytes = new byte[count];
      _fs.Read(bytes, _idx, count);
      _idx += count;
      if (_idx > _framesEndIdx)
        throw new System.Exception("Invalid frame length (past end)");
      return bytes;
    }

    public (int?, Bwr.Frame?) Read()
    {
      if (_idx >= _framesEndIdx) return (null, null);
      int? sceneIdx = null;
      if (_nextLevel.HasValue && _idx >= _nextLevel.Value.FrameOffset)
      {
        sceneIdx = _nextLevel.Value.SceneIndex;
        _nextLevel = _metadata.Levels(++_levelIdx);
      }
      var frameLen = System.BitConverter.ToUInt16(_readBytes(2), 0);
      var frameBytes = _readBytes(frameLen);
      return (
        sceneIdx,
        Bwr.Frame.GetRootAsFrame(new FlatBuffers.ByteBuffer(frameBytes))
      );
    }
  }
}
