# Re-Audit Report — P1: Code Quality (Pass 1-B)

**Agent**: PA1-B\
**Domain**: code-quality\
**Branch**: `fix/audit-remediate`\
**Original findings**: `reports/audit/findings-P1-code-quality.json` (10
findings: 3 high, 4 medium, 3 low)\
**Date**: 2026-05-26

---

## 1. Executive Summary

| Metric             | Count        |
| ------------------ | ------------ |
| Original findings  | 10           |
| **Resolved**       | **4**        |
| Partially resolved | 1            |
| Unresolved         | 5            |
| New findings       | 4            |
| Net delta          | +0 (10 → 10) |
| **Quality trend**  | **STABLE**   |

The remediation successfully addressed the 3 high-severity findings (CQ-001 dead
code, CQ-002 code duplication, CQ-003 swallowed exception) and 1 low-severity
finding (CQ-010 sealed UnionFind). However, 5 medium/low findings remain
unaddressed and 4 new issues were introduced by the refactoring, resulting in no
net change in finding count. Overall code quality is slightly improved due to
resolution of the critical issues.

---

## 2. Verification of Original Fixes

### 2.1 Resolved (4/10)

#### CQ-001 — Dead Class1.cs (high) **RESOLVED**

- `src/ContestJudging.Infrastructure/Class1.cs` confirmed deleted.
- Zero references to `Class1` anywhere in the codebase.

#### CQ-002 — Duplicate IsTotalOrder/IsValidOrder (high) **RESOLVED**

- `GraphValidationService.cs` refactored with two new private methods:
  - `BuildTopologicalGraph` (line 44): constructs UnionFind, adjacency lists,
    and in-degree map.
  - `TryTopologicalSort` (line 114): shared BFS-based Kahn's algorithm with
    `checkUnique` parameter.
- `IsTotalOrder` (line 147) and `IsValidOrder` (line 160) are now 13-line thin
  wrappers.
- **Verification**: `GraphValidationService.cs:44-171` — correct.

#### CQ-003 — Swallowed Exception in Program.cs (high) **RESOLVED**

- Original `catch (Exception ex) { Console.WriteLine(...) }` at `Program.cs:49`
  removed.
- Error handling moved to `BackupService.TryRestoreBackupAsync` (line 47-59).
- Uses `ILogger.LogError(ex, "Failed to restore database from backup")` — proper
  structured logging with full stack trace.

#### CQ-010 — Missing sealed on UnionFind (low) **RESOLVED**

- `GraphValidationService.cs:11`: `private sealed class UnionFind` — confirmed.

### 2.2 Partially Resolved (1/10)

#### CQ-006 — Large Methods >50 Lines (medium) **PARTIALLY RESOLVED**

- `GetSortedTiers` reduced from 97 to 38 lines.
- `IsTotalOrder`/`IsValidOrder` reduced from 85/84 to 13 lines each.
- **Remaining**: `ResolveGlobalStrengths` still 115 lines
  (`BradleyTerryResolutionService.cs:14-128`).
- **New issue**: `BuildTopologicalGraph` is 69 lines — the extracted method
  itself exceeds the threshold (see RA-CQ-014).

### 2.3 Not Resolved (5/10)

#### CQ-004 — var Violations (medium) **NOT RESOLVED**

- ~35+ explicit-type local variable declarations violate `.editorconfig`:
  - `csharp_style_var_for_built_in_types = true:suggestion`
  - `csharp_style_var_when_type_is_apparent = true:suggestion`
  - `csharp_style_var_elsewhere = true:suggestion`
- **Worst files**: `GraphValidationService.cs` (7),
  `BradleyTerryResolutionService.cs` (7), `PercentileScoring.cs` (5),
  `LinearSpacingScoring.cs` (5), `DefinedIntervalScoring.cs` (6).
- Examples: `string root = uf.Find(...)`, `int n = list.Count`,
  `double sum = .Sum()`.

#### CQ-005 — No Sealed Classes (medium) **NOT RESOLVED**

- 21 public classes remain unsealed. Only `UnionFind` (a private nested class)
  was sealed.
- None of these classes are designed for inheritance.
- JIT devirtualization optimization (CA1852) still inhibited codebase-wide.

#### CQ-007 — O(n*m) Client-Side Join (medium) **NOT RESOLVED**

- `SqliteRepositories.cs:84` and `:108`:
  `categories.FirstOrDefault(c => c.Id == scoreEntity.CategoryId)` inside loops
  unchanged.
- No navigation property or `.Include()` added.

#### CQ-008 — Magic Numbers (low) **NOT RESOLVED**

