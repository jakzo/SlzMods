# BoneworksPerformance pose-history tests

Run the deterministic pose math and concurrent-buffer tests with:

```powershell
dotnet run --project tests/BoneworksPerformance.Tests/BoneworksPerformance.Tests.csproj -c Debug
```

Pass a generated profile directory to check both CSV schemas, row counts,
strict timestamp ordering, sampler rate, OpenVR query errors, p95 query time,
and HTML links:

```powershell
dotnet run --project tests/BoneworksPerformance.Tests/BoneworksPerformance.Tests.csproj -c Debug -- "C:\path\to\profile"
```
