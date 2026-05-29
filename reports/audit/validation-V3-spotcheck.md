# Pass 3 — V3 SpotCheck Validation Report

**Agent:** V3-SpotCheck\
**Date:** 2026-05-25\
**Audit scope:** All P1 and P2 finding JSON files\
**Method:** Systematic proportional sampling (~20% across domains)

---

## Sample Selection

Total findings across all 10 files: **103**

| Domain                | File                                   | Count |
| --------------------- | -------------------------------------- | ----- |
| Structure             | findings-P1-structure.json             | 10    |
| Code Quality          | findings-P1-code-quality.json          | 10    |
| Tests                 | findings-P1-tests.json                 | 13    |
| Security              | findings-P1-security.json              | 8     |
| CI/CD                 | findings-P1-cicd.json                  | 11    |
| Architecture          | findings-P2-architecture.json          | 8     |
| Algorithm Correctness | findings-P2-algorithm-correctness.json | 5     |
| Blazor WASM           | findings-P2-blazor-wasm.json           | 10    |
| EFCore                | findings-P2-efcore.json                | 12    |
| Test Effectiveness    | findings-P2-test-effectiveness.json    | 16    |

**Sample size:** 21 (20.4%)\
**Selection method:** Proportional stratified — at least 1 finding from each of
the 10 domains, weighted by domain size. Selected manually to maximize file
coverage across domains.

---

## Per-Finding Accuracy Assessment

### 1. STRUCT-001 — "Layer isolation violation: Web project directly references Infrastructure layer"

**Files verified:**

- `src/ContestJudging.Web/ContestJudging.Web.csproj:29-30,36`
- `src/ContestJudging.Web/Program.cs:5,12,21`

**Evidence verification:** CONFIRMED

- Web.csproj line 29:
  `<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />`
- Web.csproj line 30:
  `<PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" />`
- Web.csproj line 36:
  `<ProjectReference Include="..\ContestJudging.Infrastructure\..."/>`
- Program.cs line 5: `using ContestJudging.Infrastructure.Persistence;`
- Program.cs line 12: `using Microsoft.EntityFrameworkCore;`
- Program.cs line 21: `SQLitePCL.Batteries_V2.Init();`

**Severity:** `medium` — Reasonable for architecture violation that works but
violates Clean Architecture principles.

**Remediation:** Actionable and technically correct — removing the
Infrastructure reference from Web requires lifting the composition root.

**Rating:** ✅ **ACCURATE**

---

### 2. STRUCT-003 — "E2E tests reference packages not declared in Directory.Packages.props"

**Files verified:**

- `tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj:14-17`
- `Directory.Packages.props`

**Evidence verification:** CONFIRMED

- E2ETests.csproj lines 14-17: NUnit, NUnit.Analyzers, NUnit3TestAdapter,
  Microsoft.Playwright.NUnit all defined as `<PackageReference>` without
  version.
- Directory.Packages.props: Zero `<PackageVersion>` entries for any of these
  four packages. Central Package Management is enabled (line 3:
  `ManagePackageVersionsCentrally` = true).

**Severity:** `critical` — Justified. This is build-breaking when central
package management is active; `dotnet restore` will fail.

**Remediation:** Actionable and correct.

**Rating:** ✅ **ACCURATE**

---

### 3. CQ-002 — "Near-identical IsTotalOrder and IsValidOrder methods in GraphValidationService"

**Files verified:**

- `src/ContestJudging.Services/Validation/GraphValidationService.cs:43-127,129-212`

**Evidence verification:** CONFIRMED

- `IsTotalOrder` (lines 43–127): 85 lines. Builds UnionFind, adjacency,
  in-degree, runs Kahn's algorithm. Line 109:
  `if (queue.Count > 1) return false;`
- `IsValidOrder` (lines 129–212): 84 lines. Identical logic except missing
  line 109. Same UnionFind construction, adjacency building, Kahn BFS.
- The only behavioral difference is the `queue.Count > 1` check on line 109.

**Severity:** `high` — Could argue medium (no functional bug, purely maintenance
risk), but the duplication is significant (170+ lines of near-duplicate logic)
and introduces divergent-fix risk.

**Remediation:** Actionable and correct — extract `TryBuildTopologicalOrder` as
shared private method.

**Rating:** ✅ **ACCURATE**

---

### 4. CQ-003 — "Swallowed exception in database restore path"

**Files verified:**