- `1e-9` in `LinearSpacingScoring.cs:57` and `DefinedIntervalScoring.cs:50`
- `50`/`10` in `BradleyTerryResolutionService.cs:90`
- `1e-3` in `BradleyTerryResolutionService.cs:108`

#### CQ-009 — Tuple → ValueTuple (low) **NOT RESOLVED**

- `Judging.razor.cs:36`: `private Tuple<string, string>? suggestedPair;`
- `Judging.razor.cs:130`: `suggestedPair = new Tuple<string, string>(a, b);`

---

## 3. New Findings (4)

### RA-CQ-011 — GetSortedTiers Duplicates Topological Sort Logic (medium, duplication)

- `GraphValidationService.cs:173-210`
- `GetSortedTiers` manually copies `inDegree`, creates a queue, and runs the
  same BFS-based Kahn's algorithm as `TryTopologicalSort` (line 114-145).
- The two implementations differ only in tier-grouping of results.
- **Remediation**: Have `GetSortedTiers` call `TryTopologicalSort` then group
  the sorted result into tiers.

### RA-CQ-012 — New Backup Classes Not Sealed (low, performance)

- `BackupService.cs:7` and `DatabaseBackupService.cs:5`
- Both are DI-registered implementation classes with no subclasses.
- CA1852 applies — JIT cannot devirtualize interface dispatch through these
  types.

### RA-CQ-013 — Inconsistent Namespace Style in New Files (low, style)

- `BackupService.cs:1-5`, `DatabaseBackupService.cs:1-3`, `IBackupService.cs:1`,
  `IDatabaseBackupService.cs:1`
- New files use file-scoped namespace declarations (`namespace Foo.Bar;`) with
  `using` directives placed **after** the namespace declaration — a non-standard
  pattern that `dotnet format` flags as WHITESPACE errors.
- The existing 27 files use block-scoped namespaces
  (`namespace Foo.Bar { ... }`).
- **Remediation**: Move usings before namespace declaration or convert to
  block-scoped namespace for consistency.

### RA-CQ-014 — New BuildTopologicalGraph Method >50 Lines (medium, maintainability)

- `GraphValidationService.cs:44-112` (69 lines)
- The method performs three distinct operations: UnionFind initialization (lines
  52-58), root-member grouping (lines 64-73), and adjacency/in-degree graph
  building (lines 75-109).
- **Remediation**: Extract the edge-processing loop (lines 75-109) into
  `BuildAdjacencyGraph`.

---

## 4. Full Discovery Scan Results

### 4.1 Dead Code / Unused Usings

- **No issues found.** All usings verified as used. No dead code detected beyond
  the already-removed `Class1.cs`.

### 4.2 Nullability

- `default!` used for Blazor `[Inject]` properties — standard Blazor pattern,
  not a violation.

### 4.3 Access Modifiers

- 21 public classes lack `sealed` (see CQ-005 and RA-CQ-012).
- No `internal` classes exist where `public` is unnecessary.

### 4.4 Async Hygiene

- **No `async void` found.**
- **No `ConfigureAwait` needed** — Blazor WASM has no `SynchronizationContext`.
- **No sync-over-async** (`.Result`, `.GetAwaiter().GetResult()`, `.Wait()`)
  found.

### 4.5 Exception Handling

- `BackupService.cs:54-57`: Proper `ILogger.LogError(ex, ...)` — full stack
  trace logged.
- `GraphValidationService.cs:153-157,167-170`: Catches
  `InvalidOperationException` — acceptable, the methods are boolean checks.
- No empty catch blocks.
- No `throw new Exception()` — all thrown exceptions use typed classes
  (`ArgumentException`, `InvalidOperationException`,
  `ArgumentOutOfRangeException`).

### 4.6 var Usage (Editorconfig)

- ~35+ explicit-type local declarations violate `.editorconfig` lines 24-26.
- `for` loop variables excluded (standard C# convention uses explicit types in
  for loops).
- See CQ-004 for full breakdown.

### 4.7 Naming Conventions

- All interfaces prefixed with `I` — compliant.
- Private fields use `_camelCase` — consistent across codebase.
- No violations found.

### 4.8 IDisposable / IAsyncDisposable

- No explicit `Dispose` overrides needed. `DbContext` is managed by DI
  container. All services are stateless or scoped properly.

### 4.9 String Concatenation / Boxing / Magic Numbers

- No string concatenation in loops found.
- No boxing issues found.
- Magic numbers: see CQ-008 (unresolved).

### 4.10 TODO / FIXME / HACK Comments

- `IContestManager.cs:20`: `// TRICKY OPTIMIZATION #2` — non-standard comment
  style but not a violation.
