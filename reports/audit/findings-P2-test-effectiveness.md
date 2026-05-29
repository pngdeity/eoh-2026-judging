# P2-E Test Effectiveness Audit Report

**Agent:** P2-E | **Pass:** 2 | **Domain:** test-effectiveness | **Date:**
2026-05-25

## Summary

| Metric                        | Value                            |
| ----------------------------- | -------------------------------- |
| Test files analyzed           | 9 (.cs) + 2 (.csproj)            |
| Implementation files analyzed | 20                               |
| Tests evaluated               | 28 factual + 2 theory-equivalent |
| Findings                      | 16                               |
| Test Effectiveness Score      | **4/10**                         |

The test suite shows basic structural coverage but suffers from weak assertions,
non-deterministic tests, untestable code due to poor DI, zero coverage of the
entire Web layer, and critical algorithmic edge cases left unexercised.

---

## TE-001 — PartitionService tests are non-deterministic (HIGH)

**Severity:** high | **Category:** test-isolation | **Effort:** small

**Files:**

- `src/ContestJudging.Services/Partitioning/PartitionService.cs:9`
- `tests/ContestJudging.Tests/PartitionServiceTests.cs:22`
- `tests/ContestJudging.Tests/PartitionServiceTests.cs:31`

`PartitionService` uses an instance-level
`private readonly Random _random = new()` whose seed depends on system clock.
Tests assert exact bridge-node counts (`Assert.Equal(10, common.Count)`) and
partition sizes, which can fail non-deterministically on different machines or
under load.

**Evidence:**

```csharp
// PartitionService.cs:9
private readonly Random _random = new();

// PartitionServiceTests.cs:31 — asserts exact count from Random shuffle
Assert.Equal(10, common.Count);
```

At 100 entries, k=2, overlap=0.10: `Math.Round(100 * 0.10) = 10` bridge nodes
are selected by `shuffled.Take(bCount)`. While statistically almost guaranteed,
it's non-deterministic — no `Random(seed)` or `[CallerMemberName]`-based
seeding.

**Remediation:** Inject `Random` (or an `IRandomProvider`) into PartitionService
constructor or make it a parameter to `GeneratePartitions`. Tests should use
`new Random(42)` for deterministic runs.

**Related:** TEST-001 (Pass 1)

---

## TE-002 — ContestManager ExportDataAsync/ImportDataAsync are structurally untestable (HIGH)

**Severity:** high | **Category:** mock-quality | **Effort:** medium

**Files:**

- `src/ContestJudging.Services/Managers/ContestManager.cs:23`
- `src/ContestJudging.Services/Managers/ContestManager.cs:119`
- `src/ContestJudging.Services/Managers/ContestManager.cs:124`
- `tests/ContestJudging.Tests/ContestManagerTests.cs:30`
- `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:66`
- `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:76`

ContestManager takes `ContestDbContext` as a concrete dependency (not an
interface). Tests pass `null!` for this parameter, making any call to
`ExportDataAsync()`/`ImportDataAsync()` throw NullReferenceException. Further,
both methods write/read the hardcoded path `contest.db` — impossible to mock or
intercept with the concrete type.

**Evidence:**

```csharp
// ContestManager.cs:23
private readonly ContestDbContext _context;

// ContestManager.cs:119
public async Task<byte[]> ExportDataAsync() => await _context.ExportDatabaseAsync();

// ContestDbContext.cs:66
public async Task<byte[]> ExportDatabaseAsync()
{
    var path = "contest.db";  // hardcoded!
    if (File.Exists(path)) return await File.ReadAllBytesAsync(path);
    return Array.Empty<byte>();
}

// ContestManagerTests.cs:30 — test passes null
var manager = new ContestManager(... , null!);
```

The `null!` suppression masks that the test constructor can never exercise
Export/Import. This is a direct consequence of CQ-003 (swallowed exception in
restore) and TEST-004 (concrete DbContext injection).

**Remediation:** Extract `IDatabaseExportImport` with
`ExportAsync`/`ImportAsync`, implement in ContestDbContext, and inject into
ContestManager. Integration tests can then use an in-memory implementation or
mock.

