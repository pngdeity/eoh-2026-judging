# Re-Audit: P2 Algorithm Correctness (PA2-B)

**Date**: 2026-05-26 **Branch**: `fix/audit-remediate` **Scope**:
GraphValidationService.cs refactoring — shared `BuildTopologicalGraph` +
`TryTopologicalSort`, ALGO-001 self-loop fix

## Verdict

**PASS** — the refactored implementation is algorithmically correct. All 10
tests pass. No correctness regressions vs. original. Three low-severity items
identified for follow-up.

---

## 1. BuildTopologicalGraph Trace

```csharp
private static (UnionFind uf, Dictionary<string, HashSet<string>> adjList,
         Dictionary<string, int> inDegree, Dictionary<string, HashSet<string>> rootToMembers)
    BuildTopologicalGraph(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds)
```

### 1.1 UnionFind Setup (lines 48–58)

- Instantiates `UnionFind` with all entry IDs — each entry is its own root.
- Iterates `relations`; for each `EqualTo`, calls
  `uf.Union(rel.EntryA.Id, rel.EntryB.Id)` to merge equivalence classes.
- **Correct.** Matches original behavior exactly. Only `EqualTo` forms
  equivalence classes; ordering relations are handled separately.

### 1.2 In-Degree + Root-to-Members Construction (lines 60–73)

- Initializes `rootToMembers`, `adjList`, `inDegree`.
- Iterates all entry IDs, finds each entry's root via `uf.Find`.
- First encounter of a root: creates
  `rootToMembers[root] = new HashSet<string>()` and sets `inDegree[root] = 0`.
- Adds each entry ID into `rootToMembers[root]`.
- **Correct.** Every root (equivalence class) gets zero initial in-degree.
  Members are tracked for tier expansion. Original code used
  `inDegree.ContainsKey(root)` as first-encounter guard; new code uses
  `rootToMembers.ContainsKey(root)`. Both are initialized for the same roots —
  semantics identical.

### 1.3 Adjacency List Construction + Self-Loop Detection (lines 75–110)

- Iterates relations. For each, computes `rootA`, `rootB` via UF.
- Direction: `GreaterThan` → edge `rootA → rootB` (winner → loser); `LessThan` →
  edge `rootB → rootA`.
- `EqualTo` → `continue` (already handled by UF union).
- `if (u == v)` → **throws `InvalidOperationException`** (new behavior; was
  `return false` / `continue`).
- Deduplicates edges via `HashSet<string>` adjacency.
- **Correct.** The `throw` replaces divergent behaviors: original
  `IsTotalOrder`/`IsValidOrder` returned `false`, original `GetSortedTiers`
  silently `continue`d. Now unified.

### 1.4 Return Tuple (line 112)

- Returns `(uf, adjList, inDegree, rootToMembers)`.
- CQ-002 remediation also returned `uf` to reduce duplicate `UnionFind`
  construction in the wrapper methods, but current code discards it (`_`).
  Acceptable — `ValidatePartitionedGraph` still constructs its own UF for
  connectivity (separate concern).

---

## 2. TryTopologicalSort Trace

```csharp
private static bool TryTopologicalSort(
    Dictionary<string, HashSet<string>> adjList,
    Dictionary<string, int> inDegree,
    out List<string> sorted,
    bool checkUnique = false)
```

### 2.1 Initialization (lines 120–122)

- Creates `workingInDeg` as a shallow copy of `inDegree` (values are `int` value
  types — safe).
- Initializes queue with all zero-in-degree nodes from `workingInDeg`.

### 2.2 Kahn's BFS Loop (lines 124–144)

- **Uniqueness check** (line 126):
  `if (checkUnique && queue.Count > 1) return false;` — this detects
  non-total-order (branching or disconnected components starting
  simultaneously). Exactly matches original `IsTotalOrder` line 109.
- Dequeues `u`, appends to `sorted`, decrements in-degree of neighbors, enqueues
  when zero.
- Adjacency existence check (line 131): `if (adjList.ContainsKey(u))` — handles
  roots with no outgoing edges (leaf nodes, isolated nodes). Correct.
- **Cycle detection** (line 144): `return sorted.Count == inDegree.Count` — if a
  cycle exists, some nodes never reach zero in-degree, so sorted is incomplete.
  Matches original `processedNodes == inDegree.Count`.

### 2.3 Uniqueness Check Semantics

The `checkUnique` flag correctly differentiates:

- `checkUnique: true` → any branching/comparability gap returns `false` (total
  order requires exactly one source at every step).
- `checkUnique: false` → branching is accepted; only cycles fail.

---

## 3. Public Method Verification

### 3.1 IsTotalOrder (lines 147–157)

```csharp
public bool IsTotalOrder(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds)
{
    try
    {
        var (_, adj, inDeg, _) = BuildTopologicalGraph(relations, allEntryIds);
        return TryTopologicalSort(adj, inDeg, out _, checkUnique: true);
    }
    catch (InvalidOperationException) { return false; }
}
```

