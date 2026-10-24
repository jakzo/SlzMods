param(
  [Parameter(Mandatory = $true)]
  [ValidateSet('Inspect', 'Devices', 'Compatibility', 'LoadScene', 'Prepare',
    'Matrix', 'Subset', 'Comparison', 'Status', 'Stop', 'Cleanup')]
  [string]$Action,
  [ValidateSet('absent', 'full', 'half')]
  [string]$Condition = 'absent',
  [string]$Requests,
  [switch]$IncludeSmgs
)

$ErrorActionPreference = 'Stop'
$performanceDirectory = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$dll = Join-Path $performanceDirectory 'bin/Gunfly/Debug/BoneworksPerformance.Gunfly.Debug.dll'
if (!(Test-Path -LiteralPath $dll)) {
  throw 'Build GunflyBenchmarks.csproj in Debug first.'
}
if ($IncludeSmgs -and $Action -ne 'Matrix') {
  throw '-IncludeSmgs applies only to Matrix.'
}
if ($Condition -eq 'half' -and $Action -in @('Matrix', 'Subset')) {
  throw 'Scored matrices require matched rendering and physics. Use absent or full.'
}

function Quote-CSharp([string]$Value) {
  return '@"' + $Value.Replace('"', '""') + '"'
}

$type = 'SessionTools'
$method = 'Inspect'
$values = @()
switch ($Action) {
  'Devices' { $method = 'Devices' }
  'Compatibility' { $method = 'GuardInvalidDevice' }
  'LoadScene' { $method = 'LoadSandbox' }
  'Prepare' { $type = 'SceneFixture'; $method = 'Prepare' }
  'Matrix' {
    $type = 'MatrixRunner'
    $method = 'StartMatrix'
    $values = @(
      (Quote-CSharp $Condition),
      $IncludeSmgs.IsPresent.ToString().ToLowerInvariant()
    )
  }
  'Subset' { $type = 'MatrixRunner'; $method = 'StartSubset' }
  'Comparison' { $type = 'ComparisonRunner'; $method = 'StartComparison' }
  'Status' { $type = 'MatrixRunner'; $method = 'Status' }
  'Stop' { $type = 'MatrixRunner'; $method = 'Stop' }
  'Cleanup' { $type = 'HarnessCleanup'; $method = 'Run' }
}

if ($Action -in @('Subset', 'Comparison')) {
  if (!$Requests) {
    throw 'Supply -Requests with a TSV path.'
  }
  $values = @(
    (Quote-CSharp (Resolve-Path -LiteralPath $Requests).Path),
    (Quote-CSharp $Condition)
  )
}

# Reflection avoids requiring a compiler reference to the helper.
$load = 'var a=System.Reflection.Assembly.LoadFrom(' + (Quote-CSharp $dll) + ');'
$call = $load + ' return a.GetType("Sst.BoneworksPerformance.Debug.Gunfly.' +
  $type + '").GetMethod("' + $method + '").Invoke(null,new object[]{' +
  ($values -join ',') + '});'
if ($Action -in @('Status', 'Stop')) {
  $call = $load + ' var n="Sst.BoneworksPerformance.Debug.Gunfly."; return ' +
    'a.GetType(n+"MatrixRunner").GetMethod("' + $method +
    '").Invoke(null,null)+"\n"+a.GetType(n+"ComparisonRunner").GetMethod("' +
    $method + '").Invoke(null,null);'
}

$argumentsJson = @{ code = $call; timeout = 30000 } | ConvertTo-Json -Compress
& (Join-Path $PSScriptRoot 'gunfly-mcp.ps1') -ArgumentsJson $argumentsJson
