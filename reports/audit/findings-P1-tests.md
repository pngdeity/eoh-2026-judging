# P1-C Audit Report — Domain: tests

**Agent:** P1-C | **Pass:** 1 | **Date:** 2026-05-25

## Scope

Examined all test files under `tests/ContestJudging.Tests/` (8 `.cs` files) and
`tests/ContestJudging.E2ETests/` (1 `.cs` file), plus the test project
configuration, central package management, source project structure for coverage
comparison, and the audit schema (`FINDING-SCHEMA.json`).

## Metrics

| Metric                                                 | Value |
| ------------------------------------------------------ | ----- |
| Unit test files scanned                                | 8     |
| E2E test files scanned                                 | 1     |
| Source files (implementation, excl. interfaces/markup) | ~22   |
| Test-to-source ratio (approx.)                         | ~36%  |
| Total findings                                         | 13    |
| xUnit Facts                                            | 25    |
| xUnit Theories                                         | 0     |
| Skipped tests                                          | 0     |

## Findings

### TEST-001 — No parameterized tests (Theories) anywhere in the unit test suite

- **Severity:** high
- **Category:** testing
- **Files:** `tests/ContestJudging.Tests/CoreTests.cs:12`,
  `tests/ContestJudging.Tests/CoreTests.cs:36`,
  `tests/ContestJudging.Tests/ScoringStrategyTests.cs:12` (and 5 more)
- **Evidence:** Every test in the project uses `[Fact]` exclusively; `[Theory]`,
  `[InlineData]`, and `[MemberData]` are absent.
  `Category_Constructor_ThrowsWhenMaxScoreIsOneOrLess` tests boundary values 1
  and 0 inside a single `[Fact]` with two sequential `Assert.Throws` calls.
  `Entry_SetScore_InvalidScore_Throws` tests values 11 and -1 identically. If
  the first assertion throws/fails, the second never executes, masking defects.
- **Rule violated:** xUnit best practices: parameterized tests (`[Theory]`) over
  repeated `Assert.Throws` in a single `[Fact]`.
- **Remediation:** Convert `Category_Constructor_ThrowsWhenMaxScoreIsOneOrLess`
  to `[Theory]` with `[InlineData(1)]` and `[InlineData(0)]`. Apply same pattern
  to `Entry_SetScore_InvalidScore_Throws`. Consider `[Theory]` + `[MemberData]`
  for scoring strategy boundary tests (0 entries, 1 entry, maxScore 0, etc.).
- **Effort:** small

### TEST-002 — ContestJudging.Web project has zero tests

- **Severity:** high
- **Category:** testing
- **Files:** `src/ContestJudging.Web/Program.cs:1`,
  `src/ContestJudging.Web/Pages/Setup.razor.cs:1`,
  `src/ContestJudging.Web/Pages/Judging.razor.cs:1`,
  `src/ContestJudging.Web/Pages/Results.razor.cs:1`
- **Evidence:** The test project references `ContestJudging.Core`,
  `ContestJudging.Services`, and `ContestJudging.Infrastructure` but not
  `ContestJudging.Web`. No Blazor component tests, no DI registration tests, no
  page logic tests exist. All three Blazor pages (Setup, Judging, Results)
  contain significant logic (suggested pair algorithm, partition filtering,
  keyboard handling, score calculation) that is untested.
- **Rule violated:** Testing pyramid: missing unit tests for UI/presentation
  layer logic.
- **Remediation:** Add `bunit` or similar Blazor unit testing library. Write
  tests at minimum for the pure-logic methods: `FindSuggestedPair()`,
  `GetFilteredEntries()`, `GeneratePartitions()`, `CalculateResults()`, and the
  keyboard handler. Add `tests/ContestJudging.Web.Tests/` project.
- **Effort:** large

### TEST-003 — No test categories/traits to separate unit from integration tests

- **Severity:** medium
- **Category:** testing
- **Files:** `tests/ContestJudging.Tests/InfrastructureTests.cs:18`
- **Evidence:** `InfrastructureTests` uses SQLite in-memory (a real database)
  via `SqliteConnection("DataSource=:memory:")`, making them integration tests.
  No `[Trait("Category", "Integration")]` or equivalent attribute exists
  anywhere in the test suite. This prevents running unit tests independently
  from integration tests in CI (e.g.,
  `dotnet test --filter "Category!=Integration"`).
- **Rule violated:** xUnit best practices: test categorization for CI pipeline
  efficiency.
- **Remediation:** Add `[Trait("Category", "Unit")]` to unit test classes and
  `[Trait("Category", "Integration")]` to `InfrastructureTests`. Configure CI to
  run unit tests on every PR and integration tests on merge to main.
- **Effort:** trivial

### TEST-004 — ContestManager constructor takes concrete ContestDbContext; tests pass `null!`

- **Severity:** medium
- **Category:** testing
- **Files:** `src/ContestJudging.Services/Managers/ContestManager.cs:25`,
  `tests/ContestJudging.Tests/ContestManagerTests.cs:30`
