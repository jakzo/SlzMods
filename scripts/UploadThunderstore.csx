#r "System.Net.Http"
using System.Net.Http;

var zipFilename = Directory.GetFiles("thunderstore").First(filename => filename.EndsWith(".zip"));
var zipBytes = File.ReadAllBytes($"thunderstore/{zipFilename}");

var client = new HttpClient();

var uploadContent = new MultipartFormDataContent();
uploadContent.Headers.Add("Authorization", Environment.GetEnvironmentVariable("THUNDERSTORE_API_TOKEN"));
uploadContent.Add(new ByteArrayContent(zipBytes), "newfile", zipFilename);
var uploadRes = await client.PostAsync("https://boneworks.thunderstore.io/api/experimental/submission/upload/", uploadContent);
uploadRes.EnsureSuccessStatusCode();
var uploadBody = await uploadRes.Content.ReadAsStringAsync();
var uuid = uploadBody;

Console.WriteLine($"Uploaded {zipFilename} to Thunderstore with UUID {uuid}");

var submitContent = new StringContent($@"{{
""author_name"": ""jakzo"",
""categories"": [],
""communities"": [],
""has_nsfw_content"": false,
""upload_uuid"": ""{uuid}""
}}");
submitContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
uploadContent.Headers.Add("Authorization", Environment.GetEnvironmentVariable("THUNDERSTORE_API_TOKEN"));
var submitRes = await client.PostAsync("/api/experimental/submission/submit/", submitContent);
submitRes.EnsureSuccessStatusCode();

Console.WriteLine("Submitted to Thunderstore");
