# ST-3 Remediation Completion Report

**Branch:** `fix/audit-remediate`\
**Agent:** ST-3\
**Date:** 2026-05-26\
**Tier:** 2 — Algorithm and Data Access findings

## Findings Resolved

### ALGO-003: UnionFind lacks union-by-rank

**File:** `src/ContestJudging.Services/Validation/GraphValidationService.cs`\
**Fix:** Added `_rank` dictionary to the `UnionFind` private sealed class. The
`Union` method now uses union-by-rank: attaches the shorter tree under the
taller tree, incrementing rank when equal.\
**Status:** Resolved

### ALGO-004: BradleyTerry NaN guard

**File:**
`src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs`\
**Fix:** Added a `1e-15` floor guard on the `gamma[i] + gamma[j]` denominator
inside the inner loop to prevent division by near-zero values that could produce
NaN.\
**Status:** Resolved

### ALGO-005: Partition bridge count floor

**File:** `src/ContestJudging.Services/Partitioning/PartitionService.cs`\
**Fix:** Added `Math.Max(1, ...)` floor on `(int)Math.Round(n * overlapRate)`
when `overlapRate > 0`, ensuring at least one bridge node when overlap is
requested. When `overlapRate == 0`, bridge count remains 0 (preserving
disjoint-set semantics).\
**Status:** Resolved

### CQ-007 / EF-004: O(n*m) client-side join

**File:** `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs`

- Added `CategoryEntity Category` navigation property to `EntryScoreEntity`.
- Updated FK configuration from `HasOne<CategoryEntity>()` to
  `HasOne(es => es.Category)`.

**File:** `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs`

- `SqliteEntryRepository.GetByIdAsync`: Replaced manual
  `categories.FirstOrDefault(c => c.Id == scoreEntity.CategoryId)` loop with
  `.Include(e => e.Scores).ThenInclude(es => es.Category)` + direct
  `scoreEntity.Category` property access.
- `SqliteEntryRepository.GetAllAsync`: Same ThenInclude pattern, eliminating the
  linear category scan per score. **Status:** Resolved

### EF-006: No transaction wrapping

**File:** `src/ContestJudging.Services/Managers/ContestManager.cs`\
**Fix:** Added `// TODO (EF-006, Tier 3)` comment above the batch update loop in
`CalculateGlobalScoresAsync`, noting that a transaction wrap would improve
atomicity and performance for large datasets.\
**Status:** Deferred to Tier 3 (per instructions)

## Collateral Fixes

The branch had pre-existing interface changes (CancellationToken parameters on
`ICategoryRepository`, `IEntryRepository`, `IRelationRepository`,
`IDatabaseBackupService`) whose implementations were missing. These were
completed to make the solution compile:

- **SqliteRepositories.cs:** All async methods updated with
  `CancellationToken cancellationToken = default` parameter, passed through to
  EF Core calls.
- **DatabaseBackupService.cs:** `ExportAsync`/`ImportAsync` updated with
  `CancellationToken` parameter, passed to
  `File.ReadAllBytesAsync`/`File.WriteAllBytesAsync`.
- **InfrastructureTests.cs:** Updated test constructors to pass
  `Mock.Of<ILogger<T>>()` (loggers were added by a prior agent).

## Build & Test Verification

```
dotnet build ContestJudging.slnx -c Release  → 0 errors
dotnet test tests/ContestJudging.Tests/ContestJudging.Tests.csproj -c Release
  Total tests: 51
  Passed: 51
  Failed: 0
```

All assertions verified: UnionFind rank correctness, BradleyTerry NaN safety,
partition bridge count floor, ThenInclude join optimization, CancellationToken
pass-through.
