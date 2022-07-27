#r "System.Net.Http"
#r "System.IO.Compression"
#r "System.IO.Compression.ZipFile"
#r "../packages/Newtonsoft.Json.13.0.1/lib/net45/Newtonsoft.Json.dll"

using System.Net.Http;
using System.IO.Compression;
using Newtonsoft.Json;

try {
  var newVersion = Args[0];

  var readme = File.ReadAllText("README.md");
  var changelog = File.ReadAllText("CHANGELOG.md");
  File.WriteAllText("thunderstore/README.md",
                    $"{readme}\n# Changelog\n\n{changelog}");
  const string MODS_DIR = "thunderstore/Mods";
  if (Directory.Exists(MODS_DIR)) {
    foreach (var file in Directory.GetFiles(MODS_DIR))
      File.Delete(file);
  } else {
    Directory.CreateDirectory(MODS_DIR);
  }
  File.Copy("bin/Release/SpeedrunTools.dll", MODS_DIR);

  Console.WriteLine("Thunderstore files copied");

  var zipFilename = $"SpeedrunTools_{newVersion}.zip";
  var zipPath = $"thunderstore/{zipFilename}";
  using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create)) {
    zip.CreateEntryFromFile("thunderstore/manifest.json", "manifest.json");
    zip.CreateEntryFromFile("thunderstore/icon.png", "icon.png");
    zip.CreateEntryFromFile("thunderstore/README.md", "README.md");
    zip.CreateEntryFromFile("thunderstore/Mods/SpeedrunTools.dll",
                            "Mods/SpeedrunTools.dll");
  }

  Console.WriteLine("Thunderstore zip file created");

  var zipBytes = File.ReadAllBytes(zipPath);

  string AUTH_TOKEN =
      Environment.GetEnvironmentVariable("THUNDERSTORE_API_TOKEN");
  if (AUTH_TOKEN == null)
    throw new Exception("THUNDERSTORE_API_TOKEN not set");

  var client = new HttpClient();

  async Task<T> Post<T>(string url, object body) {
    var res = await client.SendAsync(new HttpRequestMessage {
      Method = HttpMethod.Post,
      RequestUri =
          new Uri($"https://boneworks.thunderstore.io/api/experimental{url}"),
      Content = new StringContent(JsonConvert.SerializeObject(body),
                                  Encoding.UTF8, "application/json"),
      Headers =
          {
            { "Authorization", $"Bearer {AUTH_TOKEN}" },
          },
    });
    res.EnsureSuccessStatusCode();
    var resText = await res.Content.ReadAsStringAsync();
    return JsonConvert.DeserializeObject<T>(resText);
  }

  var resInitUpload =
      await Post<ResInitUpload>("/usermedia/initiate-upload/", new {
        filename = zipFilename,
        file_size_bytes = zipBytes.Length,
      });
  Console.WriteLine("Upload initiated");

  var parts = new List<ReqFinishUploadPart>();
  foreach (var uploadUrl in resInitUpload.upload_urls) {
    var bytes = new byte[uploadUrl.length];
    Array.Copy(zipBytes, uploadUrl.offset, bytes, 0, uploadUrl.length);
    var res = await client.PutAsync(uploadUrl.url, new ByteArrayContent(bytes));
    res.EnsureSuccessStatusCode();
    parts.Add(new ReqFinishUploadPart {
      PartNumber = uploadUrl.part_number,
      ETag = res.Headers.ETag.Tag,
    });
  }

  var uuid = resInitUpload.user_media.uuid;
  Console.WriteLine($"Uploaded {zipFilename} to Thunderstore with UUID {uuid}");

  await Post<object>($"/usermedia/{uuid}/finish-upload/", new { parts });
  await Post<object>("/submission/submit/", new {
    upload_uuid = uuid,
    author_name = "jakzo",
    categories = new string[] { "code-mods" },
    communities = new string[] { "boneworks" },
    has_nsfw_content = false,
  });

  Console.WriteLine("Submitted to Thunderstore");
} catch (Exception ex) {
  Console.WriteLine(ex);
  Environment.Exit(1);
}

// Types
class ResInitUpload {
  public ResUserMedia user_media;
  public List<ResUploadUrl> upload_urls;
}
class ResUserMedia {
  public string uuid;
}
class ResUploadUrl {
  public int part_number;
  public string url;
  public int offset;
  public int length;
}
class ReqFinishUploadPart {
  public string ETag;
  public int PartNumber;
}
