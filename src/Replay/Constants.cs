using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeedrunTools.Replay {
class Constants {
  public const string MAGIC_HEADER = "BwRp";
  public const string REPLAY_EXTENSION = "bwr";

  public static readonly byte[] MAGIC_HEADER_BYTES =
      Encoding.ASCII.GetBytes(MAGIC_HEADER);
  public static readonly byte[] FILE_START_BYTES =
      MAGIC_HEADER_BYTES.Concat(System.BitConverter.GetBytes((uint)0))
          .ToArray();
  public static int METADATA_OFFSET_INDEX = MAGIC_HEADER_BYTES.Length;
}
}