- `BradleyTerryResolutionService.cs:89`: `// TRICKY OPTIMIZATION #4` — same.
- No `TODO`, `FIXME`, `HACK`, or `XXX` comments found.
- No commented-out code found.
- 5 standard explanatory comments in `Program.cs` — acceptable.

### 4.11 Large Methods (>50 lines)

| Method                   | File                                  | Lines | Status              |
| ------------------------ | ------------------------------------- | ----- | ------------------- |
| `ResolveGlobalStrengths` | `BradleyTerryResolutionService.cs:14` | 115   | CQ-006 (unresolved) |
| `BuildTopologicalGraph`  | `GraphValidationService.cs:44`        | 69    | RA-CQ-014 (new)     |
| `GetSortedTiers`         | `GraphValidationService.cs:173`       | 38    | Resolved (was 97)   |
| `IsTotalOrder`           | `GraphValidationService.cs:147`       | 13    | Resolved (was 85)   |
| `IsValidOrder`           | `GraphValidationService.cs:160`       | 13    | Resolved (was 84)   |

### 4.12 Deep Nesting

- `BradleyTerryResolutionService.ResolveGlobalStrengths`: 3-deep nesting (for →
  for → if).
- All other methods: 2-deep max. No violations beyond the already-identified
  large method.

### 4.13 New Backup Files Review

| File                        | Lines | Issues                                                                                                                                  |
| --------------------------- | ----- | --------------------------------------------------------------------------------------------------------------------------------------- |
| `IBackupService.cs`         | 7     | File-scoped namespace (style inconsistency — see RA-CQ-013)                                                                             |
| `IDatabaseBackupService.cs` | 7     | File-scoped namespace (style inconsistency)                                                                                             |
| `BackupService.cs`          | 60    | Not sealed (RA-CQ-012), usings after namespace (RA-CQ-013). Otherwise clean: proper ILogger usage, schema versioning, good null guards. |
| `DatabaseBackupService.cs`  | 33    | Not sealed (RA-CQ-012), usings after namespace (RA-CQ-013). SQLite magic header validation (lines 25-30) is robust.                     |

---

## 5. Risk Assessment

| Severity  | Resolved | New   | Remaining | Net   |
| --------- | -------- | ----- | --------- | ----- |
| High      | 3        | 0     | 0         | -3    |
| Medium    | 0        | 2     | 4         | +2    |
| Low       | 1        | 2     | 3         | +1    |
| **Total** | **4**    | **4** | **7**     | **0** |

- All high-severity issues are resolved. The codebase is safer from a
  correctness and maintainability perspective.
- The medium-severity gap widened slightly due to RA-CQ-011 (duplication in
  GetSortedTiers) and RA-CQ-014 (new large method).
- The remaining unresolved medium issues (sealed classes, var style, O(n*m)
  join) are technical debt items — they don't affect correctness but impact
  performance and maintainability.

---

## 6. Comparison vs Original Audit

| Dimension             | Before                           | After                                            | Delta             |
| --------------------- | -------------------------------- | ------------------------------------------------ | ----------------- |
| Dead code             | Class1.cs exists                 | Removed                                          | Improved          |
| Code duplication      | IsTotalOrder copies IsValidOrder | Shared TryTopologicalSort                        | Improved          |
| Exception handling    | Swallowed Console.WriteLine      | ILogger with full stack trace                    | Improved          |
| Nested type sealed    | UnionFind not sealed             | UnionFind sealed                                 | Improved          |
| Var style             | ~50 violations                   | ~35 violations                                   | Minor improvement |
| Sealed classes        | 0 sealed                         | 1 sealed (UnionFind)                             | Minor improvement |
| Large methods         | 4 methods >50 lines              | 2 methods >50 lines                              | Minor improvement |
| O(n*m) join           | Present                          | Present                                          | Unchanged         |
| Magic numbers         | 4 locations                      | 4 locations                                      | Unchanged         |
| Tuple→ValueTuple      | Not applied                      | Not applied                                      | Unchanged         |
| New duplication       | N/A                              | GetSortedTiers vs TryTopologicalSort             | Degraded          |
| Namespace consistency | Block-scoped throughout          | 4 new files use file-scoped with inverted usings | Degraded          |

**Overall assessment**: The remediation was effective for the critical issues.
The codebase is more maintainable and safer in its exception handling. However,
the effort appears to have been focused on the high-severity items only — the
medium/low backlog was largely untouched, and the new code introduced minor
stylistic inconsistencies. The quality trend is **STABLE** with slight
improvement in the highest-risk categories.