- `src/ContestJudging.Web/Program.cs:49-51`

**Evidence verification:** CONFIRMED

```csharp
catch (Exception ex)
{
    Console.WriteLine($"Failed to restore database: {ex.Message}");
}
```

Only `ex.Message` is logged; no stack trace, no error flag, no user
notification.

**Severity:** `high` — Reasonable. The restore path is the only persistence
mechanism in a client-side WASM app; silent failure means data loss.

**Remediation:** Actionable and correct.

**Rating:** ✅ **ACCURATE**

---

### 5. TEST-001 — "No parameterized tests (Theories) used — all tests use Fact with sequential Assert.Throws"

**Files verified:**

- `tests/ContestJudging.Tests/CoreTests.cs:12-16,36-41`

**Evidence verification:** CONFIRMED

- `Category_Constructor_ThrowsWhenMaxScoreIsOneOrLess` (line 12): Single
  `[Fact]` with two sequential `Assert.Throws` for maxScore=1 and maxScore=0.
- `Entry_SetScore_InvalidScore_Throws` (line 36): Single `[Fact]` with two
  sequential `Assert.Throws` for score=11 and score=-1.
- If first assertion throws, second never executes, potentially masking bugs.

**Severity:** `high` — Slightly elevated; the tests work but aren't best
practice. Should be medium.

**Remediation:** Actionable and correct — convert to `[Theory]` with
`[InlineData]`.

**Rating:** ✅ **ACCURATE** (severity slightly high but the factual basis is
solid)

---

### 6. TEST-004 — "ContestManager constructor takes concrete ContestDbContext; tests pass null!"

**Files verified:**

- `src/ContestJudging.Services/Managers/ContestManager.cs:23,32`
- `tests/ContestJudging.Tests/ContestManagerTests.cs:30,64,101`

**Evidence verification:** CONFIRMED

- ContestManager.cs line 23: `private readonly ContestDbContext _context;`
- ContestManager.cs line 32: `ContestDbContext context` in constructor.
- ContestManagerTests.cs line 30: `new ContestManager(..., null!);` — all three
  test instantiations pass `null!` for the DbContext parameter.

**Severity:** `medium` — Appropriate. No NRE today because
ExportDataAsync/ImportDataAsync are never called in unit tests, but the null
suppression masks fragility.

**Remediation:** Actionable and correct.

**Rating:** ✅ **ACCURATE**

---

### 7. TEST-009 — "Repository edge cases untested (non-existent IDs, duplicates, nulls, empty tables)"

**Files verified:**

- `tests/ContestJudging.Tests/InfrastructureTests.cs` (full file, 160 lines)

**Evidence verification:** CONFIRMED

- Only 5 tests exist, all happy-path:
  - `CategoryRepository_AddAndGet_Succeeds`
  - `EntryRepository_AddWithScores_Succeeds`
  - `RelationRepository_AddAndGet_Succeeds`
  - `CategoryRepository_Delete_Cascades`
  - `EntryRepository_Delete_Cascades`
- Zero tests for: GetByIdAsync with non-existent ID, AddAsync with duplicate,
  DeleteAsync with non-existent ID, GetAllAsync on empty table, AddAsync with
  null entity, UpdateAsync with non-existent entity.

**Severity:** `low` — Reasonable. The finding itself notes this correctly.

**Remediation:** Actionable and correct.

**Rating:** ✅ **ACCURATE**

---

### 8. SEC-002 — "Insecure random number generation using System.Random in PartitionService"

**Files verified:**

- `src/ContestJudging.Services/Partitioning/PartitionService.cs:9,24`

**Evidence verification:** CONFIRMED

- Line 9: `private readonly Random _random = new();`
- Line 24:
  `var shuffled = allEntryIdsList.OrderBy(x => _random.Next()).ToList();`

**Severity:** `medium` — Debatable. This is a judgment-shuffle use case, not
cryptographic. `System.Random` is perfectly adequate for shuffling entries into
partitions. The category "weak-cryptography" is misleading; this isn't
cryptography at all. The remediation even acknowledges this with "use
Random.Shared with explicit documentation that the randomness is
non-cryptographic."

**Note on context:** The finding correctly identifies the code pattern, but the
framing as a security/cryptography weakness is overstated. The non-seeded
`new Random()` is more a testability concern (tests can't reproduce shuffle
order) than a security issue.

