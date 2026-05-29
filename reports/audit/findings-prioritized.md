# P3-B: Findings Prioritization Report

**Agent:** P3-B | **Pass:** 3 | **Domain:** prioritization | **Date:**
2026-05-25

---

## Summary

| Metric               | Value |
| -------------------- | ----- |
| Total findings       | 103   |
| Tier 1 (Immediate)   | 20    |
| Tier 2 (This Sprint) | 41    |
| Tier 3 (Backlog)     | 42    |
| Critical severity    | 1     |
| High severity        | 17    |
| Medium severity      | 41    |
| Low severity         | 31    |
| Informational        | 13    |

---

## Dispute & Waiver Notes

### CQ-001 Severity Dispute (Validator V1-B)

V1-B flagged CQ-001 ("Dead empty class Class1.cs") as `severity_inconsistent` —
a 6-line empty class rated `high` alongside CQ-003 (swallowed exception with
data loss risk). The validator recommended `low` or `informational`. **Original
severity retained** per policy. The finding is ranked at position 5 in Tier 1
due to its trivial fix cost — regardless of whether the severity is defensible,
the fix is a 5-second file deletion and should be done immediately.

### P1-E CI/CD Gate Override

V1-E rejected 7 of 11 CICD findings (CICD-001, CICD-004, CICD-006, CICD-007,
CICD-008, CICD-009, CICD-011) for schema format violations: line ranges
(`52-60`), directory-only paths (`release/`, `.github/`). The gate override
accepted all 11 as PASS — underlying paths resolve correctly, evidence is sound,
but the `^[^:]+:\d+$` schema pattern is too restrictive for CI/CD domain
findings. A **schema waiver** is noted; the schema should be relaxed post-audit
to allow `file:start-end` ranges and directory references for infrastructure
domains.

---

## Dependency Chains

### Chain A: Export/Import Testability

```
ARCH-004 (SRP violation in ContestDbContext)
  └─ ARCH-002 (ContestManager takes concrete DbContext)
       ├─ TE-002 (ExportDataAsync/ImportDataAsync structurally untestable)
       ├─ TE-003 (backup/restore pipeline zero coverage)
       ├─ TE-010 (null! for ContestDbContext in tests)
       ├─ TEST-004 (ContestManager null! in tests)
       └─ TEST-010 (ExportDataAsync/ImportDataAsync untested)
```

**Strategy:** Fix ARCH-004 first (extract IDatabaseBackupService), then ARCH-002
(inject abstraction), then TE-002/TE-003/TE-010 become independently testable.

### Chain B: Validation Algorithm DRY

```
CQ-002 (duplicate IsTotalOrder/IsValidOrder methods)
  ├─ ALGO-002 (100% duplicated Kahn's algorithm in 3 methods)
  ├─ ALGO-001 (self-loop inconsistency)
  └─ TE-014 (misleading test coverage from duplication)
```

**Strategy:** Extract shared topological sort method (CQ-002). ALGO-001/ALGO-002
and TE-014 resolve naturally as byproducts.

### Chain C: Layer Isolation

```
STRUCT-001 (Web references Infrastructure)
  ├─ ARCH-001 (deeper analysis of same issue)
  └─ BW-010 (confirmatory: leak contained to Program.cs)
```

ARCH-001 and BW-010 are analysis depth findings; STRUCT-001 is the actionable
remediation.

### Chain D: Database Restore Safety

```
CQ-003 (swallowed exception in restore)
  └─ EF-003 (broader: active DbContext overwrite + swallowed exception)
       └─ BW-007 (additional: schema version + integrity checks)
```

EF-003 is the most comprehensive; fix it to address CQ-003 and enable BW-007
improvements.

---

## Tier 1: Immediate (Must Fix Before Production Use)

**20 findings** — 1 critical, 17 high, 2 medium

