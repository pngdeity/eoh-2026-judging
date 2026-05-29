# Re-Audit Report: Tests Domain (P1-C)

**Agent**: PA1-C | **Date**: 2026-05-26 | **Branch**: `fix/audit-remediate`\
**Based on**: `reports/audit/findings-P1-tests.json` (13 findings)

---

## Executive Summary

| Metric                      | Original | Remediated   | Delta      |
| --------------------------- | -------- | ------------ | ---------- |
| Test count                  | 33       | 59           | +26 (+79%) |
| Test files                  | 9        | 11           | +2         |
| Test projects               | 2        | 3            | +1         |
| Theories (parameterized)    | 0        | 3            | +3         |
| Findings resolved           | —        | 4            | —          |
| Findings partially resolved | —        | 2            | —          |
| Findings unresolved         | —        | 7            | —          |
| New findings                | —        | 5            | —          |
| **Overall health**          | —        | **IMPROVED** | —          |

---

## 1. Full Discovery

### 1.1 Test Inventory

| File                                               | Tests  | Facts  | Theories | NUnit |
| -------------------------------------------------- | ------ | ------ | -------- | ----- |
| `ContestJudging.Tests/CoreTests.cs`                | 5      | 3      | 2        | 0     |
| `ContestJudging.Tests/ScoringStrategyTests.cs`     | 9      | 9      | 0        | 0     |
| `ContestJudging.Tests/ValidationServiceTests.cs`   | 10     | 10     | 0        | 0     |
| `ContestJudging.Tests/PartitionServiceTests.cs`    | 2      | 2      | 0        | 0     |
| `ContestJudging.Tests/InfrastructureTests.cs`      | 5      | 5      | 0        | 0     |
| `ContestJudging.Tests/ContestManagerTests.cs`      | 7      | 7      | 0        | 0     |
| `ContestJudging.Tests/BackupServiceTests.cs`       | 5      | 5      | 0        | 0     |
| `ContestJudging.Tests/TrimmingSafetyTests.cs`      | 2      | 2      | 0        | 0     |
| `ContestJudging.Tests/ResolutionServiceTests.cs`   | 5      | 5      | 0        | 0     |
| `ContestJudging.Web.Tests/ModelValidationTests.cs` | 6      | 5      | 1        | 0     |
| `ContestJudging.E2ETests/AppE2ETests.cs`           | 2      | 0      | 0        | 2     |
| **TOTAL**                                          | **59** | **53** | **3**    | **2** |

### 1.2 Test Naming Conventions

All tests follow `MethodOrScenario_ExpectedBehavior` pattern (xUnit convention).
Naming is consistent and descriptive. The E2E project uses NUnit `[Test]` with
descriptive names.

### 1.3 Arrange/Act/Assert Structure

- **Strong**: `ValidationServiceTests.cs` (lines 153+) explicitly labels AAA
  sections with `// Arrange`, `// Act`, `// Assert` comments.
- **Acceptable**: Most other test files follow AAA implicitly.
  `ContestManagerTests.cs`, `BackupServiceTests.cs` use mock setup → call SUT →
  assert + verify pattern.
- **Missing**: E2E tests and `TrimmingSafetyTests.cs` skip explicit AAA
  separation.

### 1.4 Assertion Strength

- **Good**: `ValidationServiceTests` uses `Assert.True`, `Assert.False`,
  `Assert.Equal` with specific expected values.
- **Good**: `BackupServiceTests` uses `Mock.Verify(... Times.Once)` for
  side-effect verification plus value assertions.
- **Good**: `ContestManagerTests` verifies mock invocations and entry state
  mutations.
- **Weak**: `ResolutionServiceTests.cs:61` —
  `Assert.Equal(strengths["A"], strengths["B"], 5)` uses ±5 precision which
  could mask significant deviations.
- **Weak**: Export/Import tests only verify mock delegation, not actual data
  integrity.

### 1.5 Mock Usage Quality

- `ContestManagerTests`: Uses Moq correctly. All 7 dependencies mocked. Pattern:
  new Mock<T>() → Setup → .Object.
- `BackupServiceTests`: Well-structured. Mocks initialized in constructor,
  reused across tests. Good isolation.
- `InfrastructureTests`: Uses real Sqlite in-memory database
  (integration-style). No mocking — appropriate for repository tests.

### 1.6 Test Isolation

- No shared static state detected.
- `BackupServiceTests` creates fresh mocks per test via constructor.
- `ContestManagerTests` creates fresh mocks inline per test (duplicated
  boilerplate).
- `InfrastructureTests` uses `using var context` per test — good isolation.

### 1.7 Integration vs Unit Split