**Related:** CQ-003, TEST-004, TEST-010

---

## TE-003 — LocalStorage backup/restore has zero coverage across all layers (HIGH)

**Severity:** high | **Category:** coverage-gap | **Effort:** medium

**Files:**

- `src/ContestJudging.Web/Pages/Setup.razor.cs:49`
- `src/ContestJudging.Web/Pages/Setup.razor.cs:55`
- `src/ContestJudging.Web/Pages/Judging.razor.cs:76`
- `src/ContestJudging.Web/Pages/Judging.razor.cs:82`
- `src/ContestJudging.Web/Program.cs:39`
- `src/ContestJudging.Web/Program.cs:46`

The backup/restore pipeline is the only persistence mechanism in the Blazor WASM
app (SQLite is in-memory and lost on page refresh). Three identical
`BackupDatabase()` methods exist in Setup and Judging pages (copy-paste). The
restore path in Program.cs swallows exceptions (CQ-003). None of this is tested
— no unit tests, no integration tests, no E2E tests exercising the
backup/restore round-trip.

**Evidence:**

```csharp
// Setup.razor.cs:49-57 and Judging.razor.cs:76-84 — identical code
private async Task BackupDatabase()
{
    var data = await ContestManager.ExportDataAsync();
    if (data.Length > 0)
    {
        await LocalStorage.SetItemAsStringAsync("db_backup", Convert.ToBase64String(data));
    }
}

// Program.cs:39-54 — restore path with swallowed exception
catch (Exception ex)
{
    Console.WriteLine($"Failed to restore database: {ex.Message}"); // CQ-003
}
```

If the restore path silently fails, users lose all contest data. No test
validates this scenario.

**Remediation:** Extract backup/restore to a dedicated `IBackupService` (DRY +
testable). Add integration test that round-trips: write data, backup, wipe,
restore, verify data match. Add bUnit test for Blazor pages exercising the full
pipeline.

**Related:** CQ-003, CQ-002, TE-002

---

## TE-004 — BradleyTerryResolveService convergence path completely untested (HIGH)

**Severity:** high | **Category:** coverage-gap | **Effort:** medium

**Files:**

- `src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:56`
- `src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:90`
- `src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:108`
- `src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:118`
- `tests/ContestJudging.Tests/ResolutionServiceTests.cs:13`
- `tests/ContestJudging.Tests/ResolutionServiceTests.cs:41`

`BradleyTerryResolutionService.ResolveGlobalStrengths` contains a sophisticated
MLE iterative scaling loop (lines 56-119) with two exit conditions: convergence
(`maxDiff < 1e-6`) and rank stability early-exit (`iter > 50 && iter % 10 == 0`
with stable ranks and `maxDiff < 1e-3`). Neither exit path is exercised by
existing tests — both tests use ≤3 entries and trivial relations that converge
in 1 iteration. The loop never reaches `iter > 50` and never triggers the
rank-stability check.

**Evidence:**

```csharp
// BradleyTerryResolutionService.cs:90-111 — untested early-exit logic
if (iter > 50 && iter % 10 == 0)
{
    // ... rank stability check
    if (stable && maxDiff < 1e-3)
    {
        gamma = nextGamma;
        break;
    }
    // ...
}
// Line 118 — also untested: convergence threshold
if (maxDiff < ConvergenceThreshold) break;
```

Additionally, there is no test for: empty `allEntryIds`, single entry,
MaxIterations exhaustion (1000 iterations), or the `denominator == 0` branch
(line 77-80).

**Remediation:** Add tests for: 10+ entries with partial rankings requiring 50+
iterations (stress the convergence path), empty input returns empty dictionary,
single entry returns non-trivial strength. Test that `maxIterations` exits
without infinite loop.

**Related:** CQ-006, CQ-008

---

## TE-005 — CalculateScoresFromStrengths untested across all three scoring strategies (HIGH)

**Severity:** high | **Category:** coverage-gap | **Effort:** small

**Files:**

