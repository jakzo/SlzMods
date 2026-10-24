# BoneworksFrameFix pose-history tests

Run the deterministic pose math and concurrent-buffer tests with:

```powershell
dotnet run --project tests/BoneworksFrameFix.Tests/BoneworksFrameFix.Tests.csproj -c Debug
```

Button regressions cover native samples ahead of or behind the action cache,
sample-time tolerance, original event timestamps after stalls, stale/zero-filled
native data, source-switch deduplication, and jump-release delivery. They do not
replace headset tests of the actual controller bindings and native capture.

Pass a generated profile directory to check both CSV schemas, row counts,
strict timestamp ordering, sampler rate, OpenVR query errors, p95 query time,
and HTML links:

```powershell
dotnet run --project tests/BoneworksFrameFix.Tests/BoneworksFrameFix.Tests.csproj -c Debug -- "C:\path\to\profile"
```
