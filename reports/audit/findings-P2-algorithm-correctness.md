# P2-B Algorithm Correctness Report

**Agent**: P2-B **Pass**: 2 **Domain**: algorithm-correctness **Status**:
success

## Scope

Deep analysis of every algorithm in the judging engine's mathematics pipeline as
described in the README:

1. Cycle Detection — Kahn's Algorithm for topological sort
2. Tie Handling — Union-Find for equivalence classes
3. Score Calculation — Linear Spacing and Percentile Ranking
4. Partition Planning — overlapping groups with statistical connectivity

Files audited: 13 source files in `src/ContestJudging.Core/` and
`src/ContestJudging.Services/`, plus 4 test files.

## Overall Correctness Verdict

**The core algorithms are correctly implemented and produce mathematically sound
results.** Kahn's algorithm, Union-Find with path compression, Linear Spacing,
Percentile Scoring, and the Bradley-Terry MLE iteration are all textbook-correct
for valid inputs. The algorithms correctly handle standard edge cases including
cycles, disconnected graphs, empty input, single node, all-tied entries, and
degenerate partition configurations.

Five findings were identified: two medium-severity algorithmic issues
(GetSortedTiers silently ignoring contradictions, and 100% duplicated topology
logic across three methods), plus three low-severity issues (UnionFind missing
union-by-rank, theoretical division-by-zero in Bradley-Terry, and partition
bridge count hitting zero).

---

## Finding: ALGO-001

**Severity**: medium **Category**: correctness-defense-in-depth

### GetSortedTiers silently skips contradictory self-loop edges

`src/ContestJudging.Services/Validation/GraphValidationService.cs:264`

In `IsTotalOrder` (line 90) and `IsValidOrder` (line 176), a self-loop after
Union-Find resolution — i.e., two entries found to be equivalent via EqualTo
chains but also related by a directional operator — triggers an immediate
`return false`, correctly identifying a contradiction.

In `GetSortedTiers` (line 264), the same condition uses `continue` instead,
silently discarding the contradictory relation. This means `GetSortedTiers` will
produce tiers for an invalid graph that both `IsTotalOrder` and `IsValidOrder`
would reject. If a caller invokes `GetSortedTiers` without first validating the
graph, the output tiers would be mathematically meaningless.

```csharp
// IsTotalOrder line 90, IsValidOrder line 176:
if (u == v) return false;

// GetSortedTiers line 264:
if (u == v) continue;
```