- `src/ContestJudging.Services/Scoring/LinearSpacingScoring.cs:38`
- `src/ContestJudging.Services/Scoring/PercentileScoring.cs:40`
- `src/ContestJudging.Services/Scoring/DefinedIntervalScoring.cs:38`
- `tests/ContestJudging.Tests/ScoringStrategyTests.cs:13`

The `IScoringStrategy` interface defines two methods: `CalculateScores`
(tier-based) and `CalculateScoresFromStrengths` (strength-based). All
ScoringStrategyTests test only `CalculateScores`. `CalculateScoresFromStrengths`
is called by `ContestManager.CalculateGlobalScoresAsync` (line 104) and
exercises entirely different code paths — normalization, min/max range,
division-by-zero guard. All three implementations have a `range < 1e-9` guard
(CQ-008) that is tested through neither `CalculateScores` nor
`CalculateScoresFromStrengths`.

**Evidence:**

```csharp
// LinearSpacingScoring.cs:52-63 — untested normalization
double range = maxStrength - minStrength;
if (range < 1e-9) { normalized = 1.0; }
else { normalized = (kvp.Value - minStrength) / range; }
```

**Remediation:** Add tests for each strategy's `CalculateScoresFromStrengths`
with varied strength values, identical strengths (range < 1e-9), and
single-entry inputs.

**Related:** CQ-008, TE-004

---

## TE-006 — Repository UpdateAsync and GetAllAsync methods untested (MEDIUM)

**Severity:** medium | **Category:** coverage-gap | **Effort:** small

**Files:**

- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:41`
- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:28`
- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:101`
- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:143`
- `tests/ContestJudging.Tests/InfrastructureTests.cs:35`

Only AddAsync and DeleteAsync are exercised in InfrastructureTests.
`SqliteCategoryRepository.UpdateAsync`, `SqliteCategoryRepository.GetAllAsync`,
`SqliteEntryRepository.GetAllAsync`, and `SqliteEntryRepository.UpdateAsync`
have zero coverage. These are called by Setup.razor.cs (UpdateAsync via scoring
pipeline) and Results.razor.cs (GetAllAsync).

**Evidence:**

```csharp
// SqliteRepositories.cs:41-49 — UpdateAsync untested
public async Task UpdateAsync(Category category)
{
    var entity = await _context.Categories.FindAsync(category.Id);
    if (entity != null) { entity.MaxScore = category.MaxScore; await _context.SaveChangesAsync(); }
}

// SqliteRepositories.cs:143-165 — Entry UpdateAsync untested (removes/rebuilds scores)
public async Task UpdateAsync(Entry entry)
{
    // ... removes all scores, re-adds them
    _context.EntryScores.RemoveRange(entity.Scores);
    foreach (var score in entry.Scores) { entity.Scores.Add(...); }
}
```

Particularly dangerous: `SqliteEntryRepository.UpdateAsync` removes all existing
scores and re-adds — a destructive operation with no test.

**Remediation:** Add tests for all UpdateAsync and GetAllAsync methods across
all three repositories. Include edge: update non-existent ID, empty table
GetAllAsync, update with zero scores.

---

## TE-007 — ValidatePartitionedGraph assertions fail to verify error messages (MEDIUM)

**Severity:** medium | **Category:** assertion-strength | **Effort:** small

**Files:**

- `tests/ContestJudging.Tests/ValidationServiceTests.cs:175`
- `tests/ContestJudging.Tests/ValidationServiceTests.cs:201`
- `tests/ContestJudging.Tests/ValidationServiceTests.cs:228`
- `src/ContestJudging.Services/Validation/GraphValidationService.cs:312`

Three tests for `ValidatePartitionedGraph` only check `result.IsValid` and
`result.ComponentCount`. The `ErrorMessage` string and component-count error
message (line 339: "Bridge nodes failed to overlap correctly") are never
asserted. A test could pass because `result.IsValid` is false for the wrong
reason (e.g., a cycle rather than disconnection).

**Evidence:**

