# P1-B: Code Quality Audit Report

**Agent:** P1-B | **Domain:** code-quality | **Pass:** 1 | **Status:** success

## Scope

Scanned all 28 `.cs` files across 4 source projects (Core, Infrastructure,
Services, Web) totalling ~1,920 lines. Analyzed against `.editorconfig` rules
and standard C# quality metrics.

---

## Findings Summary

| ID     | Severity | Category        | Title                                                |
| ------ | -------- | --------------- | ---------------------------------------------------- |
| CQ-001 | high     | dead-code       | Dead empty Class1.cs in Infrastructure project       |
| CQ-002 | high     | duplication     | Near-identical IsTotalOrder and IsValidOrder methods |
| CQ-003 | high     | error-handling  | Swallowed exception in database restore path         |
| CQ-004 | medium   | style           | var usage violations across codebase                 |
| CQ-005 | medium   | performance     | No sealed classes anywhere                           |
| CQ-006 | medium   | maintainability | Large methods exceeding 50 lines                     |
| CQ-007 | medium   | performance     | N+1 / client-side join in SqliteEntryRepository      |
| CQ-008 | low      | maintainability | Magic numbers without named constants                |
| CQ-009 | low      | style           | Tuple usage instead of ValueTuple                    |
| CQ-010 | low      | performance     | Missing sealed on nested UnionFind class             |

---

## Detailed Findings

### CQ-001 — Dead empty class `Class1.cs`

**Severity:** high | **Category:** dead-code\
**Files:** `src/ContestJudging.Infrastructure/Class1.cs:1-6`\
**Rule violated:** Stub/placeholder code in production source\
**Evidence:**

```csharp
namespace ContestJudging.Infrastructure;
public class Class1
{
}
```

**Remediation:** Delete the file. It serves no purpose and clutters the
assembly. **Effort:** trivial

---

### CQ-002 — Duplicate code: `IsTotalOrder` vs `IsValidOrder`

**Severity:** high | **Category:** duplication\
**Files:**
`src/ContestJudging.Services/Validation/GraphValidationService.cs:43-127`,
`src/ContestJudging.Services/Validation/GraphValidationService.cs:129-212`\
**Rule violated:** DRY principle\
**Evidence:** Both methods are 85 and 84 lines respectively, identical except
for one line:

```csharp
// IsTotalOrder (line 109):
if (queue.Count > 1) return false;

// IsValidOrder: this line is absent.
```

Both independently construct UnionFind, process relations, build adjacency
lists, and perform topological sort. **Remediation:** Extract a shared private
method (e.g., `TryTopologicalSort`) that returns the sorted order or null on
cycle. `IsTotalOrder` wraps it checking for unique order, `IsValidOrder` checks
for no cycles. **Effort:** small

---

### CQ-003 — Swallowed exception in database restore

**Severity:** high | **Category:** error-handling\
**Files:** `src/ContestJudging.Web/Program.cs:49-52`\
**Rule violated:** Best practice — never swallow exceptions silently\
**Evidence:**

```csharp
catch (Exception ex)
{
    Console.WriteLine($"Failed to restore database: {ex.Message}");
}
```

The app catches `Exception` broadly, logs only `Message` (losing stack trace),
and continues execution with an empty database. The user is never informed.
**Remediation:** Either (a) rethrow, (b) set a flag to show user-facing error,
or (c) catch only specific storage exceptions. At minimum, log `ex.ToString()`
instead of `ex.Message`. **Effort:** small

---

### CQ-004 — `var` usage violations against editorconfig

**Severity:** medium | **Category:** style\
**Files:** 11+ files, ~50+ locations\
**Rule violated:** `.editorconfig` lines 24-26
(`csharp_style_var_for_built_in_types=true:suggestion`,
`csharp_style_var_when_type_is_apparent=true:suggestion`,
`csharp_style_var_elsewhere=true:suggestion`)\
**Evidence (representative):**

```csharp
// GraphValidationService.cs — 19 explicit type declarations
string root = uf.Find(entryId);          // should be: var root
int processedNodes = 0;                  // should be: var processedNodes

// BradleyTerryResolutionService.cs — 16 explicit type declarations
int n = allEntryIdsList.Count;           // should be: var n
double sum = nextGamma.Sum();            // should be: var sum

// SqliteRepositories.cs — 33 explicit type declarations vs 33 var uses (mixed style)
```

**Remediation:** Run `dotnet format` on the solution to auto-fix `var`
preferences. The editorconfig already has the right settings; the code just
wasn't formatted against them. **Effort:** trivial (automated)

---

### CQ-005 — No sealed classes anywhere

**Severity:** medium | **Category:** performance\
**Files:** All 15+ class declarations across `src/`\
**Rule violated:** CA1852 (performance) — Type can be sealed when it has no
visible subclasses\
**Evidence:** Every concrete class in the codebase is unsealed: `Category`,
`Entry`, `Relation`, `CategoryEntity`, `EntryEntity`, `EntryScoreEntity`,
`RelationEntity`, `SqliteCategoryRepository`, `SqliteEntryRepository`,
`SqliteRelationRepository`, `GraphValidationService`, `PercentileScoring`,
`LinearSpacingScoring`, `DefinedIntervalScoring`,
`BradleyTerryResolutionService`, `PartitionService`, `ContestManager`,
`CategoryModel`, `EntryModel`, `LeaderboardItem`. None are designed for
inheritance; they all implement interfaces or are POCOs. **Remediation:** Add
`sealed` to all leaf classes. For DI-registered classes implementing interfaces,
sealing allows the JIT to devirtualize calls. For entity/POCO classes, sealing
prevents the virtual method table overhead. **Effort:** small

---

### CQ-006 — Large methods exceeding 50 lines