- **Evidence:** `ContestManager` constructor accepts `ContestDbContext context`
  (a concrete EF Core type) alongside 6 mocked interfaces. All 3 tests pass
  `null!` for this parameter. This is risky because: (a) the `ExportDataAsync`
  and `ImportDataAsync` methods on `ContestManager` directly call `_context`
  with no tests exercising those paths; (b) injecting a concrete class violates
  the Dependency Inversion Principle and makes the class harder to unit-test
  properly.
- **Rule violated:** SOLID:DIP — depending on concrete implementation instead of
  abstraction.
- **Remediation:** Either (a) introduce an `IDatabaseExportImport` interface
  abstracting `ExportDatabaseAsync`/`ImportDatabaseAsync`, or (b) add a
  dedicated integration test for `ExportDataAsync`/`ImportDataAsync` that uses a
  real in-memory context. The null suppression in current tests masks the
  fragility.
- **Effort:** medium

### TEST-005 — ServiceCollectionExtensions has no test coverage

- **Severity:** medium
- **Category:** testing
- **Files:**
  `src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:21`
- **Evidence:** `AddContestJudgingServices` is the single method that wires all
  DI registrations for the application (8 service registrations). It is
  untested. A misconfiguration here (wrong lifetime, missing registration, wrong
  implementation type) would only be caught at runtime.
- **Rule violated:** Testing: critical infrastructure/pathway code should have
  coverage.
- **Remediation:** Add a test that builds the `IServiceProvider` from
  `AddContestJudgingServices`, resolves each registered service type, and
  verifies they're non-null and of the expected concrete type. Use in-memory
  SQLite to avoid filesystem dependency.
- **Effort:** small

### TEST-006 — Vague test class name "CoreTests"

- **Severity:** low
- **Category:** style
- **Files:** `tests/ContestJudging.Tests/CoreTests.cs:9`
- **Evidence:** `CoreTests` tests `Category` and `Entry` entities but the name
  doesn't indicate this. By contrast, `ValidationServiceTests` clearly maps to
  `GraphValidationService`, `PartitionServiceTests` maps to `PartitionService`,
  etc.
- **Rule violated:** Consistency/readability: test class names should map to the
  class under test.
- **Remediation:** Rename to `CategoryTests` and `EntryTests` (split into two
  files), or rename to `CoreEntityTests`.
- **Effort:** trivial

### TEST-007 — E2E tests use NUnit while unit tests use xUnit

- **Severity:** low
- **Category:** testing
- **Files:** `tests/ContestJudging.E2ETests/AppE2ETests.cs:3`,
  `tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj:16`
- **Evidence:** The E2E project uses NUnit (`NUnit.Framework`,
  `Microsoft.Playwright.NUnit`, `NUnit3TestAdapter`) while the unit test project
  uses xUnit. This creates a split testing framework footprint, requiring
  developers to know both assertion styles (`Assert.Equal` vs `Assert.That`),
  setup patterns (`[SetUp]` vs constructor), and test discovery conventions.
- **Rule violated:** Consistency: single framework reduces cognitive load.
- **Remediation:** Either migrate E2E tests to xUnit (using
  `Microsoft.Playwright` without the NUnit adapter), or document the rationale
  for the split. If staying with NUnit, ensure all E2E authors are aware of the
  framework difference.
- **Effort:** small

### TEST-008 — E2E project package versions missing from central package management

- **Severity:** medium
- **Category:** testing
- **Files:** `tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj:11`,
  `Directory.Packages.props:1`
- **Evidence:** `Directory.Packages.props` sets
  `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` and
  defines versions for xUnit, Moq, and Coverlet, but does **not** define
  versions for NUnit, NUnit.Analyzers, NUnit3TestAdapter, or
  Microsoft.Playwright.NUnit — all referenced by the E2E project. This will
  cause `dotnet restore` to fail with `NU1008` (version not defined centrally)
  unless the E2E project overrides central management.
- **Rule violated:** Build integrity: central package management must cover all
  projects.
- **Remediation:** Either add version entries to `Directory.Packages.props` for
  all E2E dependencies, or add
  `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>` to
  the E2E `.csproj` if it's intentionally managed separately. Verify with
  `dotnet restore`.
- **Effort:** trivial

### TEST-009 — Repository edge cases untested

- **Severity:** low
- **Category:** testing
- **Files:** `tests/ContestJudging.Tests/InfrastructureTests.cs:35`
- **Evidence:** Repository tests cover happy paths (add/get/delete with
  cascades). Missing edge cases: `GetByIdAsync` with non-existent ID, `AddAsync`
  with duplicate ID, `AddAsync` with null entity, `DeleteAsync` with
  non-existent ID, `UpdateAsync` with non-existent entity, `GetAllAsync` on
  empty table.
- **Rule violated:** Testing: edge case and error path coverage.
- **Remediation:** Add tests for each edge case listed above using `[Fact]` or
  `[Theory]` with `[InlineData]`.
- **Effort:** small

### TEST-010 — ContestManager ExportDataAsync/ImportDataAsync untested

