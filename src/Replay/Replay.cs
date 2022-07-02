using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SpeedrunTools.Replay
{
  class Replay
  {
    public Bwr.File File;

    public Replay(Bwr.File file)
    {
      File = file;
    }

    public static Replay ReadFromFile(string filename)
    {
      var filePath = Path.Combine(Utils.DIR, filename);
      var fileBytes = System.IO.File.ReadAllBytes(filePath);
      var file = Bwr.File.GetRootAsFile(new FlatBuffers.ByteBuffer(fileBytes));
      return new Replay(file);
    }

    public System.DateTime GetStartTime() =>
      System.DateTime.FromBinary(File.Metadata.Value.StartTime);
    public System.TimeSpan GetDuration() =>
      System.DateTime.FromBinary(File.Metadata.Value.StartTime);
  }
}