```csharp
// ValidationServiceTests.cs:175 — checks IsValid but not ErrorMessage
Assert.False(result.IsValid);
Assert.Equal(2, result.ComponentCount);
// ErrorMessage is never checked

// GraphValidationService.cs:339 — error message never asserted in any test
return new ValidationResult(false, "The graph is not fully connected. Bridge nodes failed to overlap correctly.", componentCount);
```

**Remediation:** Add
`Assert.Equal("The judging graph contains cycles.", result.ErrorMessage)` and
`Assert.Equal("The graph is not fully connected. Bridge nodes failed to overlap correctly.", result.ErrorMessage)`
to the relevant tests.

---

## TE-008 — IsTotalOrder/IsValidOrder never tested with LessThan operator (MEDIUM)

**Severity:** medium | **Category:** coverage-gap | **Effort:** small

**Files:**

- `src/ContestJudging.Services/Validation/GraphValidationService.cs:80`
- `src/ContestJudging.Services/Validation/GraphValidationService.cs:166`
- `tests/ContestJudging.Tests/ValidationServiceTests.cs:15`
- `tests/ContestJudging.Tests/ValidationServiceTests.cs:89`

All 10 validation tests use only `Operator.GreaterThan` and `Operator.EqualTo`.
The `Operator.LessThan` code path in both `IsTotalOrder` (line 80-83) and
`IsValidOrder` (line 166-168) has zero coverage. The UI's keyboard handler
records all three operators (lines 163-176 of Judging.razor.cs), so this path is
exercised in production but untested.

**Evidence:**

```csharp
// GraphValidationService.cs:80-83 — untested LessThan branch
else if (rel.Operator == Operator.LessThan)
{
    u = rootB;
    v = rootA;
}
```

**Remediation:** Add one test each for IsTotalOrder and IsValidOrder with a
LessThan relation, verifying the directional inversion works correctly.

---

## TE-009 — Non-existent ID, duplicate, and empty-table repository edge cases untested (MEDIUM)

**Severity:** medium | **Category:** coverage-gap | **Effort:** small

**Files:**

- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:22`
- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:34`
- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:51`
- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:78`
- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:167`
- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:194`
- `tests/ContestJudging.Tests/InfrastructureTests.cs:35`

Matches Pass 1 TEST-009. Confirmed: no tests for GetByIdAsync with non-existent
ID (should return null), AddAsync with duplicate ID (should throw), DeleteAsync
with non-existent ID (should silently succeed), or GetAllAsync on empty table
(should return empty collection). `GetByCategoryIdAsync` with non-existent
category is partially tested implicitly (it returns empty by design at line
197).

**Evidence:**

```csharp
// SqliteRepositories.cs:22 — GetByIdAsync with non-existent ID untested
var entity = await _context.Categories.FindAsync(id);
return entity == null ? null : new Category(entity.Id, entity.MaxScore);
```

**Remediation:** Add tests for all with-non-existent-ID and empty-table
scenarios. Add test for duplicate AddAsync expecting DbUpdateException.

**Related:** TEST-009

---

## TE-010 — Tests pass null! for ContestDbContext masking null safety (MEDIUM)

**Severity:** medium | **Category:** mock-quality | **Effort:** small

**Files:**

- `tests/ContestJudging.Tests/ContestManagerTests.cs:30`
- `tests/ContestJudging.Tests/ContestManagerTests.cs:64`
- `tests/ContestJudging.Tests/ContestManagerTests.cs:101`
- `src/ContestJudging.Services/Managers/ContestManager.cs:23`

All three ContestManagerTests pass `null!` for the `ContestDbContext` parameter.
While the tested methods don't use `_context` directly (they delegate to
repositories), the `null!` suppresses any compile-time or runtime null check. If
a future maintainer adds code that accesses `_context` in a tested method, the
null would surface only in production, not in tests. Additionally, any test of
the non-repository gateway methods (AddCategoryAsync, AddEntryAsync,
AddRelationAsync) would mask null-dereference.

**Evidence:**

```csharp
// ContestManagerTests.cs:30 — all three instantiations
var manager = new ContestManager(... , null!);

// ContestManager.cs:23 — the field never null-checked
private readonly ContestDbContext _context;
```

