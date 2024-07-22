using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace Sst.Utilities {
public static class Resources {
  public static void ExtractResource(string resourceName, string dir) {
    var assembly = Assembly.GetExecutingAssembly();
    string resourcePath = assembly.GetManifestResourceNames().Single(
        str => str.EndsWith(resourceName));
    using (var stream = assembly.GetManifestResourceStream(resourcePath)) {
      using (var archive = new ZipArchive(stream, ZipArchiveMode.Read)) {
        foreach (var entry in archive.Entries) {
          var entryStream = entry.Open();
          using (var fileStream =
                     File.Create(Path.Combine(dir, entry.FullName))) {
            entryStream.CopyTo(fileStream);
          }
        }
      }
    }
  }
}
}