**Rating:** ✅ **ACCURATE** (with overstated severity/category — not a security
finding, should be low/performance or low/testing)

---

### 9. SEC-004 — "Database backup in localStorage lacks integrity verification"

**Files verified:**

- `src/ContestJudging.Web/Pages/Setup.razor.cs:49-57`
- `src/ContestJudging.Web/Pages/Judging.razor.cs:76-84`
- `src/ContestJudging.Web/Program.cs:39-53`

**Evidence verification:** CONFIRMED

- Setup.razor.cs lines 49-57: Exports database, Base64-encodes, writes to
  localStorage as `"db_backup"`. No HMAC, no checksum, no versioning.
- Judging.razor.cs lines 76-84: Same pattern.
- Program.cs lines 39-53: On startup restore, reads `"db_backup"` from
  localStorage, Base64-decodes, imports raw bytes. No integrity check before
  restoring.

**Severity:** `medium` — Reasonable for a local-device judging app. Could be
higher if data corruption from localStorage is a realistic risk.

**Remediation:** Actionable and correct.

**Rating:** ✅ **ACCURATE**

---

### 10. CICD-001 — "E2E test project excluded from solution"

**Files verified:**

- `ContestJudging.slnx` (full file, 11 lines)

**Evidence verification:** CONFIRMED

- Solution only contains 5 projects: Core, Infrastructure, Services, Web (in
  `/src/`), and ContestJudging.Tests (in `/tests/`).
- `tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj` exists on disk
  but is not listed in the solution file.
- CI runs `dotnet test ContestJudging.slnx` — E2E tests are never executed.

**Severity:** `high` — Justified. E2E tests are entirely excluded from CI.

**Remediation:** Actionable and correct.

**Rating:** ✅ **ACCURATE**

---

### 11. CICD-006 — "release/ directory is committed to the repository"

**Files verified:**

- `.gitignore` (full file, 500 lines)
- `release/` disk directory
- `git ls-files release/` (returns empty — NOT tracked)

**Evidence verification:** REFUTED

- `.gitignore` line 25: `[Rr]elease/` — this pattern already matches `release/`
  (case-insensitive). The pattern has existed in the `.gitignore` since the file
  was generated by `dotnet new gitignore`.
- `git ls-files release/` returns **zero output** — no files under `release/`
  are tracked by git.
- The `release/` directory exists on disk (from a local `dotnet publish`), but
  it is NOT committed to the repository.
- The finding's claim that "release/ directory is committed to the repository"
  is false. The evidence snippet listing specific files
  (`ContestJudging.Web.runtimeconfig.json`, `dotnet.js`, etc.) as if they were
  committed is incorrect.

**Severity:** N/A — finding is based on incorrect evidence.

**Remediation:** The suggested remediation ("Add release/ to .gitignore") is
already satisfied by line 25 of `.gitignore`. The `git rm -r --cached release/`
portion would be a no-op since no files are tracked.

**Rating:** ❌ **HALLUCINATED** — The finding claims a problem that does not
exist. `.gitignore` already covers the `release/` directory, and no files under
`release/` are tracked by git. The evidence snippet is fabricated.

---

### 12. ARCH-002 — "ContestManager directly depends on concrete ContestDbContext from Infrastructure"

**Files verified:**

- `src/ContestJudging.Services/Managers/ContestManager.cs:9,23,32`
- `tests/ContestJudging.Tests/ContestManagerTests.cs:30,64,101`

**Evidence verification:** CONFIRMED

- ContestManager.cs line 9: `using ContestJudging.Infrastructure.Persistence;` —
  imports Infrastructure namespace in Services layer.
- ContestManager.cs lines 23,32: Constructor accepts concrete
  `ContestDbContext`.
- ContestManagerTests.cs lines 30,64,101: All test instantiations pass `null!`
  for the DbContext.

**Severity:** `high` — Justified. This is a clean DIP violation; the Services
layer depends on a concrete Infrastructure class.

**Remediation:** Actionable and correct — extract IDatabaseBackupService
interface.

**Rating:** ✅ **ACCURATE**

---

### 13. ARCH-004 — "ContestDbContext violates Single Responsibility — ORM + file I/O"

**Files verified:**

- `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:65-80`

**Evidence verification:** CONFIRMED

- Lines 66-80: `ExportDatabaseAsync()` and `ImportDatabaseAsync()` perform raw
  file I/O (`File.ReadAllBytesAsync`, `File.WriteAllBytesAsync`) directly on
  `"contest.db"`. These are in a DbContext class alongside ORM entity
  configuration.

