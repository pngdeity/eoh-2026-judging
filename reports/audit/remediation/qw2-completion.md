# QW-2 Remediation: Seal Concrete DI-Registered Classes

**Date:** 2026-05-26 **Findings Addressed:** CQ-005 (unsealed classes), CQ-010
(UnionFind already sealed) **Status:** Complete

## Summary

Added `sealed` keyword to 11 concrete classes that are DI-registered services or
not designed for inheritance. This enables JIT optimizations (devirtualization,
inlining) and clarifies design intent.

## Classes Sealed

### Repository Layer

| Class                      | File                                                                       |
| -------------------------- | -------------------------------------------------------------------------- |
| `SqliteCategoryRepository` | `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:13`  |
| `SqliteEntryRepository`    | `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:62`  |
| `SqliteRelationRepository` | `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:171` |

### Service Layer

| Class                           | File                                                                        |
| ------------------------------- | --------------------------------------------------------------------------- |
| `GraphValidationService`        | `src/ContestJudging.Services/Validation/GraphValidationService.cs:9`        |
| `PartitionService`              | `src/ContestJudging.Services/Partitioning/PartitionService.cs:7`            |
| `BradleyTerryResolutionService` | `src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:9` |
| `BackupService`                 | `src/ContestJudging.Services/Managers/BackupService.cs:7`                   |
| `ContestManager`                | `src/ContestJudging.Services/Managers/ContestManager.cs:14`                 |
| `DatabaseBackupService`         | `src/ContestJudging.Infrastructure/Persistence/DatabaseBackupService.cs:5`  |

### Scoring Strategies

| Class                    | File                                                              |
| ------------------------ | ----------------------------------------------------------------- |
| `LinearSpacingScoring`   | `src/ContestJudging.Services/Scoring/LinearSpacingScoring.cs:8`   |
| `PercentileScoring`      | `src/ContestJudging.Services/Scoring/PercentileScoring.cs:9`      |
| `DefinedIntervalScoring` | `src/ContestJudging.Services/Scoring/DefinedIntervalScoring.cs:8` |

## Classes Intentionally Skipped

- **Entity classes** (`CategoryEntity`, `EntryEntity`, `EntryScoreEntity`,
  `RelationEntity`) — EF Core may need proxy support for lazy loading
- **`ContestDbContext`** — Inherits from EF Core `DbContext` base class
- **Blazor pages** (`Setup`, `Judging`, `Results`) — `partial class` components;
  Blazor requires them to be inheritable for rendering pipeline
- **`ServiceCollectionExtensions`** — `static class` is implicitly sealed
- **`ValidationResult`** — `record`, not `class`
- **`UnionFind`** — Already `sealed` (private nested class in
  `GraphValidationService`)
- **Nested model classes** (`CategoryModel`, `EntryModel`, `LeaderboardItem`) —
  Used for Blazor data binding, not DI-registered services

## Verification

```
dotnet build src/ContestJudging.Services/ContestJudging.Services.csproj -c Release  → 0 errors, 0 warnings
dotnet build src/ContestJudging.Infrastructure/ContestJudging.Infrastructure.csproj -c Release  → 0 errors, 0 warnings
```