| Rank | ID         | Severity | Effort  | Title                                                                                      |
| ---- | ---------- | -------- | ------- | ------------------------------------------------------------------------------------------ |
| 1    | STRUCT-003 | critical | trivial | E2E tests reference packages not declared in Directory.Packages.props (build-breaking CPM) |
| 2    | ARCH-004   | medium   | small   | ContestDbContext SRP violation — ORM + file I/O mixed **(unblocks Chain A)**               |
| 3    | ARCH-002   | high     | small   | ContestManager depends on concrete ContestDbContext **(unblocks chain)**                   |
| 4    | CQ-002     | high     | small   | Near-identical IsTotalOrder/IsValidOrder methods **(unblocks Chain B)**                    |
| 5    | CQ-001     | high     | trivial | Dead empty class Class1.cs ⚠️ severity disputed by V1-B                                    |
| 6    | BW-004     | high     | trivial | Bootstrap JS not loaded — accordion UI non-functional                                      |
| 7    | CICD-001   | high     | small   | E2E test project excluded from solution ℹ️ schema waiver (range format)                    |
| 8    | CICD-004   | high     | small   | No SAST/CodeQL scanning ℹ️ schema waiver (range format)                                    |
| 9    | EF-001     | high     | small   | No database migration files; uses EnsureCreatedAsync() only                                |
| 10   | EF-002     | high     | small   | Missing foreign key relationships in entity configuration                                  |
| 11   | CQ-003     | high     | small   | Swallowed exception in database restore path                                               |
| 12   | TEST-001   | high     | small   | No parameterized tests — uses sequential Assert.Throws in [Fact]                           |
| 13   | TE-001     | high     | small   | PartitionService tests non-deterministic (unseeded Random)                                 |
| 14   | TE-005     | high     | small   | CalculateScoresFromStrengths untested across all scoring strategies                        |
| 15   | SEC-007    | medium   | small   | No authentication on admin operations                                                      |
| 16   | EF-003     | high     | medium  | Database restore overwrites active DbContext; swallowed exception masks data loss          |
| 17   | TE-002     | high     | medium  | ExportDataAsync/ImportDataAsync structurally untestable **(blocked by ARCH-002/004)**      |
| 18   | TE-003     | high     | medium  | LocalStorage backup/restore pipeline zero coverage **(blocked by ARCH-002/004)**           |
| 19   | TE-004     | high     | medium  | BradleyTerry convergence and early-exit paths completely untested                          |
| 20   | TEST-002   | high     | large   | ContestJudging.Web has zero unit tests                                                     |

### Tier 1 Effort Summary

| Effort    | Count  | Est. Hours  |
| --------- | ------ | ----------- |
| trivial   | 3      | 0.5 h       |
| small     | 12     | 12-24 h     |
| medium    | 4      | 12-24 h     |
| large     | 1      | 20-40 h     |
| **Total** | **20** | **45-89 h** |

---

## Tier 2: This Sprint

**41 findings** — medium + low-quick-wins