- **Severity:** low
- **Category:** testing
- **Files:** `src/ContestJudging.Services/Managers/ContestManager.cs:119`
- **Evidence:** `ExportDataAsync` and `ImportDataAsync` directly call
  `_context.ExportDatabaseAsync()`/`_context.ImportDatabaseAsync()` and are not
  covered by any unit or integration test. These methods are used by the Web
  pages' `BackupDatabase()` methods (Blazor LocalStorage) and by `Program.cs` at
  startup.
- **Rule violated:** Testing: critical data path (backup/restore) has no
  coverage.
- **Remediation:** Add integration tests using an in-memory context that verify
  round-trip (export, wipe, import, verify data matches).
- **Effort:** small

### TEST-011 — Dead placeholder code: Class1.cs

- **Severity:** informational
- **Category:** testing
- **Files:** `src/ContestJudging.Infrastructure/Class1.cs:3`
- **Evidence:** `Class1` is an empty class in the Infrastructure project. It has
  no tests (none are needed for an empty class), but its presence is dead code
  that pollutes the namespace.
- **Rule violated:** Clean code: remove unused code.
- **Remediation:** Delete `Class1.cs` if it serves no purpose.
- **Effort:** trivial

### TEST-012 — TrimmingSafetyTests contradicts its own purpose

- **Severity:** informational
- **Category:** testing
- **Files:** `tests/ContestJudging.Tests/TrimmingSafetyTests.cs:19`
- **Evidence:** `JsonSerialization_ShouldWork_WithDomainEntities` is decorated
  with
  `[RequiresUnreferencedCode("Testing reflection-based JSON serialization.")]`.
  The test is meant to validate that IL trimming doesn't break serialization,
  but it suppresses the very warning that trimming analysis would generate. In a
  trimmed build, this test's presence is contradictory — it can't verify
  trimming safety if it suppresses the warning.
- **Rule violated:** Testing: trimming safety tests should validate behavior
  under trimming without suppressing trimming warnings.
- **Remediation:** Either (a) use the source-generated JSON serializer instead
  of reflection-based `JsonSerializer.Serialize`/`Deserialize`, or (b) remove
  the `[RequiresUnreferencedCode]` attribute and run this test explicitly in a
  trimmed publish to detect actual breaks. Consider `FluentAssertions` or a
  dedicated trimmed-test project.
- **Effort:** small

### TEST-013 — No cancellation token support in async tests

- **Severity:** informational
- **Category:** testing
- **Files:** `tests/ContestJudging.Tests/InfrastructureTests.cs:35`,
  `tests/ContestJudging.Tests/ContestManagerTests.cs:21`
- **Evidence:** All async test methods (5 in InfrastructureTests, 3 in
  ContestManagerTests) use `async Task` without `CancellationToken` parameters.
  The underlying async methods (`AddAsync`, `GetByIdAsync`, etc.) likely don't
  accept cancellation tokens either, but the async test pattern doesn't verify
  timeout or abort behavior.
- **Rule violated:** Testing: async tests should validate cancellation behavior
  where applicable.
- **Remediation:** If the underlying async operations support cancellation, add
  `[Fact]` tests that pass `CancellationToken` with
  `new CancellationTokenSource(TimeSpan.FromMilliseconds(100)).Token`. If not,
  this is informational — consider adding cancellation token support to the
  repository interfaces.
- **Effort:** small

## Coverage Gap Summary

| Source Project    | Files | Has Tests?      | Notes                                                        |
| ----------------- | ----- | --------------- | ------------------------------------------------------------ |
| Core (entities)   | 4     | Partial         | Relation entity has no dedicated tests                       |
| Core (interfaces) | 4     | N/A             | Interfaces don't need direct tests                           |
| Services (impl)   | 9     | Yes (6 covered) | Extensions, ValidationResult, I* interfaces uncovered        |
| Infrastructure    | 3     | 2 of 3          | Class1.cs is dead code; ContestDbContext not directly tested |
| Web               | 7+    | No              | Entirely untested — critical gap                             |

## Test Quality Assessment

**Strengths:**

- Consistent Arrange/Act/Assert structure
- Good assertion specificity (`Assert.Single`, `Assert.Empty`,
  `Assert.NotEmpty`, `Assert.Contains`)
- Proper async patterns (`using var`, `await`)
- Moq used appropriately for behavior verification (not implementation detail)
- ValidationService tests are thorough (cycles, ties, disconnection, tiers)
- Infrastructure tests are well-structured with proper DB lifecycle

**Weaknesses:**

- Zero parameterized tests — candidates missed across all test files
- No test categorization (unit vs integration)
- ContestManager tests pass `null!` for concrete dependency
- Whole Web project untested
- No service DI registration tests
- No edge case coverage in repository tests
- Framework split (xUnit vs NUnit) between test projects

## E2E Tests

The E2E test directory contains one test file (`AppE2ETests.cs`) with 2
Playwright tests:

- `HomepageHasExpectedTitleAndContent` — verifies page title and heading text
- `NavigationToSetupWorks` — clicks "Get Started" and verifies the Setup page
  heading

Both tests assume the app is running on `http://localhost:5000`. Coverage is
minimal (homepage + one navigation). No tests for judging workflow, results
calculation, or edge cases (empty database, errors). The E2E project may not
build due to missing central package versions.
