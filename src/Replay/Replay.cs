using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SpeedrunTools.Replay
{
  class Replay
  {
    private FileStream _fs;

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

      var metadataBytes = new byte[_fs.Length - metadataIdx];
      _fs.Read(metadataBytes, metadataIdx, metadataBytes.Length);
      Metadata = Bwr.Metadata.GetRootAsMetadata(new FlatBuffers.ByteBuffer(metadataBytes));
    }

    public void Close()
    {
      _fs.Close();
    }

    public System.DateTime GetStartTime() =>
      System.DateTime.FromBinary(Metadata.StartTime);
    public System.TimeSpan GetDuration() =>
      System.TimeSpan.FromSeconds(Metadata.Duration);
  }

  class ReplayException : System.Exception
  {
    public ReplayException(string message) : base($"Invalid replay: {message}") { }
  }
}
