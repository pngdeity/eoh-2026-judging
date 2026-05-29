# R3-B Remediation Completion Report

**Agent:** R3-B (Test Quality) **Pass:** 3 **Branch:** fix/audit-remediate

## Test Count Summary

| Layer                          | Before | After  | Delta   |
| ------------------------------ | ------ | ------ | ------- |
| ContestJudging.Tests           | 40     | 51     | +11     |
| ContestJudging.Web.Tests (new) | 0      | 8      | +8      |
| **Total**                      | **40** | **59** | **+19** |

## Changes by Finding

### TE-001: PartitionService Non-Deterministic Tests (HIGH)

- **File:** `src/ContestJudging.Services/Partitioning/PartitionService.cs`
  - Added seeded `Random` constructor overload:
    `public PartitionService(Random random)`
  - Default constructor chains to seeded:
    `public PartitionService() : this(new Random()) { }`
- **File:** `tests/ContestJudging.Tests/PartitionServiceTests.cs`
  - Updated both tests to use `new PartitionService(new Random(42))`
  - Added `using System;`

### TE-004: BradleyTerry Convergence Edge Cases (HIGH)

- **File:** `tests/ContestJudging.Tests/ResolutionServiceTests.cs`
  - `ResolveGlobalStrengths_LinearOrder_ConvergesWithCorrectOrder` — 20-entry
    linear chain, validates transitive strength ordering
  - `ResolveGlobalStrengths_EmptyInput_ReturnsEmpty` — empty relations/IDs edge
    case
  - `ResolveGlobalStrengths_SingleEntry_ReturnsLogZero` — single entry with no
    relations
  - Added `using System;` and `using System.Linq;`

### TE-005: CalculateScoresFromStrengths Across All Strategies (HIGH)

- **File:** `tests/ContestJudging.Tests/ScoringStrategyTests.cs`
  - `LinearSpacing_CalculateScoresFromStrengths_VariedStrengths_ReturnsScaledScores`
  - `LinearSpacing_CalculateScoresFromStrengths_AllSameStrength_AllGetMaxScore`
  - `LinearSpacing_CalculateScoresFromStrengths_SingleEntry_GetsMaxScore`
  - `Percentile_CalculateScoresFromStrengths_RanksByStrengthPercentile`
  - `DefinedInterval_CalculateScoresFromStrengths_LinearScalingFallback`
  - All tests match actual method signature:
    `(Dictionary<string, double> globalStrengths, double maxScore)`

### TEST-001: Parameterized Tests (HIGH)

- **File:** `tests/ContestJudging.Tests/CoreTests.cs`
  - `Category_Constructor_ThrowsWhenMaxScoreIsOneOrLess`: Fact → Theory with
    `[InlineData(1)]`, `[InlineData(0)]`, `[InlineData(-1)]`
  - `Entry_SetScore_InvalidScore_Throws`: Fact → Theory with `[InlineData(11)]`,
    `[InlineData(-1)]`
  - No other test files had sequential `Assert.Throws` in Facts — no additional
    conversions needed

### TEST-002: Web Project Tests (HIGH)

- **Created:** `tests/ContestJudging.Web.Tests/ContestJudging.Web.Tests.csproj`
  (Microsoft.NET.Sdk.Razor, net10.0)
- **Created:** `tests/ContestJudging.Web.Tests/ModelValidationTests.cs` (8 test
  cases)
  - CategoryModel validation: valid values, empty ID, max score below minimum
    (Theory×3)
  - EntryModel validation: valid values, empty ID
  - LeaderboardItem: stores entry correctly
- **Modified:** `Directory.Packages.props` — added `bunit` 1.38.5
- **Modified:** `ContestJudging.slnx` — added Web.Tests project to /tests/
  folder

#### Web Test Surface Analysis

The Web project (Blazor WASM) has mostly private code-behind methods
(`FindSuggestedPair`, `RecordResult`, `AddRelation`, `GetOpText`, etc.). The
only public testable surfaces are the nested model classes
(`Setup.CategoryModel`, `Setup.EntryModel`, `Results.LeaderboardItem`) and their
DataAnnotations validation. Tests focus on these public APIs. For future
improvement, consider extracting core logic (`FindSuggestedPair`, partitioning
integration) to injectable services to enable direct unit testing.

## Build Results

| Project                        | Status                                                                                                       |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------ |
| ContestJudging.Tests           | 0 errors, 51 passed                                                                                          |
| ContestJudging.Web.Tests       | 0 errors, 8 passed, 10 IL2026 trim warnings (expected — Validator.TryValidateObject)                         |
| ContestJudging.slnx full build | Web project fails on `ManagedToNativeGenerator` (pre-existing WASM AOT issue on Linux, unrelated to changes) |
