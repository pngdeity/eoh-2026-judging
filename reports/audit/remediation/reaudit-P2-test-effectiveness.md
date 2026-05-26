# Re-Audit Report: P2-E — Test Effectiveness

**Agent:** PA2-E (re-audit)\
**Domain:** test-effectiveness\
**Date:** 2026-05-26\
**Branch:** `fix/audit-remediate` (working tree, uncommitted)\
**Test delta:** 33 → 59 (+26)

---

## Score (1–10)

| Metric      | Before | After |
| ----------- | ------ | ----- |
| **Overall** | **4**  | **6** |

**Summary:** High-severity findings largely addressed (3/5 fixed, 2/5
partially). Medium/low-severity findings almost entirely unresolved (1 fixed, 1
partial out of 11). Core algorithmic coverage improved substantially.
Infrastructure, validation edge-cases, and E2E tests remain weak.

---

## Part 1: New Test Evaluation

### 1.1 BackupServiceTests (5 tests)

| Test                                                | Assertions                                                  | Quality                                            |
| --------------------------------------------------- | ----------------------------------------------------------- | -------------------------------------------------- |
| `SaveBackupAsync_StoresBase64AndVersion`            | `Verify` on both `SetItemAsync` calls                       | Strong — verifies correct keys and values via Moq  |
| `TryRestoreBackupAsync_NoBackup_ReturnsNull`        | `Assert.Null`                                               | Adequate — covers early-exit path                  |
| `TryRestoreBackupAsync_VersionMismatch_ReturnsNull` | `Assert.Null` + `Verify RemoveItemAsync("db_backup")`       | Good — covers schema migration path                |
| `TryRestoreBackupAsync_ValidBackup_Restores`        | `Assert.Equal(expectedData, result)` + `Verify ImportAsync` | Strong — round-trip assertion, verifies delegation |
| `TryRestoreBackupAsync_CorruptBase64_ReturnsNull`   | `Assert.Null`                                               | Adequate — covers catch path                       |

**Mock Setup:**

- `ILocalStorageService` (Blazored) — correctly mocked with `Setup` for
  key-based async operations
- `IDatabaseBackupService` — correctly verified for `ImportAsync` delegation
- `ILogger<BackupService>` — instantiated but never verified. No effect on test
  quality.

**Gaps in BackupService coverage:**

1. `null`/empty base64 path (`BackupService.cs:44-45`) — not tested
2. `ImportAsync` throws exception path (`BackupService.cs:54-57`) — not tested
3. Version mismatch test only verifies removal of `"db_backup"`, not
   `"db_schema_version"` — missing assert on line 39 of BackupService.cs

**Verdict:** Good quality, good isolation. 3 uncovered paths remain. **Score
contribution: +1.0**

---

### 1.2 ContestManager ExportDataAsync/ImportDataAsync (2 tests)

| Test                                       | Assertions                                               | Quality                                           |
| ------------------------------------------ | -------------------------------------------------------- | ------------------------------------------------- |
| `ExportDataAsync_DelegatesToBackupService` | `Assert.Equal` on byte array + `Verify ExportAsync Once` | Strong — precise value match                      |
| `ImportDataAsync_DelegatesToBackupService` | `Verify ImportAsync(data) Once`                          | Strong — verifies delegation with exact parameter |

**Context:** `ContestManager` now uses `IDatabaseBackupService` interface
(replacing concrete `ContestDbContext`). Tests use `Mock.Of<T>()` helpers for
all other dependencies — clean and concise.

**Verdict:** Strong delegation verification. Previously structurally untestable
code is now properly abstracted and tested. **Score contribution: +0.4**

---

### 1.3 ResolutionService Convergence Tests (3 new tests)

| Test                                    | What it exercises                           | Quality                                         |
| --------------------------------------- | ------------------------------------------- | ----------------------------------------------- |
| `LinearOrder_ConvergesWithCorrectOrder` | 20-entry linear chain (E1 > E2 > ... > E20) | Strong — forces 19 assertions across all ranks  |
| `EmptyInput_ReturnsEmpty`               | n=0 guard clause (line 18)                  | Adequate                                        |
| `SingleEntry_ReturnsLogZero`            | n=1 guard clause                            | Adequate — specific assertion on log(1.0) = 0.0 |

