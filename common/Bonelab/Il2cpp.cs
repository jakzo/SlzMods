using System.Collections.Generic;

namespace Sst.Utilities {
public class Il2Cpp {
  public static Dictionary<K, V>
  ToDictionary<K, V>(Il2CppSystem.Collections.Generic.Dictionary<K, V> from) {
    Dictionary<K, V> dict = new Dictionary<K, V>();
    foreach (var entry in from)
      dict.Add(entry.key, entry.value);
    return dict;
  }
}
}