**Remediation**: Change line 264 to
`if (u == v) throw new InvalidOperationException(...)` or `return false`
(changing the method's signature to report validity). At minimum, unify behavior
with the other two methods.

---

## Finding: ALGO-002

**Severity**: medium **Category**: duplication

### 100% duplicated Kahn's Algorithm logic across three methods

`src/ContestJudging.Services/Validation/GraphValidationService.cs:43-127,129-212,214-310`

`IsTotalOrder`, `IsValidOrder`, and `GetSortedTiers` each independently perform:

- Union-Find construction
- Equality relation processing
- Adjacency-list and in-degree initialization
- Edge construction from GreaterThan/LessThan relations
- Kahn's BFS traversal

The only behavioral difference between `IsTotalOrder` and `IsValidOrder` is a
single check (`if (queue.Count > 1) return false;` on line 109).
`GetSortedTiers` differs in batch processing and self-loop handling. The three
methods share ~85 lines of identical algorithm logic each.

**Why this is a correctness risk**: If a bug is discovered in the
edge-construction or degree-initialization logic, it must be fixed in three
separate places. Divergent fixes are a real risk.

**Remediation**: Extract a private method (e.g., `TryBuildTopologicalOrder`)
that returns the topological order list or null on cycle. `IsValidOrder` wraps
it checking for non-null. `IsTotalOrder` additionally verifies the order is
unique. `GetSortedTiers` groups the order into tiers by in-degree-zero batches.

**Cross reference**: CQ-002 from P1-B

---

## Finding: ALGO-003

**Severity**: low **Category**: performance-algorithm

### UnionFind lacks union-by-rank/size

`src/ContestJudging.Services/Validation/GraphValidationService.cs:32-39`

The `Union` method always attaches `rootI` under `rootJ` without considering
subtree size or rank:

```csharp
public void Union(string i, string j)
{
    string rootI = Find(i);
    string rootJ = Find(j);
    if (rootI != rootJ)
    {
        _parent[rootI] = rootJ;
    }
}
```

Without union-by-rank, an adversarial sequence of unions can create a depth-O(n)
tree. With path compression, amortized complexity is still O(log n) (not the
optimal O(alpha(n)) with both optimizations). For contest-scale data (hundreds
of entries), this has no practical impact, but it falls short of textbook
optimality.

**Remediation**: Track a rank/int dictionary and attach the smaller tree under
the larger.

---

## Finding: ALGO-004

**Severity**: low **Category**: correctness-edge-case

### Theoretical division-by-zero in BradleyTerry iteration

`src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:69`

The inner loop computes:

```csharp
denominator += totalComparisons[i, j] / (gamma[i] + gamma[j]);
```

If `gamma[i]` and `gamma[j]` are both exactly `0.0`, this produces `NaN`. An
entry's gamma can become exactly `0.0` when it has `totalWins[i] == 0` and
participates in comparisons where the opponent has non-zero gamma. In valid
comparison data, two entries both with gamma = 0 cannot exist while also being
compared to each other (a comparison implies one wins, hence non-zero gamma).
However, crafted or corrupted input data could trigger this.

**Remediation**: Add a guard: `if (gamma[i] + gamma[j] < 1e-12) continue;`

---

## Finding: ALGO-005

**Severity**: low **Category**: correctness-edge-case

### Partition bridge node count can be zero

`src/ContestJudging.Services/Partitioning/PartitionService.cs:21`

```csharp
int bCount = (int)Math.Round(n * overlapRate);
```

`Math.Round` with `overlapRate` small relative to `n` can produce `bCount = 0`.
For example, `n=50`, `overlapRate=0.005` → `Round(0.25) = 0`. With zero bridge
nodes, partitions have no overlap, defeating the "statistical connectivity"
guarantee described in the README. The downstream `ValidatePartitionedGraph`
will correctly detect the resulting disconnected graph, but the user experience
degrades (no valid partitioning possible).

**Remediation**: Enforce a minimum of 1 bridge node when `kPartitions > 1` and
`n > 0`, or validate that `bCount >= kPartitions - 1`.

---

## Detailed Algorithm Audits

### Kahn's Algorithm

- **Correctness**: Textbook implementation. Uses in-degree map, adjacency list
  with `HashSet<string>` for duplicate-edge prevention, and a BFS queue on
  zero-in-degree nodes.
- **Cycle detection**: Returns `false` when `processedNodes < inDegree.Count` —
  correct for both simple cycles and complex cycle structures.
- **Self-loop detection**: After Union-Find, checks `u == v` and returns `false`
  in `IsTotalOrder`/`IsValidOrder`.
- **Disconnected graphs**: Correctly processes all components independently.
  Each component's root gets in-degree 0 initially.
- **Single node**: `inDegree` has 1 entry with value 0; processed correctly;
  returns `true`.
- **Empty input**: `inDegree.Count == 0`, `processedNodes == 0`; returns `true`.
- **Off-by-one**: No array sizing issues — all structures are dynamically sized
  dictionaries/hashsets.
- **Memory complexity**: O(|V| + |E|) where V = equivalence classes, E =
  relations. No unbounded growth.
- **Total order uniqueness check**: `if (queue.Count > 1) return false;` —
  correct for identifying non-total (partial) orders.
- **Edge direction**: `GreaterThan` → u=EntryA.root, v=EntryB.root (u beats v).
  `LessThan` → swapped. Correct.

### Union-Find

- **Path compression**: Implemented correctly in `Find` via
  `_parent[i] = Find(_parent[i])`.
- **Union**: Correctly merges at the root level.
- **Self-union**: No-op (root equality check prevents action).
- **Chained unions**: Works correctly; path compression on subsequent `Find`
  calls keeps tree flat.
- **Union after find**: Correct — paths are compressed before root comparison.
- **Absent union-by-rank**: See ALGO-003.

### Score Calculation

- **Linear Spacing** (`LinearSpacingScoring.CalculateScores`): Formula
  `(i / (k-1)) * maxScore` is correct. Worst tier gets 0, best gets `maxScore`.
  Single tier (k=1) returns maxScore for all entries. Empty input returns empty
  dictionary. No division by zero (k=0 handled; k=1 special-cased).
- **Percentile Ranking** (`PercentileScoring.CalculateScores`): Formula
  `(beatenOpponents / (totalEntries-1)) * maxScore` correctly implements
  "percentage of exhibits outperformed." All-ties case
  (`sortedTiers.Count == 1`) returns maxScore for all. Single-entry case handled
  separately.
- **DefinedInterval** (`DefinedIntervalScoring.CalculateScores`): Direct mapping
  from tier index to predefined rank points. Excess tiers get score 0.
- **Strengths-to-scores** methods: All three strategies implement a linear
  min-max normalization of strengths onto [0, maxScore]. `range < 1e-9` guard
  prevents division by zero.
- **Precision**: `Math.Round(score, 2)` limits precision to 2 decimal places.
  Appropriate for typical maxScore values (e.g., 100, 10). With maxScore=1.0 and
  many tiers, rounding could collapse distinct scores — minor concern.

### Bradley-Terry MLE Resolution

- **Algorithm**: Jacobi-style iterative MLE estimation. Correct implementation
  of: `gamma_i = w_i / Σ_{j≠i} (n_ij / (gamma_i + gamma_j))`
- **Normalization**: Scales gamma to sum-to-1 each iteration.
- **Convergence**: Max 1000 iterations with `maxDiff < 1e-6` threshold. Correct.
- **Rank-stability early exit**: After iteration 50, checks if ranking order
  stabilizes every 10 iterations. Sound optimization, though rank could
  stabilize before gamma converges fully (acceptable tradeoff at `1e-3`
  secondary threshold).
- **Empty relations**: All gamma stay at 1.0; results are `log(1.0) = 0` for all
  entries.
- **Log-strength output**: `Math.Log(gamma[i])` — higher is better, standard
  Bradley-Terry convention.
- **Division-by-zero risk**: See ALGO-004.

### Partition Planning

- **Algorithm**: Randomly selects `Round(n * overlapRate)` bridge nodes (present
  in all partitions), then distributes remaining nodes round-robin.
- **Connectivity guarantee**: Holds when `bCount >= 1` and every partition
  receives at least one unique node. Fails for zero bridge nodes (ALGO-005).
- **Distribution**: Round-robin ensures partitions differ in size by at most 1.
- **Randomization**: `OrderBy(x => _random.Next())` is an approximate shuffle,
  not cryptographically secure but adequate for judging.
- **Degenerate cases**: `kPartitions=1` correctly produces a single partition
  with all nodes. `n=0` produces empty partitions. `n=1, kPartitions>1` produces
  one non-empty and `k-1` empty partitions (correct but potentially surprising).

---

## Cross-Reference: CQ-002 / Duplicated Methods

The finding CQ-002 from P1-B identified that `IsTotalOrder` and `IsValidOrder`
are ~99% identical. My analysis confirms this and extends it: `GetSortedTiers`
also shares the same core topology logic. The duplication is a real correctness
risk — not because the methods produce wrong results currently, but because any
future bug fix in the shared topology code would need to be applied to three
locations, and a missed spot could produce inconsistent behavior.

See ALGO-002 for the algorithmic perspective on this issue.

---

## Metrics

| Metric                | Value  |
| --------------------- | ------ |
| Files scanned         | 17     |
| Findings count        | 5      |
| Lines of code scanned | ~1,500 |