**Convergence path analysis:**

- The 20-entry linear chain drives the algorithm past `iter > 50` (line 90),
  exercising the rank-stability early-exit path (lines 90–115) that was
  previously unreachable with ≤3 entries.
- The existing tests (2–3 entries) converge in ≤1 iteration; new test reaches
  deeper iteration counts.
- `Assert.True(strengths["E1"] > strengths["E2"])` pattern is sufficient but
  could be strengthened with exact convergence-count assertions.

**Verdict:** Previously untested convergence path now exercised. Edge cases
covered. **Score contribution: +0.5**

---

### 1.4 ScoringStrategy CalculateScoresFromStrengths Tests (5 tests)

| Test                                    | Path exercised                          | Assertion quality                                                                          |
| --------------------------------------- | --------------------------------------- | ------------------------------------------------------------------------------------------ |
| `LinearSpacing_VariedStrengths`         | Normalization with `range > 1e-9`       | Weak — `Assert.True(scores["A"] > 90.0)` is fuzzy; should assert exact `Math.Round` output |
| `LinearSpacing_AllSameStrength`         | `range < 1e-9` guard (line 57)          | Strong — exact value match (`100`)                                                         |
| `LinearSpacing_SingleEntry`             | Single-entry path                       | Strong — exact value match (`10`)                                                          |
| `Percentile_RanksByStrengthPercentile`  | Percentile scoring via sorted strengths | Weak — ordinal-only assertion (`>` chain)                                                  |
| `DefinedInterval_LinearScalingFallback` | Fallback scaling for tiered strategy    | Weak — ordinal-only assertion                                                              |

**Gaps:**

- `PercentileScoring.CalculateScoresFromStrengths` single-entry path (line
  45–49) — not tested
- `PercentileScoring.CalculateScoresFromStrengths` empty-dict path (line 44) —
  not tested
- `DefinedIntervalScoring.CalculateScoresFromStrengths` same-strength path
  (range < 1e-9) — not tested
- Assertions on 3 of 5 tests are ordinal-only (`A > B > C`) rather than exact
  score values

**Verdict:** Core paths covered. Assertion strength could be tighter on 3/5
tests. **Score contribution: +0.6**

---

### 1.5 Theory Conversion — CoreTests (3 new InlineData cases)

| Change                                               | Was                        | Now                                                                      |
| ---------------------------------------------------- | -------------------------- | ------------------------------------------------------------------------ |
| `Category_Constructor_ThrowsWhenMaxScoreIsOneOrLess` | `[Fact]` with single value | `[Theory]` with `[InlineData(1)]`, `[InlineData(0)]`, `[InlineData(-1)]` |

**Verdict:** Correct values, correct behavior. Trivial improvement. **Score
contribution: +0.1**

---

### 1.6 Web.Tests ModelValidationTests (8 tests)

| Test                                                 | What it validates                                                    | Value                                                          |
| ---------------------------------------------------- | -------------------------------------------------------------------- | -------------------------------------------------------------- |
| `CategoryModel_ValidValues_SucceedsValidation`       | `Id = "cat1"`, `MaxScore = 10`                                       | Useful — confirms annotations accept valid input               |
| `CategoryModel_EmptyId_FailsValidation`              | `Id = ""` triggers `[Required]`                                      | Useful                                                         |
| `CategoryModel_MaxScoreBelowMinimum_FailsValidation` | Theory with `[InlineData(1)]`, `[InlineData(0)]`, `[InlineData(-5)]` | Useful — validates `[Range(1.1, double.MaxValue)]`             |
| `EntryModel_ValidValues_SucceedsValidation`          | `Id = "entry1"`                                                      | Basic                                                          |
| `EntryModel_EmptyId_FailsValidation`                 | `Id = ""` triggers `[Required]`                                      | Useful                                                         |
| `LeaderboardItem_StoresEntryCorrectly`               | `item.Entry = entry` + `Assert.Same`                                 | **Very weak** — tests a property setter/getter with zero logic |