**Remediation:** Either abstract `ContestDbContext` behind an
`IDatabaseExportImport` interface (preferred — see TE-002) or at minimum add a
constructor null check and use a mock in tests.

**Related:** TE-002, TEST-004

---

## TE-011 — E2E tests are too shallow to detect functional regressions (LOW)

**Severity:** low | **Category:** assertion-strength | **Effort:** medium

**Files:**

- `tests/ContestJudging.E2ETests/AppE2ETests.cs:23`
- `tests/ContestJudging.E2ETests/AppE2ETests.cs:30`

Two E2E tests exist: one checks the homepage title and content, the other clicks
"Get Started" and waits for a heading. Neither test exercises any functional
workflow — no adding categories/entries, no recording relations, no calculating
results, no verifying scores. They serve as smoke tests but give false
confidence. They also don't exercise backup/restore across page reloads, which
is the primary failure mode for this Blazor WASM + SQLite app.

**Evidence:**

```csharp
// AppE2ETests.cs:30-37 — navigation test with no functional assertions
await Page.Locator("text=Get Started").ClickAsync();
await Expect(Page.Locator("h2:has-text('Contest Setup')")).ToBeVisibleAsync();
```

**Remediation:** Add E2E test for end-to-end judging workflow: create category >
add entries > record comparisons > calculate results > verify leaderboard order.
Add test for backup/restore: add data > reload page > verify data persisted.

**Related:** TEST-002, TEST-007

---

## TE-012 — TrimmingSafetyTests trivia: string-contains assertions on JSON (LOW)

**Severity:** low | **Category:** assertion-strength | **Effort:** trivial

**Files:**

- `tests/ContestJudging.Tests/TrimmingSafetyTests.cs:33`
- `tests/ContestJudging.Tests/TrimmingSafetyTests.cs:34`

The `JsonSerialization_ShouldWork_WithDomainEntities` test uses
`Assert.Contains("E1", json)` and `Assert.Contains("85.5", json)` as structural
assertions. These are fragile — any string containing "E1" or "85.5" anywhere in
the JSON would pass, even if the serialization structure is corrupt. The
deserialized object check (line 38) correctly validates the round-trip.

**Evidence:**

```csharp
Assert.Contains("E1", json);      // weak: could match property name, nested value, etc.
Assert.Contains("85.5", json);    // weak: could match anything numeric
```

**Remediation:** Remove the string-contains assertions — the deserialized object
check at line 38-39 is sufficient. If JSON structure must be validated, use
`JsonDocument.Parse` and check specific property paths.

---

## TE-013 — PartitionService corner cases (k=0, overlap > 1, k > n) untested (LOW)

**Severity:** low | **Category:** coverage-gap | **Effort:** small

**Files:**

- `src/ContestJudging.Services/Partitioning/PartitionService.cs:16`
- `src/ContestJudging.Services/Partitioning/PartitionService.cs:17`
- `tests/ContestJudging.Tests/PartitionServiceTests.cs:13`

The constructor validates `kPartitions <= 0` and `overlapRate < 0 || > 1`, but
these validations are never proven in tests. No test verifies that
`GeneratePartitions` throws `ArgumentException` for invalid inputs.
Additionally, edge cases like `kPartitions > allEntryIds.Count`,
`overlapRate = 1.0` (all nodes should appear in every partition), and empty
`allEntryIds` are not covered.

**Evidence:**

```csharp
// PartitionService.cs:16-17 — validation paths untested
if (kPartitions <= 0) throw new ArgumentException(...);
if (overlapRate < 0 || overlapRate > 1) throw new ArgumentException(...);
```

**Remediation:** Add Theory-based parameterized tests: k=0 throws, k=-1 throws,
overlap=-0.1 throws, overlap=1.1 throws, k > n (should it throw or handle
gracefully?), empty allEntryIds.

---

## TE-014 — GraphValidationService methods duplicate 99% of code via copy-paste (LOW)

**Severity:** low | **Category:** maintainability | **Effort:** small

**Files:**

- `src/ContestJudging.Services/Validation/GraphValidationService.cs:43`
- `src/ContestJudging.Services/Validation/GraphValidationService.cs:129`
- `src/ContestJudging.Services/Validation/GraphValidationService.cs:214`