**Severity:** `medium` — Reasonable. Functional but poor separation of concerns.

**Remediation:** Actionable and correct.

**Rating:** ✅ **ACCURATE**

---

### 14. ALGO-001 — "GetSortedTiers silently ignores contradictory self-loop edges"

**Files verified:**

- `src/ContestJudging.Services/Validation/GraphValidationService.cs:90,176,264`

**Evidence verification:** CONFIRMED

- Line 90 (IsTotalOrder): `if (u == v) return false;`
- Line 176 (IsValidOrder): `if (u == v) return false;`
- Line 264 (GetSortedTiers): `if (u == v) continue;`
- Three methods have three different behaviors for the same condition.
  GetSortedTiers silently ignores self-loops instead of rejecting them.

**Severity:** `medium` — Reasonable for defense-in-depth consistency.

**Remediation:** Actionable and correct.

**Rating:** ✅ **ACCURATE**

---

### 15. BW-004 — "Bootstrap JavaScript not loaded — accordion UI is non-functional"

**Files verified:**

- `src/ContestJudging.Web/wwwroot/index.html` (full file, 34 lines)
- `src/ContestJudging.Web/Pages/Judging.razor:94-145`

**Evidence verification:** CONFIRMED

- index.html: Only Bootstrap CSS is loaded (line 10:
  `lib/bootstrap/dist/css/bootstrap.min.css`). No `<script>` tag for Bootstrap
  JS bundle.
- Judging.razor lines 97-100: Accordion uses `data-bs-toggle="collapse"` and
  `data-bs-target="#manualEntry"` which require Bootstrap JavaScript. Without
  the JS bundle, the accordion will not toggle.
- The only script loaded is `_framework/blazor.webassembly#[.{fingerprint}].js`
  on line 31.

**Severity:** `high` — Justified. The manual override accordion is
non-functional — a real UX bug.

**Remediation:** Actionable and correct — either load Bootstrap JS or replace
with Blazor-native conditional rendering.

**Rating:** ✅ **ACCURATE**

---

### 16. BW-005 — "Interactive div elements missing ARIA roles, keyboard handlers, and tabindex"

**Files verified:**

- `src/ContestJudging.Web/Pages/Judging.razor:6,65-66,74-75,84-85`
- `src/ContestJudging.Web/Layout/NavMenu.razor:4`

**Evidence verification:** CONFIRMED

- Judging.razor line 65-66:
  `<div class="judge-card..." @onclick="() => RecordResult(Operator.GreaterThan)">`
  — no `role`, no `tabindex`, no `@onkeydown`.
- Judging.razor line 74-75:
  `<div class="tie-button..." @onclick="() => RecordResult(Operator.EqualTo)">`
  — same lack.
- Judging.razor line 84-85:
  `<div class="judge-card..." @onclick="() => RecordResult(Operator.LessThan)">`
  — same lack.
- NavMenu.razor line 4: `<button title="Navigation menu"...>` — has `title` but
  no `aria-label`. The title does provide accessible name for buttons, making
  this the weakest part of the finding.
- The judging container div on line 6 does have `tabindex="0"` and
  `@onkeydown="HandleKeyDown"`, which partially mitigates the keyboard issue
  (global key handler), but individual judge cards remain keyboard-inaccessible.

**Severity:** `medium` — Reasonable for WCAG non-compliance.

**Remediation:** Actionable and correct.

**Rating:** ✅ **ACCURATE**

---

### 17. EF-002 — "Missing foreign key relationships in entity configuration"

**Files verified:**

- `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:53-63`

**Evidence verification:** CONFIRMED

- `OnModelCreating` configures only: `HasKey` for 4 entities, and one
  `HasIndex().IsUnique()` for EntryScoreEntity.
- Zero `HasOne()`, `HasForeignKey()`, or `OnDelete()` calls.
- SQLite foreign keys are off by default; without explicit FK configuration,
  referential integrity is not enforced at database level.
- The manual cascade deletion code in `SqliteRepositories.cs` (e.g., lines
  56-63, 172-178) exists precisely because FKs are not configured.

**Severity:** `high` — Justified. Referential integrity is a foundational data
concern.

**Remediation:** Actionable and correct.

**Rating:** ✅ **ACCURATE**

---

### 18. EF-006 — "CalculateGlobalScoresAsync performs individual entry updates without transaction wrapping"