**Assessment:** Tests real `DataAnnotations` validation (which is application
configuration, not just framework behavior). The `LeaderboardItem` test is pure
smoke (no logic beyond `= default!`). Worth keeping all but the
`LeaderboardItem` test. **Score contribution: +0.3**

---

## Part 2: Original Finding Verification

### TE-001 — Unseeded Random (HIGH)

**Status: RESOLVED**

`PartitionServiceTests.cs:17`: `new PartitionService(new Random(42))`\
`PartitionServiceTests.cs:42`: `new PartitionService(new Random(42))`\
Both test instantiations now use seeded `Random`. Deterministic runs confirmed.

---

### TE-002 — Export/Import Structurally Untestable (HIGH)

**Status: RESOLVED**

`ContestManager` constructor now takes `IDatabaseBackupService` instead of
concrete `ContestDbContext`.\
`ContestManagerTests` uses `Mock.Of<IDatabaseBackupService>()` or explicit
mocks.\
Export/Import tests at lines 134–172 verify delegation properly. No more `null!`
suppression for the database dependency.

---

### TE-003 — LocalStorage Backup/Restore Zero Coverage (HIGH)

**Status: PARTIALLY RESOLVED**

`BackupService` exists as `IBackupService` implementation wrapping
`ILocalStorageService` + `IDatabaseBackupService`.\
`BackupServiceTests` (5 tests) cover `SaveBackupAsync` and
`TryRestoreBackupAsync` paths.

**Remaining uncovered:**

- `Setup.razor.cs:49` (BackupDatabase method) — untested
- `Setup.razor.cs:55` (RestoreDatabase method) — untested
- `Judging.razor.cs:76-82` (backup/restore in judging page) — untested
- `Program.cs:39-54` (app-start restore with try/catch) — untested

The BackupService _unit_ is now covered; the Blazor web orchestration layer is
not. Partial resolution.

---

### TE-004 — BradleyTerry Convergence Untested (HIGH)

**Status: RESOLVED**

New test `ResolveGlobalStrengths_LinearOrder_ConvergesWithCorrectOrder` (20
entries) exercises iteration loop in depth, reaching the rank-stability
early-exit path (line 90–115). Additional edge case tests for empty input and
single entry cover guard clauses.

---

### TE-005 — CalculateScoresFromStrengths Untested (HIGH)

**Status: RESOLVED**

5 new tests across all three strategies. Core normalization logic, same-strength
guard clause, and single-entry path all exercised for `LinearSpacingScoring`.
Percentile and DefinedInterval paths tested but with weaker assertions and
missing edge cases (see §1.4).

---

### TE-006 — Repository UpdateAsync/GetAllAsync (MEDIUM)

**Status: NOT RESOLVED**

`InfrastructureTests.cs:115-116` calls `entryRepo.UpdateAsync(entryA)` as
_setup_ in the cascade-delete test. It is not the subject under test. No test
verifies:

- UpdateAsync correctly persists new scores
- UpdateAsync is no-op for non-existent entity
- GetAllAsync returns all entries (never called in tests)
- GetAllAsync on empty table

---

### TE-007 — Error Message Not Asserted (MEDIUM)

**Status: NOT RESOLVED**

`ValidationServiceTests.cs:175-176` still only asserts `IsValid` and
`ComponentCount`. `result.ErrorMessage` is never asserted in any
`ValidatePartitionedGraph` test.

---

### TE-008 — LessThan Operator Untested (MEDIUM)

**Status: NOT RESOLVED**

All validation tests (`ValidationServiceTests.cs`) use only
`Operator.GreaterThan` and `Operator.EqualTo`. The LessThan branch at
`GraphValidationService.cs:86-89` remains unexercised.

---

### TE-009 — Non-Existent ID Repository Edge Cases (MEDIUM)

**Status: NOT RESOLVED**

No tests for:

