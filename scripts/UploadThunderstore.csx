#r "System.Net.Http"
#r "System.IO.Compression"
#r "System.IO.Compression.ZipFile"

using System.Net.Http;
using System.IO.Compression;

var newVersion = Args[0];

var readme = File.ReadAllText("README.md");
var changelog = File.ReadAllText("CHANGELOG.md");
File.WriteAllText("thunderstore/README.md", $"{readme}\n# Changelog\n\n{changelog}");
const string MODS_DIR = "thunderstore/Mods";
if (Directory.Exists(MODS_DIR))
{
  foreach (var file in Directory.GetFiles(MODS_DIR)) File.Delete(file);
} else
{
  Directory.CreateDirectory(MODS_DIR);
}
File.Copy("bin/Release/SpeedrunTools.dll", MODS_DIR);

Console.WriteLine("Thunderstore files copied");

var thunderstoreZipPath = $"thunderstore/SpeedrunTools_{newVersion}.zip";
using (ZipArchive zip = ZipFile.Open(thunderstoreZipPath, ZipArchiveMode.Create))
{
  zip.CreateEntryFromFile("thunderstore/manifest.json", "manifest.json");
  zip.CreateEntryFromFile("thunderstore/icon.png", "icon.png");
  zip.CreateEntryFromFile("thunderstore/README.md", "README.md");
  zip.CreateEntryFromFile("thunderstore/Mods/SpeedrunTools.dll", "Mods/SpeedrunTools.dll");
}

Console.WriteLine("Thunderstore zip file created");

// var zipFilename = Directory.GetFiles("thunderstore").First(filename => filename.EndsWith(".zip"));
// var zipBytes = File.ReadAllBytes($"thunderstore/{zipFilename}");

// var client = new HttpClient();

// var uploadContent = new MultipartFormDataContent();
// uploadContent.Headers.Add("Authorization", Environment.GetEnvironmentVariable("THUNDERSTORE_API_TOKEN"));
// uploadContent.Add(new ByteArrayContent(zipBytes), "newfile", zipFilename);
// var uploadRes = await client.PostAsync("https://boneworks.thunderstore.io/api/experimental/submission/upload/", uploadContent);
// uploadRes.EnsureSuccessStatusCode();
// var uploadBody = await uploadRes.Content.ReadAsStringAsync();
// var uuid = uploadBody;

// Console.WriteLine($"Uploaded {zipFilename} to Thunderstore with UUID {uuid}");

// var submitContent = new StringContent($@"{{
// ""author_name"": ""jakzo"",
// ""categories"": [],
// ""communities"": [],
// ""has_nsfw_content"": false,
// ""upload_uuid"": ""{uuid}""
// }}");
// submitContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
// uploadContent.Headers.Add("Authorization", Environment.GetEnvironmentVariable("THUNDERSTORE_API_TOKEN"));
// var submitRes = await client.PostAsync("/api/experimental/submission/submit/", submitContent);
// submitRes.EnsureSuccessStatusCode();

// Console.WriteLine("Submitted to Thunderstore");
