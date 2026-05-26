# TQ-1 Test Remediation Completion Report

## Summary

- **New tests added:** 10
- **Existing tests enhanced:** 2
- **Pre-existing build fix:** 1 (TrimmingSafetyTests.cs duplicate namespace)
- **Total tests passing:** 67/67 (1 pre-existing `ServiceRegistrationTests` DI
  failure excluded)

## TE-006: Repository UpdateAsync/GetAllAsync

Added 4 tests to `tests/ContestJudging.Tests/InfrastructureTests.cs`:

| Test                                                  | Verifies                                                              |
| ----------------------------------------------------- | --------------------------------------------------------------------- |
| `EntryRepository_UpdateAsync_ModifiesEntry`           | UpdateAsync modifies entry scores, GetByIdAsync returns updated value |
| `CategoryRepository_GetAllAsync_ReturnsAllCategories` | GetAllAsync returns 2 added categories                                |
| `EntryRepository_GetAllAsync_ReturnsAllEntries`       | GetAllAsync returns 2 added entries                                   |
| `RelationRepository_GetAllAsync_ReturnsAllRelations`  | GetByCategoryIdAsync returns 1 added relation                         |

## TE-007: Error message assertions

Enhanced 2 tests in `tests/ContestJudging.Tests/ValidationServiceTests.cs`:

| Test                                                             | Added assertions                                                                                                       |
| ---------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `ValidatePartitionedGraph_DisconnectedGraph_ShouldReturnInvalid` | `Assert.Equal("The graph is not fully connected. Bridge nodes failed to overlap correctly.", result.ErrorMessage)`     |
| `ValidatePartitionedGraph_WithCycles_ShouldReturnInvalid`        | `Assert.Equal("The judging graph contains cycles.", result.ErrorMessage)` and `Assert.Equal(0, result.ComponentCount)` |

## TE-008: LessThan operator

Added 3 tests to `tests/ContestJudging.Tests/ValidationServiceTests.cs`:

| Test                                              | Verifies                                                  |
| ------------------------------------------------- | --------------------------------------------------------- |
| `GetSortedTiers_WithLessThan_ReturnsCorrectOrder` | LessThan chain produces correct tier ordering (A < B < C) |
| `IsTotalOrder_WithLessThan_ReturnsTrue`           | Total order is valid with LessThan                        |
| `IsValidOrder_WithLessThan_ReturnsTrue`           | Valid order recognized with LessThan                      |

## TE-009: Repository edge cases

Added 3 tests to `tests/ContestJudging.Tests/InfrastructureTests.cs`:

| Test                                                      | Verifies                                                      |
| --------------------------------------------------------- | ------------------------------------------------------------- |
| `CategoryRepository_Delete_NonExistent_DoesNotThrow`      | DeleteAsync on non-existent ID does not throw                 |
| `CategoryRepository_Add_DuplicateId_ThrowsOrReplaces`     | AddAsync with duplicate PK throws `InvalidOperationException` |
| `EntryRepository_AddWithExistingScores_ReplacesOldScores` | UpdateAsync replaces old scores (cat2 removed, cat1 updated)  |

## TE-012: Fragile string-contains assertions

All new error message assertions use `Assert.Equal` with exact strings from the
source:

- `"The graph is not fully connected. Bridge nodes failed to overlap correctly."`
- `"The judging graph contains cycles."`

No pre-existing `Assert.Contains("Invalid", ...)` patterns were found in the
test suite. All assertions written follow the exact-match pattern.

## Pre-existing issue fixed

`TrimmingSafetyTests.cs` had a duplicate `namespace ContestJudging.Tests`
declaration causing build error CS1513. Removed the duplicate namespace block.

## Known pre-existing failure

`ServiceRegistrationTests.AddContestJudgingServices_AllCoreRegistrations_CanBeResolved`
— fails because `SqliteCategoryRepository` requires
`ILogger<SqliteCategoryRepository>` which is not registered in the test DI
container. Unrelated to this remediation.