**Files verified:**

- `src/ContestJudging.Services/Managers/ContestManager.cs:106-114`

**Evidence verification:** CONFIRMED

- Lines 107-113: Loop over entries, call `UpdateAsync(entry)` for each. Each
  `UpdateAsync` calls `SaveChangesAsync()` internally (see SqliteRepositories.cs
  line 163).
- No `BeginTransactionAsync()` / `CommitTransactionAsync()` wrapping.
- If the scoring loop is interrupted mid-way, some entries are updated and
  others are not, leaving inconsistent state.

**Severity:** `medium` — Reasonable. Could be higher for data consistency.

**Remediation:** Actionable and correct.

**Rating:** ✅ **ACCURATE**

---

### 19. TE-001 — "PartitionService tests are non-deterministic due to unseeded Random"

**Files verified:**

- `src/ContestJudging.Services/Partitioning/PartitionService.cs:9,21,24`
- `tests/ContestJudging.Tests/PartitionServiceTests.cs:22,31`

**Evidence verification:** CONFIRMED (code) but ASSERTION IS DETERMINISTIC

- PartitionService.cs line 9: `private readonly Random _random = new();` —
  unseeded.
- PartitionServiceTests.cs line 31: `Assert.Equal(10, common.Count);` — This
  assertion IS deterministic:
  - n=100 entries, overlapRate=0.10, k=2 partitions
  - bCount = `Math.Round(100 * 0.10)` = 10
  - Exactly 10 items are designated as bridge nodes (first bCount from shuffled
    list)
  - All 10 are placed in both partitions
  - Remaining 90 items are distributed disjointly across partitions
  - `partition["0"] ∩ partition["1"]` = exactly the bridge nodes = always 10

**Why this is exaggerated:** The finding claims the test is "non-deterministic"
and the assertion can fail, but `Assert.Equal(10, common.Count)` is
mathematically guaranteed to pass regardless of the random seed. The source of
randomness (unseeded `new Random()`) is a valid testability concern (can't
reproduce exact partition assignments for debugging), but the finding
mischaracterizes the specific assertion as non-deterministic when it is provably
deterministic.

**Severity:** `high` — Overstated. A testability/reproducibility concern, not a
flaky-test concern.

**Remediation:** The suggested remediation (inject seeded Random) is correct for
reproducibility but the framing as non-deterministic is inaccurate.

**Rating:** ⚠️ **EXAGGERATED** — The unseeded Random is a real testability
issue, but the claim that `Assert.Equal(10, common.Count)` is non-deterministic
is incorrect. The assertion result is deterministic given the mathematical
guarantees of the algorithm.

---

### 20. TE-004 — "BradleyTerry resolution convergence and early-exit paths completely untested"

**Files verified:**

- `src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:56-119`
- `tests/ContestJudging.Tests/ResolutionServiceTests.cs` (full file, 62 lines)

**Evidence verification:** CONFIRMED

- BradleyTerryResolutionService.cs lines 56-119: Full MLE iterative scaling loop
  with convergence threshold (1e-6), rank-stability early-exit (iter > 50, every
  10th), and MaxIterations cap (1000).
- ResolutionServiceTests.cs: Only 2 tests, both with ≤3 entries that converge in
  exactly 1 iteration:
  - `ResolveGlobalStrengths_ShouldProduceTransitiveRanks`: 3 entries, converges
    in 1 iter.
  - `ResolveGlobalStrengths_WithEqualRelations_ShouldProduceEqualStrengths`: 2
    entries.
- Neither test reaches: rank-stability check (requires >50 iterations),
  convergence threshold check (requires gradual convergence), MaxIterations
  exhaustion.

**Severity:** `high` — Justified. Complex algorithmic paths with zero coverage.

**Remediation:** Actionable and correct.

**Rating:** ✅ **ACCURATE**

---

### 21. TE-007 — "ValidatePartitionedGraph tests fail to assert error message content"

**Files verified:**

- `tests/ContestJudging.Tests/ValidationServiceTests.cs:153-229`
- `src/ContestJudging.Services.Validation/GraphValidationService.cs:312-343`

**Evidence verification:** CONFIRMED

- Test `ValidatePartitionedGraph_DisconnectedGraph_ShouldReturnInvalid` (line
  153): Asserts `result.IsValid` (false) and `result.ComponentCount` (2). No
  assertion on `result.ErrorMessage`.