Matches Pass 1 CQ-002. `IsTotalOrder` (lines 43-127) and `IsValidOrder` (lines
129-212) share ~85 identical lines differing only by
`if (queue.Count > 1) return false;` on line 109. `GetSortedTiers` (lines
214-310) shares the first 60+ lines of union-find + adjacency building. The code
duplication means tests only exist for specific methods but bugs in the shared
logic could exist in one method but not the other, making the test suite appear
to pass when it shouldn't regarding the duplicated code.

**Evidence:**

- Lines 43-127 vs 129-212: identical except line 109 (`queue.Count > 1` check)
- Lines 214-276: identical union-find + adjacency building as 43-102 and 129-188

**Remediation:** Extract shared methods `BuildUnionFindAndAdjacency` and
`TryTopologicalSort` (returning order or null). This would require re-audit of
all validation tests to ensure correct coverage after refactor.

**Related:** CQ-002

---

## TE-015 — ScoringStrategyTests never exercises CalculateScores with empty tiers (LOW)

**Severity:** low | **Category:** coverage-gap | **Effort:** trivial

**Files:**

- `src/ContestJudging.Services/Scoring/LinearSpacingScoring.cs:15`
- `src/ContestJudging.Services/Scoring/PercentileScoring.cs:16`
- `src/ContestJudging.Services/Scoring/DefinedIntervalScoring.cs:22`
- `tests/ContestJudging.Tests/ScoringStrategyTests.cs:13`

All three scoring implementations have an early-return for `k == 0` (empty
tiers). `PercentileScoring` additionally has
`totalEntries == 1 || sortedTiers.Count == 1` early return and
`totalEntries == 0` guard. None of these paths are tested. A typo in the
empty-tiers return (e.g., returning null instead of empty dictionary) would pass
all existing tests.

**Evidence:**

```csharp
// All three scoring files contain untested early returns
if (k == 0) return assignedScores;     // LinearSpacing, DefinedInterval
if (totalEntries == 0) return assignedScores;  // Percentile
if (totalEntries == 1 || sortedTiers.Count == 1) { ... return assignedScores; }  // Percentile
```

**Remediation:** Add one test per strategy with empty tier list. Add one test
for PercentileScoring with single entry and single tier.

---

## TE-016 — Entry.SetScore boundary: exactly at maxScore and exactly 0 untested (LOW)

**Severity:** low | **Category:** coverage-gap | **Effort:** trivial

**Files:**

- `src/ContestJudging.Core/Entities/Entry.cs:19`
- `tests/ContestJudging.Tests/CoreTests.cs:12`
- `tests/ContestJudging.Tests/CoreTests.cs:36`

`Entry.SetScore` accepts `score >= 0 && score <= category.MaxScore`. In
CoreTests, the valid score test uses `5` for a `MaxScore: 10` category
(mid-range), and the invalid tests use `11` and `-1`. The boundary values
`score = 0` (valid) and `score = maxScore` (valid) are never tested. If the
condition used `>` instead of `>=` (line 19), no test would catch it.

**Evidence:**

```csharp
// Entry.cs:19 — boundary condition never tested at edges
if (score >= 0 && score <= category.MaxScore)

// CoreTests.cs:27-33 — only tests mid-range value 5
entry.SetScore(category, 5);  // neither 0 nor 10 tested
```

**Remediation:** Add test for `SetScore(category, 0)` (valid) and
`SetScore(category, category.MaxScore)` (valid) as parameterized Theory cases.

**Related:** TEST-001

---

## Coverage Matrix