| Rank | ID         | Severity | Effort  | Title                                                                               |
| ---- | ---------- | -------- | ------- | ----------------------------------------------------------------------------------- |
| 21   | STRUCT-001 | medium   | medium  | Layer isolation violation: Web → Infrastructure reference                           |
| 22   | CQ-004     | medium   | trivial | Widespread var usage violations (auto-fixable via `dotnet format`)                  |
| 23   | CICD-002   | medium   | trivial | No code coverage collected in CI                                                    |
| 24   | CICD-006   | medium   | trivial | release/ directory committed to repo ℹ️ schema waiver                               |
| 25   | CICD-007   | medium   | trivial | No Dependabot configuration ℹ️ schema waiver                                        |
| 26   | STRUCT-007 | medium   | trivial | .gitignore missing SQLite DB file patterns                                          |
| 27   | SEC-002    | medium   | trivial | Insecure Random in PartitionService                                                 |
| 28   | SEC-003    | low      | trivial | Hardcoded connection string in Program.cs                                           |
| 29   | EF-007     | low      | trivial | Connection string hardcoded (not from configuration)                                |
| 30   | SEC-006    | medium   | trivial | Missing .db/.sqlite patterns in .gitignore                                          |
| 31   | SEC-001    | medium   | small   | Missing Content-Security-Policy header                                              |
| 32   | CICD-003   | medium   | small   | Floating major-version GitHub Actions tags                                          |
| 33   | CICD-011   | medium   | small   | Deploy job redundantly rebuilds ℹ️ schema waiver                                    |
| 34   | SEC-004    | medium   | small   | Database backup lacks integrity verification                                        |
| 35   | BW-001     | medium   | trivial | OnInitializedAsync lacks exception handling in all pages                            |
| 36   | BW-002     | medium   | small   | Excessive backup on every CRUD operation                                            |
| 37   | BW-003     | medium   | small   | No localStorage quota check before backup                                           |
| 38   | BW-005     | medium   | small   | Interactive divs missing ARIA roles/keyboard handlers                               |
| 39   | BW-006     | medium   | small   | Large lists without Virtualize component                                            |
| 40   | BW-007     | medium   | small   | DB restore lacks schema version + integrity checks **(blocked by ARCH-004)**        |
| 41   | ALGO-001   | medium   | small   | GetSortedTiers silently ignores self-loops **(blocked by CQ-002)**                  |
| 42   | ALGO-002   | medium   | small   | 100% duplicated Kahn's algorithm in 3 methods **(blocked by CQ-002)**               |
| 43   | EF-005     | medium   | trivial | Missing .AsNoTracking() on read-only queries                                        |
| 44   | CQ-005     | medium   | small   | No sealed classes (15+ unsealed concrete classes)                                   |
| 45   | TE-007     | medium   | trivial | ValidatePartitionedGraph tests don't assert error message content                   |
| 46   | TE-008     | medium   | trivial | LessThan operator never tested in validation                                        |
| 47   | CQ-007     | medium   | small   | O(n*m) client-side join in SqliteEntryRepository                                    |
| 48   | EF-004     | medium   | small   | O(n*m) client-side join (extended CQ-007)                                           |
| 49   | EF-006     | medium   | small   | CalculateGlobalScoresAsync without transaction wrapping                             |
| 50   | TEST-003   | medium   | trivial | No test categories/traits (Unit vs Integration)                                     |
| 51   | TEST-004   | medium   | medium  | ContestManager takes concrete DbContext; tests pass null! **(blocked by ARCH-002)** |
| 52   | TEST-005   | medium   | small   | ServiceCollectionExtensions.AddContestJudgingServices untested                      |
| 53   | TEST-008   | medium   | trivial | E2E package versions missing from Directory.Packages.props                          |
| 54   | STRUCT-002 | medium   | trivial | E2E test project not in solution file                                               |
| 55   | STRUCT-009 | medium   | small   | Orphaned testapp scaffold project                                                   |
| 56   | ARCH-001   | medium   | medium  | Web→Infrastructure reference (STRUCT-001 depth)                                     |
| 57   | ARCH-003   | medium   | medium  | Service interfaces in Services instead of Core                                      |
| 58   | CQ-006     | medium   | medium  | Large methods >50 lines (4 methods in 3 files)                                      |
| 59   | TE-006     | medium   | small   | Repository UpdateAsync/GetAllAsync zero test coverage                               |
| 60   | TE-009     | medium   | small   | Repository edge cases untested                                                      |
| 61   | TE-010     | medium   | small   | Tests pass null! for ContestDbContext **(blocked by ARCH-002)**                     |

### Tier 2 Effort Summary

| Effort    | Count  | Est. Hours  |
| --------- | ------ | ----------- |
| trivial   | 16     | 4-8 h       |
| small     | 20     | 20-40 h     |
| medium    | 5      | 15-30 h     |
| **Total** | **41** | **39-78 h** |

---

## Tier 3: Backlog

**42 findings** — informational, low-priority, blocked, or deferred

