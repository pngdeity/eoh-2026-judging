# TQ-2 Completion Report — Blazor Component Tests

**Branch:** `fix/audit-remediate`\
**Date:** 2026-05-26

## Summary

Added pure-logic utility classes extracted from page code-behind and
corresponding unit tests.

## Files Created

### Utility Classes (`src/ContestJudging.Web/Services/`)

| File                    | Description                                                                           |
| ----------------------- | ------------------------------------------------------------------------------------- |
| `JudgingUtilities.cs`   | `FindSuggestedPair`, `MapKeyToOperator`, `ValidateRelationEntries`, `GetOperatorText` |
| `EntryBulkImporter.cs`  | `ParseNewEntries` — bulk text dedup excluding existing                                |
| `LeaderboardBuilder.cs` | `Build` — sorts entries by total score, assigns ranks                                 |

### Test Files (`tests/ContestJudging.Web.Tests/`)

| File                          | Tests | Description                                                                        |
| ----------------------------- | ----- | ---------------------------------------------------------------------------------- |
| `JudgingTests.cs`             | 22    | PairFinder algorithm, keyboard routing, relation validation, operator display text |
| `SetupTests.cs`               | 7     | Bulk import parsing, dedup, existing-entry exclusion, edge cases                   |
| `ResultsTests.cs`             | 6     | Leaderboard sort order, rank assignment, empty/single/tied entries                 |
| `ServiceRegistrationTests.cs` | 1     | DI container verifies all key services registered                                  |

## Test Counts

| Category                 | Before | After  |
| ------------------------ | ------ | ------ |
| ModelValidationTests     | 8      | 8      |
| JudgingTests             | —      | 22     |
| SetupTests               | —      | 7      |
| ResultsTests             | —      | 6      |
| ServiceRegistrationTests | —      | 1      |
| **Total**                | **8**  | **44** |

## Traits Added

Added `[Trait("Category", "Unit")]` or `[Trait("Category", "Integration")]` to
all test classes across all projects:

| Project                    | File                        | Trait       |
| -------------------------- | --------------------------- | ----------- |
| `ContestJudging.Tests`     | `InfrastructureTests.cs`    | Integration |
| `ContestJudging.Tests`     | `CoreTests.cs`              | Unit        |
| `ContestJudging.Tests`     | `BackupServiceTests.cs`     | Unit        |
| `ContestJudging.Tests`     | `ScoringStrategyTests.cs`   | Unit        |
| `ContestJudging.Tests`     | `ResolutionServiceTests.cs` | Unit        |
| `ContestJudging.Tests`     | `ContestManagerTests.cs`    | Unit        |
| `ContestJudging.Tests`     | `PartitionServiceTests.cs`  | Unit        |
| `ContestJudging.Tests`     | `ValidationServiceTests.cs` | Unit        |
| `ContestJudging.Tests`     | `TrimmingSafetyTests.cs`    | Unit        |
| `ContestJudging.Web.Tests` | `ModelValidationTests.cs`   | Unit        |
| `ContestJudging.E2ETests`  | `AppE2ETests.cs`            | E2E (NUnit) |

### Other Changes

- Added `Moq` package reference to
  `tests/ContestJudging.Web.Tests/ContestJudging.Web.Tests.csproj` for DI
  registration test

## Verification

```
dotnet test tests/ContestJudging.Web.Tests/ContestJudging.Web.Tests.csproj -c Release --verbosity normal
```

**Result:** 44 passed, 0 failed, 0 skipped