- `GetByIdAsync` returning `null` for non-existent ID
- `UpdateAsync` with non-existent ID (no-op path)
- `DeleteAsync` with non-existent ID (no-op path)
- `GetAllAsync` on empty table

---

### TE-010 — null! Masking ContestDbContext (MEDIUM)

**Status: RESOLVED**

`ContestManager` no longer depends on concrete `ContestDbContext`. Constructor
takes `IDatabaseBackupService` interface. All test instantiations use proper
mocks.

---

### TE-011 — E2E Shallow Smoke Tests (LOW)

**Status: NOT RESOLVED**

`AppE2ETests.cs` unchanged. Still 2 tests: homepage renders, navigation works.
No functional workflow validation.

---

### TE-012 — Fragile String-Contains Assertions (LOW)

**Status: NOT RESOLVED**

`TrimmingSafetyTests.cs:33-34` still has `Assert.Contains("E1", json)` and
`Assert.Contains("85.5", json)`.

---

### TE-013 — PartitionService Constructor Validation (LOW)

**Status: NOT RESOLVED**

No tests for `kPartitions <= 0` or invalid `overlapRate`.

---

### TE-014 — Duplicate Validation Code (LOW)

**Status: NOT RESOLVED**

`GraphValidationService.cs` still has duplicated union-find + adjacency logic
across three methods. No refactoring performed.

---

### TE-015 — Scoring Empty-Tiers/Single-Entry Edge Cases (LOW)

**Status: PARTIALLY RESOLVED**

`CalculateScoresFromStrengths` now has edge case tests for LinearSpacing.
However, `CalculateScores` (tier-based method) empty-tiers and single-entry
paths remain untested for all three strategies.

---

### TE-016 — Entry.SetScore Boundaries (LOW)

**Status: NOT RESOLVED**

`CoreTests.cs` still tests only mid-range score (5) for valid path. No tests for
`SetScore(category, 0)` or `SetScore(category, category.MaxScore)` (both valid
boundaries).

---

## Part 3: Coverage Gap Analysis

### 3.1 Critical Paths with Zero Coverage

| # | Gap                                                                               | Severity | Related |
| - | --------------------------------------------------------------------------------- | -------- | ------- |
| 1 | Repository `UpdateAsync` — as tested subject (only used as setup in cascade test) | Medium   | TE-006  |
| 2 | Repository `GetAllAsync` — never called in any test                               | Medium   | TE-006  |
| 3 | `LessThan` operator path in `GraphValidationService` (lines 86–89)                | Medium   | TE-008  |
| 4 | `PartitionService` constructor guard clauses (k≤0, invalid overlapRate)           | Low      | TE-013  |
| 5 | `Entry.SetScore` boundary values (0, MaxScore)                                    | Low      | TE-016  |
| 6 | Web-layer backup orchestration in `Setup.razor.cs` and `Program.cs`               | Medium   | TE-003  |
| 7 | `GraphValidationService` error message discrimination in tests                    | Low      | TE-007  |
| 8 | E2E functional judging workflow                                                   | Low      | TE-011  |

### 3.2 Partial Coverage (paths exist but with weak or missing assertions)

| #  | Gap                                                                                        | Severity |
| -- | ------------------------------------------------------------------------------------------ | -------- |
| 9  | `PercentileScoring.CalculateScoresFromStrengths` single-entry path (line 45–49)            | Low      |
| 10 | `PercentileScoring.CalculateScoresFromStrengths` empty-dict path (line 44)                 | Low      |
| 11 | `DefinedIntervalScoring.CalculateScoresFromStrengths` same-strength path                   | Low      |
| 12 | `BackupService` null-base64 path (line 44)                                                 | Low      |
| 13 | `BackupService` ImportAsync exception path (line 54)                                       | Low      |
| 14 | `BackupService` version mismatch: missing verify on `RemoveItemAsync("db_schema_version")` | Low      |

### 3.3 New Issues Found