| Classification    | Count | Tests                                                                                                                                                                 |
| ----------------- | ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Unit (pure logic) | 43    | CoreTests, ScoringStrategyTests, ValidationServiceTests, PartitionServiceTests, ResolutionServiceTests, ContestManagerTests, BackupServiceTests, ModelValidationTests |
| Integration (DB)  | 5     | InfrastructureTests (in-memory SQLite)                                                                                                                                |
| E2E               | 2     | AppE2ETests (Browser via Playwright)                                                                                                                                  |
| Trimming          | 2     | TrimmingSafetyTests                                                                                                                                                   |

**No `[Trait]` attributes exist** to distinguish these categories. CI cannot
filter.

### 1.8 Theory vs Fact Usage

| Pattern    | Count | Location                                |
| ---------- | ----- | --------------------------------------- |
| `[Theory]` | 3     | CoreTests (2), ModelValidationTests (1) |
| `[Fact]`   | 53    | All other unit tests                    |
| `[Test]`   | 2     | E2E                                     |

**Remaining candidates for Theory conversion**:

- `ScoringStrategyTests` — `CalculateScoresFromStrengths` tests on different
  strategies could be a single Theory with strategy type + expected ordering as
  InlineData.
- `ValidationServiceTests` — `IsTotalOrder`, `IsValidOrder` success/failure
  cases could be Theory-driven.

### 1.9 Skipped Tests

Zero skipped tests found. No `[Fact(Skip=...)]`, `[Theory(Skip=...)]`, or
`Ignore` attributes.

### 1.10 Async Test Patterns

- All async tests use `async Task` with bare `await` (no CancellationToken).
- No `ConfigureAwait(false)` anywhere — acceptable in test projects per xUnit
  guidance.
- Proper `using var context` pattern in InfrastructureTests for deterministic
  disposal.

### 1.11 Coverage Ratio

| Metric                    | Value |
| ------------------------- | ----- |
| Source files (.cs)        | 31    |
| Test files (.cs)          | 11    |
| Test-to-source file ratio | 0.35  |
| Source lines              | 1,907 |
| Test lines                | 1,175 |
| Test-to-source line ratio | 0.62  |

---

## 2. Original Findings Verification

### Resolved (4 of 13)

#### TEST-001 — No Theories → FIXED

`CoreTests.cs:11-17` now uses `[Theory]` with three `[InlineData]` values (1, 0,
-1) for `Category_Constructor_ThrowsWhenMaxScoreIsOneOrLess`. Lines 37-45 use
`[Theory]` with `[InlineData(11)]` and `[InlineData(-1)]` for
`Entry_SetScore_InvalidScore_Throws`. Each assertion executes independently.

#### TEST-004 — ContestManager null! → FIXED

`ContestManager.cs:22-31` now takes `IDatabaseBackupService backupService` as
the 7th constructor parameter instead of `ContestDbContext`. All tests construct
`Mock<IDatabaseBackupService>()` properly. The `null!` suppression eliminated.

#### TEST-008 — E2E packages missing from CPM → FIXED

`Directory.Packages.props` now contains:

- `NUnit` 4.3.2
- `NUnit.Analyzers` 4.6.0
- `NUnit3TestAdapter` 5.0.0
- `Microsoft.Playwright.NUnit` 1.52.0

#### TEST-011 — Dead Class1.cs → FIXED

File no longer exists. Deleted from repository.

### Partially Resolved (2 of 13)

#### TEST-002 — Web untested → PARTIALLY RESOLVED

`ContestJudging.Web.Tests` project created with bunit dependency and
`ModelValidationTests.cs` (6 tests). However:

- Tests only validate data annotations on `Setup.CategoryModel`,
  `Setup.EntryModel`, and `Results.LeaderboardItem`.
- Zero Blazor component rendering tests despite `bunit` PackageReference.
- Core page logic (`FindSuggestedPair()`, `GetFilteredEntries()`,
  `HandleKeyDown()`, `CalculateResults()`, `GeneratePartitions()`) remains
  completely untested.

#### TEST-010 — Export/Import untested → PARTIALLY RESOLVED

`ContestManagerTests.cs:134-172` adds `ExportDataAsync_DelegatesToBackupService`
and `ImportDataAsync_DelegatesToBackupService`. However, both tests only verify
`mockBackup.Verify(... Times.Once)` — delegation verification, not data
integrity. No round-trip integration test (seed → export → wipe → import →
verify data matches).

### Unresolved (7 of 13)

| ID       | Title                                | Severity      | Details                                              |
| -------- | ------------------------------------ | ------------- | ---------------------------------------------------- |
| TEST-003 | No test traits/categories            | medium        | Zero `[Trait]` attributes. CI cannot filter.         |
| TEST-005 | ServiceCollectionExtensions untested | medium        | No DI registration verification test.                |
| TEST-006 | Vague "CoreTests" name               | low           | Still named CoreTests, mixes Category + Entry tests. |
| TEST-007 | E2E uses NUnit, rest xUnit           | low           | Framework inconsistency persists.                    |
| TEST-009 | Repository edge cases                | low           | Still only happy-path repository tests.              |
| TEST-012 | Trimming suppresses warnings         | informational | `[RequiresUnreferencedCode]` still on test.          |
| TEST-013 | No cancellation token support        | informational | No CancellationToken anywhere.                       |

