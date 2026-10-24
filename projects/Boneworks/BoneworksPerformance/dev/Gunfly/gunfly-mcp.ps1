param(
  [string]$Tool = 'execute_csharp',
  [string]$CodeFile,
  [string]$ArgumentsJson = '{}'
)
$ErrorActionPreference = 'Stop'
$arguments = ConvertFrom-Json $ArgumentsJson
if ($CodeFile) {
  $arguments = @{ code = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $CodeFile)); timeout = 30000 }
}
$request = @{ jsonrpc = '2.0'; id = 1; method = 'tools/call'; params = @{ name = $Tool; arguments = $arguments } } | ConvertTo-Json -Depth 30 -Compress
$client = New-Object System.Net.Sockets.TcpClient
try {
  $client.Connect('127.0.0.1', 27015)
  $stream = $client.GetStream()
  $stream.ReadTimeout = 45000
  $writer = New-Object System.IO.StreamWriter($stream)
  $writer.AutoFlush = $true
  $reader = New-Object System.IO.StreamReader($stream)
  $writer.WriteLine($request)
  $reply = $reader.ReadLine()
  if (!$reply) {
    throw 'MelonMCP closed the connection without a response.'
  }
  $response = $reply | ConvertFrom-Json
  if ($response.error) {
    throw ($response.error | ConvertTo-Json -Compress)
  }
  if ($response.result.isError) {
    throw ($response.result.content | ConvertTo-Json -Depth 10 -Compress)
  }
  $reply
} finally { $client.Dispose() }