- Test `ValidatePartitionedGraph_WithCycles_ShouldReturnInvalid` (line 205):
  Asserts only `result.IsValid` (false). No assertion on `result.ErrorMessage`.
- GraphValidationService.cs line 320: Returns "The judging graph contains
  cycles."
- GraphValidationService.cs line 339: Returns "The graph is not fully connected.
  Bridge nodes failed to overlap correctly."
- A test could return `IsValid=false` for the wrong reason and pass these
  assertions.

**Severity:** `medium` — Reasonable. Weak assertions.

**Remediation:** Actionable and correct.

**Rating:** ✅ **ACCURATE**

---

## Accuracy Summary

| Rating       | Count  | Percentage |
| ------------ | ------ | ---------- |
| Accurate     | 19     | 90.5%      |
| Exaggerated  | 1      | 4.8%       |
| Hallucinated | 1      | 4.8%       |
| **Total**    | **21** | **100%**   |

**Accuracy Rate: 90.5%** (19/21 findings are factually accurate)

---

## Inaccurate Finding Details

### ❌ CICD-006 — Hallucinated

**Claim:** "release/ directory (published Blazor WASM output) is committed to
the repository"

**Reality:** `.gitignore` line 25 contains `[Rr]elease/` which already matches
`release/` (case-insensitive). `git ls-files release/` returns zero output — no
files under `release/` are tracked by git. The `release/` directory exists on
disk from local `dotnet publish` but is not committed.

**Correction:** This finding should be removed or converted to an informational
note that the `release/` directory appears in the working tree from local builds
but is already properly gitignored. The evidence snippet listing specific files
as committed is fabricated.

### ⚠️ TE-001 — Exaggerated

**Claim:** "PartitionService tests are non-deterministic due to unseeded Random"
with `Assert.Equal(10, common.Count)` as non-deterministic evidence.

**Reality:** The assertion `Assert.Equal(10, common.Count)` is mathematically
deterministic. With n=100 entries and overlapRate=0.10,
`Math.Round(100 * 0.10) = 10` bridge nodes are always assigned to both
partitions, guaranteeing an intersection count of exactly 10 regardless of
random seed. The unseeded Random is a valid testability/reproducibility concern
but does NOT make the assertion non-deterministic.

**Correction:** Change title to "PartitionService uses unseeded Random
preventing reproducible test runs" and lower severity from `high` to `low`. The
finding should focus on reproducibility for debugging, not flaky tests.

---

## Systematic Issues

1. **Blazor WASM, EFCore, and Code Quality domains are consistently accurate.**
   All findings sampled from these domains (BW-004, BW-005, EF-002, EF-006,
   CQ-002, CQ-003) had exact evidence matches.

2. **P1-E (CI/CD domain) has the only hallucinated finding (CICD-006).** This
   agent may have inferred the release/ directory was committed from seeing it
   on disk without checking `git ls-files` or the existing gitignore patterns.
   Other CICD findings checked (CICD-001) were accurate.

3. **P2-E (Test Effectiveness) has the only exaggerated finding (TE-001).** The
   agent correctly identified the unseeded Random issue but misdiagnosed the
   impact (claiming non-deterministic assertions when the assertion is provably
   deterministic).

4. **Severity drift across domains:** Several findings have reasonable
   evidentiary basis but severity is slightly inflated:
   - TEST-001 (high — should be medium)
   - SEC-002 (medium for non-crypto shuffle — should be low)
   - TE-001 (high for testability concern — should be low)

5. **Duplicate findings across passes:** Several P1 and P2 findings describe the
   same underlying issue (e.g., STRUCT-001/ARCH-001/BW-010 all describe the
   Web→Infrastructure dependency; CQ-007/EF-004 both cover the O(n*m)
   client-side join). This is expected cross-pass behavior but inflates the
   total finding count.

---

## Overall Confidence: **HIGH**

With a 90.5% accuracy rate and only one hallucinated finding (a non-existent git
hygiene issue), the audit as a whole is reliable. The errors are concentrated in
edge-case analysis (git tracking state, mathematical determinism of assertions)
rather than core code-reading. The vast majority of findings have exact evidence
matches in the source code, accurate titles, and actionable remediations.

**Recommendation:** Filter or correct CICD-006 and TE-001 before presenting
results. The remaining 19/21 verified findings (and by extrapolation,
approximately 93/103 total findings) are trustworthy.