| Rank | ID         | Severity      | Effort  | Title                                                                     |
| ---- | ---------- | ------------- | ------- | ------------------------------------------------------------------------- |
| 59   | ARCH-005   | low           | medium  | Composition root in Services instead of Web **(blocked by ARCH-001/002)** |
| 60   | ARCH-007   | informational | medium  | DbContext effectively singleton in Blazor WASM                            |
| 64   | EF-008     | low           | trivial | Missing indexes on FK columns                                             |
| 65   | EF-010     | low           | trivial | Redundant surrogate key on EntryScoreEntity                               |
| 66   | EF-012     | informational | trivial | No .IsRequired()/.HasMaxLength() on string properties                     |
| 67   | ALGO-003   | low           | trivial | UnionFind lacks union-by-rank                                             |
| 68   | ALGO-004   | low           | trivial | Theoretical division-by-zero in BradleyTerry                              |
| 69   | ALGO-005   | low           | trivial | Partition bridge count can be zero                                        |
| 70   | CQ-008     | low           | trivial | Magic numbers without constants                                           |
| 71   | CQ-009     | low           | trivial | Tuple<> should be ValueTuple                                              |
| 72   | CQ-010     | low           | trivial | Missing sealed on private UnionFind class                                 |
| 73   | CICD-005   | low           | trivial | OSV-Scanner --allow-no-lockfiles                                          |
| 74   | CICD-008   | low           | trivial | No CODEOWNERS file                                                        |
| 75   | CICD-009   | low           | trivial | No PR/issue templates                                                     |
| 76   | CICD-010   | informational | trivial | dotnet-tools.json empty                                                   |
| 77   | SEC-005    | informational | trivial | Exception message exposed to browser console                              |
| 78   | SEC-008    | informational | trivial | Client-side SQLite architecture note                                      |
| 79   | BW-008     | low           | trivial | Console.WriteLine instead of ILogger                                      |
| 80   | BW-009     | low           | trivial | Orphaned weather.json scaffold                                            |
| 81   | BW-010     | informational | medium  | STRUCT-001 follow-up: leak contained **(blocked by ARCH-001)**            |
| 82   | STRUCT-006 | informational | small   | Mixed test frameworks                                                     |
| 83   | STRUCT-010 | informational | trivial | Trim analyzer enabled globally                                            |
| 84   | ARCH-006   | low           | trivial | Unregistered IScoringStrategy implementations                             |
| 85   | ARCH-008   | informational | small   | Inconsistent interface placement **(blocked by ARCH-003)**                |
| 86   | TEST-007   | low           | small   | E2E NUnit vs xUnit inconsistency                                          |
| 87   | TEST-009   | low           | small   | Repository edge cases untested                                            |
| 88   | TEST-010   | low           | small   | ExportDataAsync/ImportDataAsync untested **(blocked by ARCH-002)**        |
| 89   | TEST-013   | informational | small   | No cancellation token support                                             |
| 90   | TE-011     | low           | medium  | E2E tests are shallow smoke tests                                         |
| 91   | TE-014     | low           | small   | Duplicate validation code misleading coverage **(blocked by CQ-002)**     |
| 92   | EF-009     | low           | small   | No error handling around SaveChangesAsync                                 |
| 93   | EF-011     | informational | small   | DbContext holds file I/O methods **(blocked by ARCH-004)**                |
| 94   | TE-012     | low           | trivial | Fragile string-contains assertions                                        |
| 95   | TE-013     | low           | trivial | PartitionService constructor validation untested                          |
| 96   | TE-015     | low           | trivial | ScoringStrategy empty-tiers edge cases untested                           |
| 97   | TE-016     | low           | trivial | Entry.SetScore boundary values untested                                   |
| 98   | STRUCT-004 | low           | trivial | E2ETests missing RootNamespace/AssemblyName                               |
| 99   | STRUCT-005 | low           | trivial | Orphaned Class1.cs scaffold (duplicate of CQ-001)                         |
| 100  | STRUCT-008 | low           | trivial | Redundant TargetFramework overrides in Web.csproj                         |
| 101  | TEST-006   | low           | trivial | Vague test class name 'CoreTests'                                         |
| 102  | TEST-011   | informational | trivial | Dead Class1.cs (triplicate)                                               |
| 103  | TEST-012   | informational | small   | TrimmingSafetyTests suppresses trimming warnings                          |