**Severity:** medium | **Category:** maintainability\
**Files:**
`src/ContestJudging.Services/Validation/GraphValidationService.cs:43`,
`src/ContestJudging.Services/Validation/GraphValidationService.cs:129`,
`src/ContestJudging.Services/Validation/GraphValidationService.cs:214`,
`src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:14`\
**Rule violated:** Code readability — methods >50 lines degrade readability and
testability\
**Evidence:**

| Method                   | Lines | File:Line                           |
| ------------------------ | ----- | ----------------------------------- |
| `ResolveGlobalStrengths` | 116   | BradleyTerryResolutionService.cs:14 |
| `GetSortedTiers`         | 97    | GraphValidationService.cs:214       |
| `IsTotalOrder`           | 85    | GraphValidationService.cs:43        |
| `IsValidOrder`           | 84    | GraphValidationService.cs:129       |

`ResolveGlobalStrengths` in particular has 3-deep nested loops (iter, i, j) and
mixes iteration logic with rank stability early-exit logic. **Remediation:**
Extract inner loops into named private methods (e.g., `IterateBradleyTerryStep`,
`ComputeRanksFromGammas`, `BuildTopologicalTiers`). This also reduces
indentation depth. **Effort:** medium

---

### CQ-007 — Inefficient query pattern in `SqliteEntryRepository`

**Severity:** medium | **Category:** performance\
**Files:**
`src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:101-125`\
**Rule violated:** CA1822 / query efficiency — avoid client-side joins when
database joins are available\
**Evidence:**

```csharp
var categories = await _context.Categories.ToListAsync();  // load ALL categories
// ... then for each entry, for each score:
var categoryEntity = categories.FirstOrDefault(c => c.Id == scoreEntity.CategoryId);
```

This loads every category into memory, then does O(n*m) `FirstOrDefault` scans
for each entry-score pair. The `EntryScoreEntity` already has `CategoryId`; a
proper EF Core `.Include()` or a navigation property on `EntryScoreEntity` to
`CategoryEntity` would produce a single SQL JOIN. **Remediation:** Add a
navigation property from `EntryScoreEntity` to `CategoryEntity` and use
`.Include()` to eager-load, or join via `_context.Categories` with `.Where()`
directly in the outer query. **Effort:** small

---

### CQ-008 — Magic numbers without named constants

**Severity:** low | **Category:** maintainability\
**Files:**

- `src/ContestJudging.Services/Scoring/LinearSpacingScoring.cs:57`
- `src/ContestJudging.Services/Scoring/DefinedIntervalScoring.cs:50`
- `src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:90`,
  `:108`\
  **Evidence:**

```csharp
if (range < 1e-9)            // LinearSpacingScoring.cs:57, DefinedIntervalScoring.cs:50
if (iter > 50 && iter % 10 == 0)   // BradleyTerryResolutionService.cs:90
if (stable && maxDiff < 1e-3)      // BradleyTerryResolutionService.cs:108
```

**Remediation:** Extract to named constants:

- `1e-9` → `const double Epsilon = 1e-9;`
- `50`, `10` →
  `const int RankStabilityCheckStart = 50; const int RankStabilityCheckInterval = 10;`
- `1e-3` → `const double RankStabilityThreshold = 1e-3;` **Effort:** trivial

---

### CQ-009 — `Tuple<string, string>` should be `(string, string)`

**Severity:** low | **Category:** style\
**Files:** `src/ContestJudging.Web/Pages/Judging.razor.cs:36`\
**Rule violated:** Use modern C# value tuples over reference tuples\
**Evidence:**

```csharp
private Tuple<string, string>? suggestedPair;  // line 36
// ...
suggestedPair = new Tuple<string, string>(a, b);  // line 131
```

`Tuple<T1, T2>` is a reference type; `ValueTuple<T1, T2>` (`(string, string)`)
is a struct with named elements (`.Item1` → semantic names). **Remediation:**

```csharp
private (string A, string B)? suggestedPair;
suggestedPair = (a, b);
```

**Effort:** trivial

---

### CQ-010 — Missing `sealed` on private nested `UnionFind` class

**Severity:** low | **Category:** performance\
**Files:**
`src/ContestJudging.Services/Validation/GraphValidationService.cs:10`\
**Rule violated:** CA1852 — even private nested types benefit from `sealed`\
**Evidence:**

```csharp
private class UnionFind
{
    // ...
}
```

**Remediation:** Change to `private sealed class UnionFind`. **Effort:** trivial

---

## Metrics

| Metric                | Value  |
| --------------------- | ------ |
| Files scanned         | 28     |
| Total findings        | 10     |
| Lines of code scanned | ~1,920 |
| High severity         | 3      |
| Medium severity       | 4      |
| Low severity          | 3      |
| Informational         | 0      |

---

## Notes

- No `TODO`, `FIXME`, `HACK`, or `XXX` comments were found.
- No commented-out code was found.
- No `async void` usage was found.
- No `.ConfigureAwait(false)` is used — acceptable in Blazor WASM where there is
  no `SynchronizationContext`.
- No `throw new Exception()` (base type) was found; all exceptions use specific
  types.
- No `IDisposable`/`IAsyncDisposable` violations were found (DbContext is
  properly scoped with `using`).
- No string concatenation in loops or boxing issues were identified.
- The `ImplicitUsings` and `Nullable` are enabled globally via
  `Directory.Build.props`.
- `DefinedIntervalScoring.cs` uses `Math.Round` without explicit `using System;`
  — this works because `System` is in the implicit global usings.
- `ContestManager.CalculateGlobalScoresAsync` performs sequential `await` in a
  loop for `UpdateAsync` — consider `Task.WhenAll` for independent updates in a
  future optimization.
