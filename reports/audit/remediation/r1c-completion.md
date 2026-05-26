# R1-C Remediation: CQ-002 + ALGO-001 + CQ-010 + CQ-006

**Agent:** R1-C | **Date:** 2026-05-26 | **Status:** Complete

---

## Findings Addressed

| Finding  | Severity | Description                                                                                | Resolution                                                               |
| -------- | -------- | ------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------ |
| CQ-002   | HIGH     | 260 lines of duplicated Kahn's algorithm across 3 methods                                  | Extracted `BuildTopologicalGraph` + `TryTopologicalSort`                 |
| ALGO-001 | HIGH     | `GetSortedTiers` silently skips self-loops (`if (u == v) continue`) while other two reject | Now throws `InvalidOperationException` in shared `BuildTopologicalGraph` |
| CQ-010   | LOW      | `UnionFind` class not sealed                                                               | Added `sealed` keyword                                                   |
| CQ-006   | MEDIUM   | Methods exceed 50-line soft limit                                                          | All three wrappers now under 38 lines                                    |

---

## What Was Extracted

### `BuildTopologicalGraph` (private static, 69 lines)

Builds UnionFind (processing `EqualTo` equivalence classes), constructs the
adjacency list and in-degree map from `GreaterThan`/`LessThan` relations, and
returns `rootToMembers` for tier expansion. Self-loops (`u == v`) now throw
`InvalidOperationException` instead of being silently skipped.

### `TryTopologicalSort` (private static, 32 lines)

Runs Kahn's BFS on a working copy of in-degree. Accepts `checkUnique` flag:

- `checkUnique: true` — returns `false` at first point where `queue.Count > 1`
  (non-unique order)
- `checkUnique: false` — standard topological sort; returns `false` only on
  cycle

### Wrappers

| Method           | Before (lines) | After (lines) | Logic                                                                                                                              |
| ---------------- | -------------- | ------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| `IsTotalOrder`   | 85             | 12            | Calls `BuildTopologicalGraph` → `TryTopologicalSort(checkUnique: true)`. Catches `InvalidOperationException` → returns `false`.    |
| `IsValidOrder`   | 84             | 12            | Calls `BuildTopologicalGraph` → `TryTopologicalSort(checkUnique: false)`. Catches `InvalidOperationException` → returns `false`.   |
| `GetSortedTiers` | 97             | 38            | Calls `BuildTopologicalGraph` → inline tier-batching Kahn's with `rootToMembers` expansion. Self-loop now throws (was `continue`). |

### File-level change

- Original: 345 lines
- After: 245 lines
- Net reduction: **100 lines (29%)**

---

## Build Result

```
dotnet build src/ContestJudging.Services/ContestJudging.Services.csproj --configuration Release
```

**Passed** — 0 Warnings, 0 Errors.

---

## Test Result

```
dotnet test --filter "FullyQualifiedName~ValidationServiceTests"
```

**Passed** — 10/10 tests, 0 failures, 0 skipped:

1. `IsTotalOrder_ValidTotalOrder_ReturnsTrue`
2. `IsTotalOrder_WithTies_ValidTotalOrder_ReturnsTrue`
3. `IsTotalOrder_Cycle_ReturnsFalse`
4. `IsTotalOrder_DisconnectedBranches_ReturnsFalse`
5. `IsValidOrder_DisconnectedBranches_ReturnsTrue`
6. `IsValidOrder_Cycle_ReturnsFalse`
7. `GetSortedTiers_GroupsIncomparableNodes`
8. `ValidatePartitionedGraph_DisconnectedGraph_ShouldReturnInvalid`
9. `ValidatePartitionedGraph_ConnectedGraph_ShouldReturnValid`
10. `ValidatePartitionedGraph_WithCycles_ShouldReturnInvalid`

---

## Issues Encountered

None. All existing tests continue to pass identically. The self-loop change
(from `continue` to `throw`) is a behavioral fix for `GetSortedTiers` that
aligns with the rejection behavior of `IsTotalOrder`/`IsValidOrder` — none of
the 10 tests exercise self-loop paths, so no regressions.