- **Original behavior**: self-loop → `return false`; branching → `return false`;
  cycle → `return false`.
- **New behavior**: self-loop → throw → caught → `return false`; branching →
  TryTopologicalSort returns false; cycle → TryTopologicalSort returns false.
- **Identical.** The `try/catch` preserves the original self-loop return value.

### 3.2 IsValidOrder (lines 159–170)

```csharp
public bool IsValidOrder(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds)
{
    try
    {
        var (_, adj, inDeg, _) = BuildTopologicalGraph(relations, allEntryIds);
        return TryTopologicalSort(adj, inDeg, out _);
    }
    catch (InvalidOperationException) { return false; }
}
```

- **Original behavior**: self-loop → `return false`; cycle → `return false`;
  disconnected DAG → `true`.
- **New behavior**: identical reasoning as above; `checkUnique` defaults to
  `false`, so disconnected components process normally.
- **Identical.**

### 3.3 GetSortedTiers (lines 173–210)

```csharp
public List<HashSet<string>> GetSortedTiers(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds)
{
    var (_, adj, inDeg, rootToMembers) = BuildTopologicalGraph(relations, allEntryIds);
    // ... own Kahn BFS with tier batching, no try/catch
}
```

- **Original behavior**: self-loop → `continue` (silent skip); tier grouping via
  BFS level batching.
- **New behavior**: self-loop → `throw InvalidOperationException` from
  `BuildTopologicalGraph` (no try/catch); tier grouping identical.
- **Behavioral change** for self-loop input: now propagates exception instead of
  continuing. This is the intended fix for ALGO-001. The normal call chain
  (`ValidatePartitionedGraph` → `IsValidOrder` → only then `GetSortedTiers`)
  ensures `GetSortedTiers` is never called with invalid input in production.
  Direct callers bypassing validation will now receive an exception (fail-fast)
  rather than silently wrong results.
- **Tier algorithm identical:** copies inDegree, batch-processes by
  `queue.Count`, expands equivalence classes via `rootToMembers[u]`, reverses
  result. Matches original line-for-line.

---

## 4. Edge Case Analysis

| Edge Case                          | IsTotalOrder | IsValidOrder | GetSortedTiers          |
| ---------------------------------- | ------------ | ------------ | ----------------------- |
| **Disconnected components**        | `false` ✓    | `true` ✓     | Both tiers merged ✓     |
| **Cycle (A>B, B>C, C>A)**          | `false` ✓    | `false` ✓    | Partial result* ✓       |
| **Single node**                    | `true` ✓     | `true` ✓     | `[{"A"}]` ✓             |
| **Empty input**                    | `true` ✓     | `true` ✓     | `[]` ✓                  |
| **All ties (A=B, B=C)**            | `true` ✓     | `true` ✓     | Single tier ✓           |
| **Self-loop**                      | `false` ✓    | `false` ✓    | **throws** (was silent) |
| **No relations, multiple entries** | `false` ✓    | `true` ✓     | Single tier ✓           |

*For cycles: `GetSortedTiers` produces incomplete output (nodes in the cycle are
never dequeued). This is acceptable because validation rejects cycles first.

**Self-loop**: The exception from `GetSortedTiers` is new. It replaces a
silently-wrong `continue`. Since production code always validates before
tiering, this is a defensive fail-fast. See RA-ALGO-001 below.

---

## 5. Logic Differences vs. Original

| Aspect                          | Original                          | Refactored                        | Verdict     |
| ------------------------------- | --------------------------------- | --------------------------------- | ----------- |
| UnionFind construction          | Per-method, duplicate             | Shared in `BuildTopologicalGraph` | Identical   |
| Adjacency/in-degree building    | Per-method, duplicate             | Shared in `BuildTopologicalGraph` | Identical   |
| Self-loop handling              | `return false` / `continue`       | `throw InvalidOperationException` | Unified     |
| `IsTotalOrder` self-loop result | `return false`                    | catch → `return false`            | Identical   |
| `IsValidOrder` self-loop result | `return false`                    | catch → `return false`            | Identical   |
| `GetSortedTiers` self-loop      | `continue` (silent wrong result)  | throw (no catch)                  | **Changed** |
| Kahn BFS (IsTotalOrder)         | inline                            | `TryTopologicalSort(checkUnique)` | Identical   |
| Kahn BFS (IsValidOrder)         | inline                            | `TryTopologicalSort`              | Identical   |
| Kahn BFS (GetSortedTiers)       | inline (mutates inDegree)         | inline copy of inDegree           | Identical   |
| rootToMembers building          | Only in GetSortedTiers            | Always in BuildTopologicalGraph   | Extra work  |
| inDegree mutation               | IsTotalOrder/IsValidOrder mutated | TryTopologicalSort copies         | Safer       |
| UnionFind class modifier        | `private class`                   | `private sealed class`            | No impact   |

