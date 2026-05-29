# QW-4 Completion Report — Tier 2/3 Style Cleanup

## Summary

All 6 findings remediated. Build verified: 0 errors, 0 warnings across all
affected projects.

---

## 1. CQ-008 — Magic Numbers

### BradleyTerryResolutionService.cs

- Added 3 named constants:
  - `private const int RankStabilityStartIteration = 50;`
  - `private const int RankStabilityCheckFrequency = 10;`
  - `private const double RankStabilityTolerance = 1e-3;`
- Replaced `iter > 50 && iter % 10 == 0` →
  `iter > RankStabilityStartIteration && iter % RankStabilityCheckFrequency == 0`
- Replaced `maxDiff < 1e-3` → `maxDiff < RankStabilityTolerance`
- Existing constants `MaxIterations = 1000` and `ConvergenceThreshold = 1e-6`
  were already in place.

### PartitionService.cs

- No magic numbers found. Boundary values `0` and `1` are parameter validation
  guards, not magic numbers.

### GraphValidationService.cs

- No magic numbers found.

---

## 2. CQ-009 — Tuple→ValueTuple

### Judging.razor.cs

- `private Tuple<string, string>? suggestedPair` →
  `private (string A, string B)? suggestedPair`
- `new Tuple<string, string>(a, b)` → `(a, b)`
- All `.Item1` → `.Value.A`, all `.Item2` → `.Value.B`

### Judging.razor

- `@suggestedPair.Item1` → `@suggestedPair.Value.A`
- `@suggestedPair.Item2` → `@suggestedPair.Value.B`

---

## 3. STRUCT-004 — E2ETests Missing RootNamespace

### ContestJudging.E2ETests.csproj

- Added `<RootNamespace>ContestJudging.E2ETests</RootNamespace>`
- Added `<AssemblyName>ContestJudging.E2ETests</AssemblyName>`
- Removed the redundant `<TargetFramework>net10.0</TargetFramework>` (inherited
  from Directory.Build.props)

---

## 4. STRUCT-008 — Redundant TargetFramework

### ContestJudging.Web.csproj

- Removed `<TargetFramework>net10.0</TargetFramework>` (inherited from
  Directory.Build.props).
- Not `net10.0-browser`, so safe to remove.

---

## 5. TEST-006 — Vague "CoreTests" Name

### CoreTests.cs (tests/ContestJudging.Tests/)

- Class renamed: `CoreTests` → `EntityValidationTests`
- Tests validate Entity constructors and validation logic (Category, Entry).

---

## 6. BW-009 — Orphaned weather.json

- Deleted `src/ContestJudging.Web/wwwroot/sample-data/weather.json` — Blazor
  scaffold artifact.
- Also removed the now-empty `sample-data/` directory.

---

## 7. CQ-001 — Class1.cs Triplicates (Verify)

- Confirmed: `Class1.cs` no longer exists anywhere in the repo. Already
  resolved.

---

## Build Verification

```
dotnet build tests/ContestJudging.Tests/ContestJudging.Tests.csproj -c Release  → 0 errors, 0 warnings
dotnet build tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj -c Release → 0 errors, 0 warnings
dotnet build src/ContestJudging.Web/ContestJudging.Web.csproj -c Release              → 0 errors, 0 warnings
```