### Tier 3 Effort Summary

| Effort    | Count  | Est. Hours  |
| --------- | ------ | ----------- |
| trivial   | 28     | 7-14 h      |
| small     | 10     | 10-20 h     |
| medium    | 4      | 12-24 h     |
| **Total** | **42** | **29-58 h** |

---

## Top 3 Most Impactful Fixes

### 1. ARCH-004 + ARCH-002: Extract IDatabaseBackupService, inject into ContestManager

- **Effort:** small (ARCH-004) + small (ARCH-002) = ~4-8 hours total
- **Unblocks:** TE-002, TE-003, TE-010, TEST-004, TEST-010, EF-011 (6 findings)
- **Risk:** The current `null!` pattern in tests masks a NullReferenceException
  that would crash production if any code path accesses `_context` before
  Export/Import. Fixing ARCH-002 eliminates this production risk AND enables
  testing of the entire backup/restore pipeline.

### 2. CQ-002: Extract shared topological sort from IsTotalOrder/IsValidOrder/GetSortedTiers

- **Effort:** small (~2-4 hours)
- **Unblocks:** ALGO-001, ALGO-002, TE-014 (3 findings)
- **Risk:** Three methods with 100% duplicated logic will inevitably diverge in
  bugfixes. ALGO-001 already demonstrates this — GetSortedTiers silently skips
  self-loops while the other two reject them. A single extraction eliminates the
  class of bugs caused by fixing one copy but not the others.

### 3. EF-001: Add EF Core migrations (replace EnsureCreatedAsync with MigrateAsync)

- **Effort:** small (~1-2 hours)
- **Risk:** `EnsureCreatedAsync()` is documented by Microsoft as "for
  testing/in-memory only." In production, it cannot handle schema changes — the
  database must be deleted and recreated, destroying all contest data. Enabling
  migrations is the minimum bar for production schema management.

---

## Overall Effort Estimate

| Tier      | Findings | Trivial | Small  | Medium | Large | Est. Total Hours |
| --------- | -------- | ------- | ------ | ------ | ----- | ---------------- |
| Tier 1    | 20       | 3       | 12     | 4      | 1     | 45-89 h          |
| Tier 2    | 41       | 16      | 20     | 5      | 0     | 39-78 h          |
| Tier 3    | 42       | 28      | 10     | 4      | 0     | 29-58 h          |
| **Total** | **103**  | **47**  | **42** | **13** | **1** | **113-225 h**    |

---

## Severity Distribution by Tier

| Severity      | Tier 1 | Tier 2 | Tier 3 | Total |
| ------------- | ------ | ------ | ------ | ----- |
| critical      | 1      | 0      | 0      | 1     |
| high          | 17     | 0      | 0      | 17    |
| medium        | 2      | 39     | 0      | 41    |
| low           | 0      | 2      | 29     | 31    |
| informational | 0      | 0      | 13     | 13    |

---

## Notes for Remaining Pass 3 Agents

1. **Duplicates not merged:** Multiple agents independently surfaced the same
   underlying issues (e.g., CQ-001/STRUCT-005/TEST-011 all flag Class1.cs). The
   findings are left as-is for triage; fixing the highest-ranked instance
   resolves the others.

2. **P1-E schema waiver:** CICD-001, CICD-004, CICD-006, CICD-007, CICD-008,
   CICD-009, CICD-011 use non-conformant file references. The `files[]` entries
   in this report have been normalized to single-line format where possible.
   Missing-file references (dependabot.yml, CODEOWNERS,
   PULL_REQUEST_TEMPLATE.md) use `:0` as convention for absent files.

3. **CQ-001 severity flag:** P4 (remediation) may choose to downgrade CQ-001 to
   `low` per validator recommendation while keeping it in Tier 1 due to
   zero-effort resolution.