| Component                     | Method                              | Tested?                                    | Gap Risk |
| ----------------------------- | ----------------------------------- | ------------------------------------------ | -------- |
| Category                      | Constructor (valid + throws)        | Yes (CoreTests)                            | —        |
| Entry                         | SetScore (valid + throws)           | Partial — no boundary values               | low      |
| Entry                         | TotalScore                          | Yes                                        | —        |
| LinearSpacingScoring          | CalculateScores                     | Yes                                        | —        |
| LinearSpacingScoring          | CalculateScoresFromStrengths        | **No**                                     | **high** |
| PercentileScoring             | CalculateScores                     | Yes                                        | —        |
| PercentileScoring             | CalculateScoresFromStrengths        | **No**                                     | **high** |
| DefinedIntervalScoring        | CalculateScores                     | Yes                                        | —        |
| DefinedIntervalScoring        | CalculateScoresFromStrengths        | **No**                                     | **high** |
| GraphValidationService        | IsTotalOrder                        | Partial — no LessThan operator             | medium   |
| GraphValidationService        | IsValidOrder                        | Partial — no LessThan operator             | medium   |
| GraphValidationService        | GetSortedTiers                      | Yes                                        | —        |
| GraphValidationService        | ValidatePartitionedGraph            | Partial — no ErrorMessage assertions       | medium   |
| BradleyTerryResolutionService | ResolveGlobalStrengths              | Partial — no convergence path              | **high** |
| PartitionService              | GeneratePartitions                  | Partial — non-deterministic + corner cases | **high** |
| ContestManager                | All delegated methods               | Partial                                    | —        |
| ContestManager                | CalculateGlobalScores (error paths) | **No**                                     | **high** |
| ContestManager                | ExportDataAsync                     | **No**                                     | **high** |
| ContestManager                | ImportDataAsync                     | **No**                                     | **high** |
| SqliteCategoryRepository      | Add, Get, Delete                    | Yes                                        | —        |
| SqliteCategoryRepository      | Update, GetAll                      | **No**                                     | medium   |
| SqliteEntryRepository         | Add, Get, Delete                    | Yes                                        | —        |
| SqliteEntryRepository         | Update, GetAll                      | **No**                                     | medium   |
| SqliteRelationRepository      | Add, Get, Delete                    | Add/Get only; Delete **No**                | medium   |
| Webshop pages                 | All Blazor logic                    | **No**                                     | **high** |
| ServiceCollectionExtensions   | AddContestJudgingServices           | **No**                                     | medium   |
| Program.cs                    | Restore pipeline                    | **No**                                     | **high** |
| LocalStorage                  | Backup/Restore                      | **No**                                     | **high** |

---

## Test Isolation Assessment

| Aspect                             | Status            | Notes                                                                                                                               |
| ---------------------------------- | ----------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| Shared mutable state between tests | **None detected** | Each InfrastructureTest creates its own in-memory SqliteConnection                                                                  |
| Test ordering dependencies         | **None detected** | No `[Collection]` or `[TestCaseOrderer]` attributes                                                                                 |
| Class fixtures used                | **None**          | No `IClassFixture` or `ICollectionFixture` usage                                                                                    |
| Random-order failures              | **Likely**        | PartitionService uses unseeded Random; same test could produce different intersection sizes on different runs                       |
| Thread safety                      | **Not tested**    | No parallel test runners configured; parallel InfrastructureTests may conflict if using same file-backed DB (but they use :memory:) |

---

## Test Effectiveness Score: 4/10

**Rationale:**

- **+2** for basic entity validation (Category, Entry, Relation)
- **+1** for in-memory database integration tests (InfrastructureTests)
- **+1** for comprehensive validation algorithm happy-path coverage
  (IsTotalOrder, IsValidOrder, GetSortedTiers, ValidatePartitionedGraph)
- **-3** for zero Web/UI layer tests (entire Blazor project, LocalStorage,
  backup/restore)
- **-2** for non-deterministic PartitionService tests that undermine test
  confidence
- **-2** for untestable export/import due to concrete DbContext dependency
- **-1** for critically untested BradleyTerry convergence path (the most complex
  algorithm in the system)
- **-1** for all three scoring strategies having their `FromStrengths` path
  untested
- **+1** for clean mock patterns in ContestManagerTests (when they work)

**Bottom line:** The tests that exist are reasonably well-structured but
shallow. The system's highest-risk components (BradleyTerry convergence,
database export/import, LocalStorage persistence, Web UI logic) are entirely
untested or untestable. The test suite gives a false sense of security.