---

## 3. New Findings

### RA-TEST-001: bunit dependency unused (medium)

`ContestJudging.Web.Tests.csproj` references
`<PackageReference Include="bunit"/>` but `ModelValidationTests.cs` contains
zero Blazor component rendering tests. The bunit package implies component
testing capability but it's only used for plain model validation (which doesn't
require bunit). Creates false impression of component coverage.

- **Remediation**: Add bunit rendering tests for Setup, Judging, Results pages,
  or remove bunit dependency if not intended.

### RA-TEST-002: Export/Import tests are delegation-only (medium)

`ExportDataAsync_DelegatesToBackupService` and
`ImportDataAsync_DelegatesToBackupService` only verify the mock was called. They
do not test:

- That exported data can be successfully imported
- That imported data correctly restores entities
- That schema changes are handled
- **Remediation**: Add integration test with in-memory DbContext: seed data →
  export → wipe context → import → verify entities match.

### RA-TEST-003: BackupService empty-base64 branch untested (medium)

`BackupService.TryRestoreBackupAsync:44` checks
`if (string.IsNullOrEmpty(base64)) return null;` but no test covers this branch
(backup key exists in localStorage but value is empty string or null).

- **Remediation**: Add test for `GetItemAsync<string>("db_backup")` returning
  `""` or mock returning null.

### RA-TEST-004: ContestManager null-category branch untested (low)

`ContestManager.CalculateGlobalScoresAsync:83-86` returns early if
`category == null`, but no test mocks `GetByIdAsync` to return null.

- **Remediation**: Add test with
  `mockCatRepo.Setup(r => r.GetByIdAsync("nonexistent")).ReturnsAsync((Category?)null)`.

### RA-TEST-005: DatabaseBackupService concrete implementation untested (low)

`IDatabaseBackupService` is only ever mocked. The concrete
`DatabaseBackupService` (file-based SQLite backup in
`src/ContestJudging.Infrastructure/Persistence/`) has zero direct unit or
integration tests.

- **Remediation**: Add integration test using temp SQLite file: export → verify
  file → import into fresh context → verify.

---

## 4. Coverage Matrix

| Source Project                    | Tested Classes                                                                                                                                                          | Untested                                                                |
| --------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| **ContestJudging.Core**           | Category, Entry, Operator                                                                                                                                               | (interfaces — tested via implementations)                               |
| **ContestJudging.Infrastructure** | ContestDbContext, SqliteCategoryRepository, SqliteEntryRepository, SqliteRelationRepository                                                                             | DatabaseBackupService                                                   |
| **ContestJudging.Services**       | ContestManager, BackupService, GraphValidationService, PartitionService, BradleyTerryResolutionService, LinearSpacingScoring, PercentileScoring, DefinedIntervalScoring | ServiceCollectionExtensions                                             |
| **ContestJudging.Web**            | Setup.CategoryModel, Setup.EntryModel, Results.LeaderboardItem                                                                                                          | Setup (component), Judging (component), Results (component), Program.cs |

---

## 5. Comparison vs Original

| Aspect                 | Original (Pass 1) | Remediated (Pass 2)   |
| ---------------------- | ----------------- | --------------------- |
| Test count             | 33                | 59 (+79%)             |
| Test files             | 9                 | 11                    |
| Theories               | 0                 | 3                     |
| Web tests              | 0                 | 6 (model only)        |
| Backup tests           | 0                 | 5 (BackupService)     |
| Export/Import tests    | 0                 | 2 (delegation only)   |
| IDatabaseBackupService | null! pattern     | proper mock injection |
| Central Pkg Mgmt       | E2E missing       | all covered           |
| Class1.cs              | present           | deleted               |
| Traits                 | 0                 | 0 (unchanged)         |
| NUnit/xUnit split      | present           | present (unchanged)   |
| Repository edge cases  | 0                 | 0 (unchanged)         |
| DI coverage            | 0                 | 0 (unchanged)         |

---

## 6. Verdict

**Test health: IMPROVED**

The remediated codebase is measurably better: test count up 79%, critical
`null!` pattern fixed, new `BackupServiceTests` are comprehensive, and
`CoreTests` now uses proper parameterized Theories. However, the majority of
original findings (7 of 13) remain unresolved, and 5 new issues were discovered.
The most concerning gaps are (a) zero Blazor component tests despite having
bunit wired up, and (b) no round-trip integration test for the backup/restore
data path.

### Top 3 Action Items (by impact)

1. Add bunit component rendering tests for `Judging.razor` (highest regression
   risk)
2. Add round-trip integration test for export → wipe → import → verify
3. Add `[Trait("Category", "Unit|Integration")]` to all test classes for CI
   filtering