| #  | ID        | Issue                                                                                                                                                                                                                                      | Severity |
| -- | --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------- |
| N1 | RA-TE-001 | `ModelValidationTests.LeaderboardItem_StoresEntryCorrectly` tests a plain property setter — zero business logic                                                                                                                            | Low      |
| N2 | RA-TE-002 | `LinearSpacing_CalculateScoresFromStrengths_VariedStrengths` uses `Assert.True(scores["A"] > 90.0)` — fuzzy assertion should use exact `Math.Round` output                                                                                 | Low      |
| N3 | RA-TE-003 | `BackupServiceTests.TryRestoreBackupAsync_VersionMismatch` omits `Verify RemoveItemAsync("db_schema_version")` — incomplete path verification                                                                                              | Low      |
| N4 | RA-TE-004 | `BackupServiceTests` mocks `ILogger<BackupService>` but never verifies any log call on the `LogWarning` or `LogError` paths                                                                                                                | Low      |
| N5 | RA-TE-005 | `ScoringStrategyTests` — 3 of 5 `CalculateScoresFromStrengths` tests use ordinal-only assertions where exact values are deterministic                                                                                                      | Low      |
| N6 | RA-TE-006 | `Percentile_CalculateScoresFromStrengths_RanksByStrengthPercentile` and `DefinedInterval_CalculateScoresFromStrengths_LinearScalingFallback` cover the same edge cases as the LinearSpacing tests — overlap without covering missing paths | Low      |

---

## Part 4: Finding Resolution Summary

| ID     | Severity | Status                 |
| ------ | -------- | ---------------------- |
| TE-001 | HIGH     | **RESOLVED**           |
| TE-002 | HIGH     | **RESOLVED**           |
| TE-003 | HIGH     | **PARTIALLY RESOLVED** |
| TE-004 | HIGH     | **RESOLVED**           |
| TE-005 | HIGH     | **RESOLVED**           |
| TE-006 | MEDIUM   | NOT RESOLVED           |
| TE-007 | MEDIUM   | NOT RESOLVED           |
| TE-008 | MEDIUM   | NOT RESOLVED           |
| TE-009 | MEDIUM   | NOT RESOLVED           |
| TE-010 | MEDIUM   | **RESOLVED**           |
| TE-011 | LOW      | NOT RESOLVED           |
| TE-012 | LOW      | NOT RESOLVED           |
| TE-013 | LOW      | NOT RESOLVED           |
| TE-014 | LOW      | NOT RESOLVED           |
| TE-015 | LOW      | **PARTIALLY RESOLVED** |
| TE-016 | LOW      | NOT RESOLVED           |

**Summary:** 4 resolved, 3 partially resolved, 9 not resolved (out of 16).

---

## Improvements Since Original Audit

1. **Dependency injection** — `IDatabaseBackupService` abstraction replaced
   concrete `ContestDbContext`, fixing TE-002 + TE-010
2. **Deterministic tests** — `new Random(42)` in PartitionServiceTests (TE-001)
3. **Backup/restore coverage** — BackupService unit tests (TE-003 partial)
4. **Algorithmic convergence** — 20-entry Bradley-Terry test exercising
   early-exit and deep iteration paths (TE-004)
5. **Score calculation** — CalculateScoresFromStrengths tested for all 3
   strategies (TE-005)
6. **Web layer** — First-ever web-layer tests via ModelValidationTests
7. **Test count** — 33 → 59 (+79%)

## Recommendations for Next Pass

1. Add `SqliteEntryRepository` tests for `UpdateAsync`, `GetAllAsync`, and
   non-existent-ID edge cases
2. Add `LessThan` operator test cases in `ValidationServiceTests`
3. Add `PartitionService` constructor validation tests (Theory with invalid
   k/overlap)
4. Add `SetScore` boundary tests (0, MaxScore)
5. Tighten fuzzy assertions in `ScoringStrategyTests` to exact `Math.Round`
   output values
6. Add error message assertions in `ValidatePartitionedGraph` tests
7. Remove `LeaderboardItem_StoresEntryCorrectly` or replace with meaningful test
8. Add E2E functional workflow test (category → entry → comparison →
   leaderboard)