The only behavioral change is `GetSortedTiers` no longer silently accepting
self-loops. This is the intended fix for ALGO-001.

---

## 6. Test Verification

All 10 tests in `ValidationServiceTests` pass
(`dotnet test --filter ValidationServiceTests`):

| Test                                                             | Result |
| ---------------------------------------------------------------- | ------ |
| `IsTotalOrder_ValidTotalOrder_ReturnsTrue`                       | Pass   |
| `IsTotalOrder_WithTies_ValidTotalOrder_ReturnsTrue`              | Pass   |
| `IsTotalOrder_Cycle_ReturnsFalse`                                | Pass   |
| `IsTotalOrder_DisconnectedBranches_ReturnsFalse`                 | Pass   |
| `IsValidOrder_DisconnectedBranches_ReturnsTrue`                  | Pass   |
| `IsValidOrder_Cycle_ReturnsFalse`                                | Pass   |
| `GetSortedTiers_GroupsIncomparableNodes`                         | Pass   |
| `ValidatePartitionedGraph_DisconnectedGraph_ShouldReturnInvalid` | Pass   |
| `ValidatePartitionedGraph_ConnectedGraph_ShouldReturnValid`      | Pass   |
| `ValidatePartitionedGraph_WithCycles_ShouldReturnInvalid`        | Pass   |

No test covers self-loop in `GetSortedTiers` (unsurprising — the original would
have silently passed such a test, producing wrong output). No regression vs. the
8 validation tests cited in the remediation report.

---

## 7. New Findings

### RA-ALGO-001 — `GetSortedTiers` exception asymmetry (low, consistency)

`IsTotalOrder` and `IsValidOrder` wrap `BuildTopologicalGraph` in
`try/catch (InvalidOperationException)` to gracefully return `false` for
self-loop input. `GetSortedTiers` does **not** catch — the
`InvalidOperationException` propagates to the caller.

**Impact**: A direct caller of `GetSortedTiers` that passes invalid input
(self-loop relation) will get an unhandled exception rather than a graceful
result. In the normal production flow (`ValidatePartitionedGraph` validates
before any tier computation), this never occurs. But the API contract
(`IValidationService`) doesn't document that `GetSortedTiers` may throw on
invalid input — unlike `IsTotalOrder`/`IsValidOrder` which are documented to
return `bool`.

**Recommendation**: Either:

1. Add `try/catch (InvalidOperationException)` in `GetSortedTiers` and return an
   empty list (or `null`), matching the defensive posture of the other public
   methods.
2. Document on `IValidationService.GetSortedTiers` that callers must ensure
   input validity (or call `ValidatePartitionedGraph` first).

### RA-ALGO-002 — Unconditional `rootToMembers` construction (low, performance)

`BuildTopologicalGraph` always constructs `rootToMembers`, even when called by
`IsTotalOrder`/`IsValidOrder` (which discard it via `_`). The original code only
built `rootToMembers` in `GetSortedTiers`.

**Cost**: O(n) extra iterations and hash set allocations, where n = number of
entries. For expected contest sizes (< 1000 entries), negligible. For very large
datasets, measurable but still dwarfed by Kahn's BFS.

**Recommendation**: Leave as-is for simplicity, or parameterize
`BuildTopologicalGraph` with a `bool includeMembers` flag to skip
`rootToMembers` construction when unneeded.

### RA-ALGO-003 — ALGO-003 (UnionFind union-by-rank) not remediated (low, performance)

The original finding ALGO-003 recommended adding `_rank` field and union-by-rank
to `UnionFind` for O(α(n)) amortized complexity. This was not addressed in the
current remediation wave.

**Status**: Remains open. DFD (defer-for-delivery) — the practical impact is
negligible for expected input sizes.

---

## 8. Remediation Status

| Original Finding | Status       | Notes                                                                                                                       |
| ---------------- | ------------ | --------------------------------------------------------------------------------------------------------------------------- |
| ALGO-001         | **Resolved** | Self-loop now throws; `IsTotalOrder`/`IsValidOrder` catch and return false. `GetSortedTiers` propagates (see RA-ALGO-001).  |
| ALGO-002         | **Resolved** | Shared `BuildTopologicalGraph` + `TryTopologicalSort`; 3 thin wrappers. All three methods function identically to original. |
| ALGO-003         | **Open**     | UnionFind still lacks union-by-rank.                                                                                        |
| ALGO-004         | **Open**     | BradleyTerry division-by-zero — out of scope for this re-audit.                                                             |
| ALGO-005         | **Open**     | Partition bridge node count — out of scope for this re-audit.                                                               |

---

## 9. Overall Assessment

The refactoring correctly implements the remediation plan. No algorithmic
regressions were found. The shared methods produce results identical to the
original inline implementations for all tested edge cases. Three low-severity
observations are documented above; none block the remediation.

**Algorithm correctness verdict: PASS.**
