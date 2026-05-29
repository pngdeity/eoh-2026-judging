# Contest Judging System — Master Audit Report

**Synthesis Agent:** P3-A | **Pass:** 3 | **Date:** 2026-05-25

**Total Raw Findings (10 agents):** 103
**After Deduplication:** 81 unique findings (22 merged away in 15 groups)

---

## Severity Distribution

| Severity      | Count |
| ------------- | ----- |
| critical | 1 |
| high | 14 |
| medium | 32 |
| low | 27 |
| informational | 7 |

---

## Domain Breakdown

| Domain               | Agent | Raw | After Merge |
| -------------------- | ----- | --- | ----------- |
| algorithm-correctness | P2-B | 5 | 5 |
| architecture | P2-A | 8 | 7 |
| blazor-wasm | P2-C | 10 | 7 |
| cicd | P1-E | 11 | 11 |
| code-quality | P1-B | 10 | 7 |
| efcore | P2-D | 12 | 11 |
| security | P1-D | 8 | 6 |
| structure | P1-A | 10 | 5 |
| test-effectiveness | P2-E | 16 | 13 |
| tests | P1-C | 13 | 9 |

---

## Most Frequently Cited Problematic Files

| File | Citations |
| ---- | --------- |
| `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs` | 25 |
| `src/ContestJudging.Services/Validation/GraphValidationService.cs` | 18 |
| `src/ContestJudging.Web/Program.cs` | 17 |
| `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs` | 16 |
| `src/ContestJudging.Web/Pages/Setup.razor.cs` | 16 |
| `src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs` | 12 |
| `src/ContestJudging.Web/Pages/Judging.razor.cs` | 12 |
| `.github/workflows/pipeline.yml` | 12 |
| `src/ContestJudging.Services/Managers/ContestManager.cs` | 10 |
| `src/ContestJudging.Web/ContestJudging.Web.csproj` | 7 |

---

## Top 5 Most Severe Findings

### 1. STRUCT-003 [critical] — E2E tests reference packages not declared in Directory.Packages.props (central package management violation)

**Domain:** structure | **Agent:** P1-A | **Category:** architecture

<!-- E2ETests.csproj lines 15-17 -->
<PackageReference Include="NUnit" />
<PackageReference Include="NUnit.Analyzers" />
<PackageReference Include="NUnit3TestAdapter" />
<PackageReference Include="Microsoft.Playwright.NUnit" />

<!-- Directory.Packages.props has no matching <PackageVersion> for any of these -->

**Rule Violated:** Central Package Management: all PackageReference items must have a corresponding PackageVersion in Directory.Packages.props when ManagePackageVersionsCentrally is true

**Remediation:** Add <PackageVersion> entries for NUnit, NUnit.Analyzers, NUnit3TestAdapter, and Microsoft.Playwright.NUnit to Directory.Packages.props. Example: <PackageVersion Include="NUnit" Version="4.3.2" />. This is build-breaking.

**Files:** tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj:15, tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj:16, tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj:17, Directory.Packages.props:1

**Related:** TEST-008

---

### 2. TEST-001 [high] — No parameterized tests (Theories) used — all tests use Fact with sequential Assert.Throws

**Domain:** tests | **Agent:** P1-C | **Category:** testing

[Fact]
public void Category_Constructor_ThrowsWhenMaxScoreIsOneOrLess()
{
    Assert.Throws<ArgumentOutOfRangeException>(() => new Category("cat1", 1));
    Assert.Throws<ArgumentOutOfRangeException>(() => new Category("cat1", 0));
}

**Rule Violated:** xUnit best practices: parameterized tests ([Theory]) over repeated Assert.Throws in a single [Fact]

**Remediation:** Convert boundary tests to [Theory] with [InlineData]. If the first assertion throws/fails, the second never executes, masking defects. Same for Entry_SetScore_InvalidScore_Throws (tests 11 and -1 in one Fact).

**Files:** tests/ContestJudging.Tests/CoreTests.cs:12, tests/ContestJudging.Tests/CoreTests.cs:36, tests/ContestJudging.Tests/ScoringStrategyTests.cs:12, tests/ContestJudging.Tests/ValidationServiceTests.cs:153, tests/ContestJudging.Tests/PartitionServiceTests.cs:37

---

### 3. TEST-002 [high] — ContestJudging.Web project has zero unit tests

**Domain:** tests | **Agent:** P1-C | **Category:** testing

// ContestJudging.Tests.csproj ProjectReference:
<ProjectReference Include="..\..\src\ContestJudging.Core\ContestJudging.Core.csproj" />
<ProjectReference Include="..\..\src\ContestJudging.Services\ContestJudging.Services.csproj" />
<ProjectReference Include="..\..\src\ContestJudging.Infrastructure\ContestJudging.Infrastructure.csproj" />
// NOTE: ContestJudging.Web is NOT referenced

**Rule Violated:** Testing pyramid: missing unit tests for UI/presentation layer logic

**Remediation:** Add bunit or similar Blazor testing library. Test pure-logic methods: FindSuggestedPair(), GetFilteredEntries(), GeneratePartitions(), CalculateResults(), keyboard handler, DI validation. Add ContestJudging.Web.Tests project.

**Files:** src/ContestJudging.Web/Program.cs:1, src/ContestJudging.Web/Pages/Setup.razor.cs:1, src/ContestJudging.Web/Pages/Judging.razor.cs:1, src/ContestJudging.Web/Pages/Results.razor.cs:1

---

### 4. CICD-001 [high] — E2E test project excluded from solution — Playwright/NUnit tests never run in CI

**Domain:** cicd | **Agent:** P1-E | **Category:** testing

<Folder Name="/tests/">
    <Project Path="tests/ContestJudging.Tests/ContestJudging.Tests.csproj" />
</Folder>

**Rule Violated:** CI completeness — all test projects must be exercised in the pipeline

**Remediation:** Add <Project Path="tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj" /> to the /tests/ folder in ContestJudging.slnx. Consider a separate CI job for E2E tests with Playwright browser installation.

**Files:** ContestJudging.slnx:8

**Related:** STRUCT-002

---

### 5. CICD-004 [high] — No SAST or CodeQL scanning — only dependency scanning is present

**Domain:** cicd | **Agent:** P1-E | **Category:** security

security-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - name: OSV-Scanner
        uses: google/osv-scanner-action/osv-scanner-action@v2.3.5

**Rule Violated:** OWASP / DevSecOps — SAST should complement dependency vulnerability scanning

**Remediation:** Add a CodeQL analysis job targeting csharp (and optionally javascript for Blazor WASM client). github/codeql-action provides free scanning for public repos.

**Files:** .github/workflows/pipeline.yml:52

---

## All Findings (Sorted by Severity)

1. **STRUCT-003** [critical] `structure` — E2E tests reference packages not declared in Directory.Packages.props (central package management violation)
   Files: tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj:15, tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj:16, tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj:17, Directory.Packages.props:1
   Related: TEST-008

2. **TEST-001** [high] `tests` — No parameterized tests (Theories) used — all tests use Fact with sequential Assert.Throws
   Files: tests/ContestJudging.Tests/CoreTests.cs:12, tests/ContestJudging.Tests/CoreTests.cs:36, tests/ContestJudging.Tests/ScoringStrategyTests.cs:12, tests/ContestJudging.Tests/ValidationServiceTests.cs:153, tests/ContestJudging.Tests/PartitionServiceTests.cs:37

3. **TEST-002** [high] `tests` — ContestJudging.Web project has zero unit tests
   Files: src/ContestJudging.Web/Program.cs:1, src/ContestJudging.Web/Pages/Setup.razor.cs:1, src/ContestJudging.Web/Pages/Judging.razor.cs:1, src/ContestJudging.Web/Pages/Results.razor.cs:1

4. **CICD-001** [high] `cicd` — E2E test project excluded from solution — Playwright/NUnit tests never run in CI
   Files: ContestJudging.slnx:8
   Related: STRUCT-002

5. **CICD-004** [high] `cicd` — No SAST or CodeQL scanning — only dependency scanning is present
   Files: .github/workflows/pipeline.yml:52

6. **ARCH-002** [high] `architecture` — ContestManager (Services/Application layer) directly depends on concrete ContestDbContext from Infrastructure
   Files: src/ContestJudging.Services/Managers/ContestManager.cs:9, src/ContestJudging.Services/Managers/ContestManager.cs:23, src/ContestJudging.Services/Managers/ContestManager.cs:32, tests/ContestJudging.Tests/ContestManagerTests.cs:30, tests/ContestJudging.Tests/ContestManagerTests.cs:64, tests/ContestJudging.Tests/ContestManagerTests.cs:101
   Related: ARCH-001, ARCH-004

7. **BW-004** [high] `blazor-wasm` — Bootstrap JavaScript not loaded in index.html — accordion UI in Judging page is non-functional
   Files: src/ContestJudging.Web/wwwroot/index.html:14, src/ContestJudging.Web/Pages/Judging.razor:97

8. **EF-001** [high] `efcore` — No database migration files; schema managed exclusively via EnsureCreatedAsync()
   Files: src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:53, src/ContestJudging.Web/Program.cs:56

9. **EF-002** [high] `efcore` — Missing foreign key relationships in entity configuration
   Files: src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:54

10. **EF-003** [high] `efcore` — Database restore overwrites file with active DbContext connection; swallowed exception masks data loss
   Files: src/ContestJudging.Web/Program.cs:35, src/ContestJudging.Web/Program.cs:47, src/ContestJudging.Web/Program.cs:49, src/ContestJudging.Web/Program.cs:56
   Related: BW-008, CQ-003, SEC-005

11. **TE-001** [high] `test-effectiveness` — PartitionService tests are non-deterministic due to unseeded Random
   Files: src/ContestJudging.Services/Partitioning/PartitionService.cs:9, tests/ContestJudging.Tests/PartitionServiceTests.cs:22, tests/ContestJudging.Tests/PartitionServiceTests.cs:31
   Related: TEST-001

12. **TE-002** [high] `test-effectiveness` — ContestManager ExportDataAsync/ImportDataAsync are structurally untestable due to concrete ContestDbContext dependency
   Files: src/ContestJudging.Services/Managers/ContestManager.cs:23, src/ContestJudging.Services/Managers/ContestManager.cs:119, src/ContestJudging.Services/Managers/ContestManager.cs:124, tests/ContestJudging.Tests/ContestManagerTests.cs:30, src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:66, src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:76
   Related: CQ-003, TE-010, TEST-004, TEST-010

13. **TE-003** [high] `test-effectiveness` — LocalStorage backup/restore pipeline has zero test coverage across all layers
   Files: src/ContestJudging.Web/Pages/Setup.razor.cs:49, src/ContestJudging.Web/Pages/Setup.razor.cs:55, src/ContestJudging.Web/Pages/Judging.razor.cs:76, src/ContestJudging.Web/Pages/Judging.razor.cs:82, src/ContestJudging.Web/Program.cs:39, src/ContestJudging.Web/Program.cs:46
   Related: CQ-002, CQ-003, TE-002

14. **TE-004** [high] `test-effectiveness` — BradleyTerryResolutionService convergence and early-exit paths completely untested
   Files: src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:56, src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:90, src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:108, src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:118, tests/ContestJudging.Tests/ResolutionServiceTests.cs:13, tests/ContestJudging.Tests/ResolutionServiceTests.cs:41
   Related: CQ-006, CQ-008

15. **TE-005** [high] `test-effectiveness` — CalculateScoresFromStrengths untested across all three scoring strategies
   Files: src/ContestJudging.Services/Scoring/LinearSpacingScoring.cs:38, src/ContestJudging.Services/Scoring/PercentileScoring.cs:40, src/ContestJudging.Services/Scoring/DefinedIntervalScoring.cs:38, tests/ContestJudging.Tests/ScoringStrategyTests.cs:13
   Related: CQ-008, TE-004

16. **STRUCT-009** [medium] `structure` — Orphaned testapp scaffold project outside solution
   Files: testapp/testapp.csproj:1, testapp/Program.cs:1

17. **CQ-004** [medium] `code-quality` — Widespread var usage violations against .editorconfig mandate
   Files: src/ContestJudging.Services/Validation/GraphValidationService.cs:62, src/ContestJudging.Services/Validation/GraphValidationService.cs:71, src/ContestJudging.Services/Validation/GraphValidationService.cs:105, src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:17, src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:59, src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:86, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:24, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:107, src/ContestJudging.Web/Pages/Setup.razor.cs:30, src/ContestJudging.Web/Pages/Setup.razor.cs:33, src/ContestJudging.Web/Pages/Judging.razor.cs:32, src/ContestJudging.Web/Pages/Judging.razor.cs:39

18. **CQ-005** [medium] `code-quality` — No sealed classes anywhere in the codebase
   Files: src/ContestJudging.Core/Entities/Category.cs:5, src/ContestJudging.Core/Entities/Entry.cs:7, src/ContestJudging.Core/Entities/Relation.cs:3, src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:12, src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:18, src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:24, src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:32, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:13, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:69, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:185, src/ContestJudging.Services/Validation/GraphValidationService.cs:8, src/ContestJudging.Services/Scoring/PercentileScoring.cs:9, src/ContestJudging.Services/Scoring/LinearSpacingScoring.cs:8, src/ContestJudging.Services/Scoring/DefinedIntervalScoring.cs:8, src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:9, src/ContestJudging.Services/Partitioning/PartitionService.cs:7, src/ContestJudging.Services/Managers/ContestManager.cs:15, src/ContestJudging.Web/Pages/Setup.razor.cs:136, src/ContestJudging.Web/Pages/Setup.razor.cs:144, src/ContestJudging.Web/Pages/Results.razor.cs:64

19. **CQ-006** [medium] `code-quality` — Large methods exceeding 50-line maintainability threshold
   Files: src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:14, src/ContestJudging.Services/Validation/GraphValidationService.cs:43, src/ContestJudging.Services/Validation/GraphValidationService.cs:129, src/ContestJudging.Services/Validation/GraphValidationService.cs:214

20. **TEST-003** [medium] `tests` — No test categories/traits to distinguish unit tests from integration tests
   Files: tests/ContestJudging.Tests/InfrastructureTests.cs:18

21. **TEST-005** [medium] `tests` — ServiceCollectionExtensions.AddContestJudgingServices has no test coverage
   Files: src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:21

22. **SEC-001** [medium] `security` — Missing Content-Security-Policy header in index.html
   Files: src/ContestJudging.Web/wwwroot/index.html:4

23. **SEC-002** [medium] `security` — Insecure random number generation using System.Random in PartitionService
   Files: src/ContestJudging.Services/Partitioning/PartitionService.cs:9, src/ContestJudging.Services/Partitioning/PartitionService.cs:24

24. **SEC-004** [medium] `security` — Database backup in localStorage lacks integrity verification
   Files: src/ContestJudging.Web/Pages/Judging.razor.cs:76, src/ContestJudging.Web/Pages/Setup.razor.cs:49, src/ContestJudging.Web/Program.cs:39
   Related: BW-007

25. **SEC-006** [medium] `security` — Missing .db and .sqlite patterns in .gitignore
   Files: .gitignore:1
   Related: STRUCT-007

26. **SEC-007** [medium] `security` — No authentication on administrative operations
   Files: src/ContestJudging.Web/Pages/Setup.razor.cs:59, src/ContestJudging.Web/Pages/Setup.razor.cs:68, src/ContestJudging.Web/Pages/Judging.razor.cs:147, src/ContestJudging.Web/Pages/Judging.razor.cs:180

27. **CICD-002** [medium] `cicd` — No code coverage collected in CI despite coverlet.collector being configured
   Files: .github/workflows/pipeline.yml:50

28. **CICD-003** [medium] `cicd` — Six of seven GitHub Actions use floating major-version tags (supply chain risk)
   Files: .github/workflows/pipeline.yml:18, .github/workflows/pipeline.yml:21, .github/workflows/pipeline.yml:27, .github/workflows/pipeline.yml:73, .github/workflows/pipeline.yml:79, .github/workflows/pipeline.yml:100, .github/workflows/pipeline.yml:103, .github/workflows/pipeline.yml:108

29. **CICD-006** [medium] `cicd` — release/ directory (published Blazor WASM output) is committed to the repository
   Files: release/web.config:1

30. **CICD-007** [medium] `cicd` — No Dependabot configuration for automated dependency updates
   Files: .github/dependabot.yml:0

31. **CICD-011** [medium] `cicd` — Deploy job redundantly rebuilds from source instead of reusing tested artifacts
   Files: .github/workflows/pipeline.yml:62

32. **ARCH-001** [medium] `architecture` — Web project directly references Infrastructure layer — SQLitePCL init and ContestDbContext resolved in UI startup
   Files: src/ContestJudging.Web/Program.cs:5, src/ContestJudging.Web/Program.cs:21, src/ContestJudging.Web/Program.cs:34, src/ContestJudging.Web/ContestJudging.Web.csproj:29, src/ContestJudging.Web/ContestJudging.Web.csproj:30, src/ContestJudging.Web/ContestJudging.Web.csproj:36
   Related: ARCH-002, ARCH-005, BW-010, STRUCT-001

33. **ARCH-003** [medium] `architecture` — Service interfaces (IValidationService, IPartitionService, IGlobalRankingService, IContestManager) defined in Services project instead of Core
   Files: src/ContestJudging.Services/Validation/IValidationService.cs:1, src/ContestJudging.Services/Partitioning/IPartitionService.cs:1, src/ContestJudging.Services/Resolution/IGlobalRankingService.cs:1, src/ContestJudging.Services/Managers/IContestManager.cs:1, src/ContestJudging.Web/Pages/Setup.razor.cs:11, src/ContestJudging.Web/Pages/Judging.razor.cs:11, src/ContestJudging.Web/Pages/Results.razor.cs:3
   Related: ARCH-008

34. **ARCH-004** [medium] `architecture` — ContestDbContext violates Single Responsibility — mixes ORM configuration with raw file I/O export/import
   Files: src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:66, src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:76
   Related: ARCH-002, EF-011

35. **ALGO-001** [medium] `algorithm-correctness` — GetSortedTiers silently ignores contradictory self-loop edges unlike IsTotalOrder/IsValidOrder
   Files: src/ContestJudging.Services/Validation/GraphValidationService.cs:264, src/ContestJudging.Services/Validation/GraphValidationService.cs:90, src/ContestJudging.Services/Validation/GraphValidationService.cs:176
   Related: CQ-002

36. **ALGO-002** [medium] `algorithm-correctness` — Three methods contain 100% duplicated Kahn's Algorithm logic with only minor behavioral differences
   Files: src/ContestJudging.Services/Validation/GraphValidationService.cs:43, src/ContestJudging.Services/Validation/GraphValidationService.cs:129, src/ContestJudging.Services/Validation/GraphValidationService.cs:214
   Related: ALGO-001, CQ-002, TE-014

37. **BW-001** [medium] `blazor-wasm` — OnInitializedAsync methods lack exception handling in all three page components
   Files: src/ContestJudging.Web/Pages/Setup.razor.cs:37, src/ContestJudging.Web/Pages/Judging.razor.cs:44, src/ContestJudging.Web/Pages/Results.razor.cs:20

38. **BW-002** [medium] `blazor-wasm` — BackupDatabase invoked on every Setup page load and every mutation, causing excessive localStorage writes
   Files: src/ContestJudging.Web/Pages/Setup.razor.cs:42, src/ContestJudging.Web/Pages/Setup.razor.cs:49, src/ContestJudging.Web/Pages/Setup.razor.cs:55

39. **BW-003** [medium] `blazor-wasm` — No localStorage quota check before storing multi-MB SQLite backup
   Files: src/ContestJudging.Web/Pages/Setup.razor.cs:55, src/ContestJudging.Web/Pages/Judging.razor.cs:82

40. **BW-005** [medium] `blazor-wasm` — Interactive div elements missing ARIA roles, keyboard handlers, and tabindex
   Files: src/ContestJudging.Web/Pages/Judging.razor:6, src/ContestJudging.Web/Pages/Judging.razor:66, src/ContestJudging.Web/Pages/Judging.razor:74, src/ContestJudging.Web/Pages/Judging.razor:84, src/ContestJudging.Web/Layout/NavMenu.razor:4

41. **BW-006** [medium] `blazor-wasm` — Large lists rendered without Virtualize component
   Files: src/ContestJudging.Web/Pages/Setup.razor:35, src/ContestJudging.Web/Pages/Setup.razor:67, src/ContestJudging.Web/Pages/Judging.razor:162, src/ContestJudging.Web/Pages/Results.razor:55

42. **EF-004** [medium] `efcore` — O(n*m) client-side join in SqliteEntryRepository queries
   Files: src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:87, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:91, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:107, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:115
   Related: CQ-007

43. **EF-005** [medium] `efcore` — Missing .AsNoTracking() on all read-only repository queries
   Files: src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:30, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:80, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:103, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:200

44. **EF-006** [medium] `efcore` — CalculateGlobalScoresAsync performs individual entry updates without transaction wrapping
   Files: src/ContestJudging.Services/Managers/ContestManager.cs:107, src/ContestJudging.Services/Managers/ContestManager.cs:112

45. **TE-006** [medium] `test-effectiveness` — Repository UpdateAsync and GetAllAsync methods have zero test coverage
   Files: src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:41, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:28, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:101, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:143, tests/ContestJudging.Tests/InfrastructureTests.cs:35
   Related: CQ-007, TE-009, TEST-009

46. **TE-007** [medium] `test-effectiveness` — ValidatePartitionedGraph tests fail to assert error message content
   Files: tests/ContestJudging.Tests/ValidationServiceTests.cs:175, tests/ContestJudging.Tests/ValidationServiceTests.cs:201, tests/ContestJudging.Tests/ValidationServiceTests.cs:228, src/ContestJudging.Services/Validation/GraphValidationService.cs:312

47. **TE-008** [medium] `test-effectiveness` — LessThan operator never tested in IsTotalOrder or IsValidOrder
   Files: src/ContestJudging.Services/Validation/GraphValidationService.cs:80, src/ContestJudging.Services/Validation/GraphValidationService.cs:166, tests/ContestJudging.Tests/ValidationServiceTests.cs:15, tests/ContestJudging.Tests/ValidationServiceTests.cs:89

48. **STRUCT-004** [low] `structure` — E2ETests project missing explicit RootNamespace and AssemblyName properties
   Files: tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj:1

49. **STRUCT-008** [low] `structure` — Redundant TargetFramework/Nullable/ImplicitUsings overrides in Web.csproj
   Files: src/ContestJudging.Web/ContestJudging.Web.csproj:9, src/ContestJudging.Web/ContestJudging.Web.csproj:10, src/ContestJudging.Web/ContestJudging.Web.csproj:11, Directory.Build.props:3, Directory.Build.props:4, Directory.Build.props:5

50. **CQ-001** [low] `code-quality` — Dead empty class Class1.cs in Infrastructure project
   Files: src/ContestJudging.Infrastructure/Class1.cs:1
   Related: STRUCT-005, TEST-011

51. **CQ-008** [low] `code-quality` — Magic numbers without named constants
   Files: src/ContestJudging.Services/Scoring/LinearSpacingScoring.cs:57, src/ContestJudging.Services/Scoring/DefinedIntervalScoring.cs:50, src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:90, src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:108

52. **CQ-009** [low] `code-quality` — Tuple<string, string> should be ValueTuple (string, string)
   Files: src/ContestJudging.Web/Pages/Judging.razor.cs:36

53. **CQ-010** [low] `code-quality` — Missing sealed on private nested UnionFind class
   Files: src/ContestJudging.Services/Validation/GraphValidationService.cs:10

54. **TEST-006** [low] `tests` — Vague test class name 'CoreTests' — unclear what it tests
   Files: tests/ContestJudging.Tests/CoreTests.cs:9

55. **TEST-007** [low] `tests` — E2E tests use NUnit while unit tests use xUnit — framework inconsistency
   Files: tests/ContestJudging.E2ETests/AppE2ETests.cs:3, tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj:16
   Related: STRUCT-006

56. **TEST-010** [low] `tests` — ContestManager ExportDataAsync/ImportDataAsync untested
   Files: src/ContestJudging.Services/Managers/ContestManager.cs:119, tests/ContestJudging.Tests/ContestManagerTests.cs:30

57. **CICD-005** [low] `cicd` — OSV-Scanner uses --allow-no-lockfiles reducing scan accuracy
   Files: .github/workflows/pipeline.yml:60

58. **CICD-008** [low] `cicd` — No CODEOWNERS file for automated PR review assignment
   Files: .github/CODEOWNERS:0

59. **CICD-009** [low] `cicd` — No PR template or issue templates
   Files: .github/PULL_REQUEST_TEMPLATE.md:0

60. **ARCH-005** [low] `architecture` — Composition root (ServiceCollectionExtensions.AddContestJudgingServices) in Services layer instead of outermost layer
   Files: src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:5, src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:6, src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:21, src/ContestJudging.Web/Program.cs:7, src/ContestJudging.Web/Program.cs:64
   Related: ARCH-001, ARCH-002, ARCH-003

61. **ARCH-006** [low] `architecture` — Two IScoringStrategy implementations exist but are never registered in DI container
   Files: src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:33, src/ContestJudging.Services/Scoring/PercentileScoring.cs:1, src/ContestJudging.Services/Scoring/DefinedIntervalScoring.cs:1
   Related: CQ-001

62. **ALGO-003** [low] `algorithm-correctness` — UnionFind implementation lacks union-by-rank/size for optimal complexity
   Files: src/ContestJudging.Services/Validation/GraphValidationService.cs:32

63. **ALGO-004** [low] `algorithm-correctness` — Theoretical division-by-zero in BradleyTerry denominator for crafted zero-gamma entries
   Files: src/ContestJudging.Services/Resolution/BradleyTerryResolutionService.cs:69

64. **ALGO-005** [low] `algorithm-correctness` — Partition bridge node count can evaluate to zero at small overlap rates, breaking connectivity guarantee
   Files: src/ContestJudging.Services/Partitioning/PartitionService.cs:21

65. **BW-009** [low] `blazor-wasm` — Orphaned weather.json scaffold file in production wwwroot output
   Files: src/ContestJudging.Web/wwwroot/sample-data/weather.json:1

66. **EF-007** [low] `efcore` — Connection string hardcoded in Program.cs rather than sourced from configuration
   Files: src/ContestJudging.Web/Program.cs:66
   Related: SEC-003

67. **EF-008** [low] `efcore` — Missing indexes on frequently queried foreign key columns
   Files: src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:54

68. **EF-009** [low] `efcore` — No error handling around SaveChangesAsync calls in repository methods
   Files: src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:38, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:47, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:64, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:140, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:163, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:180, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:228, src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:238

69. **EF-010** [low] `efcore` — EntryScoreEntity has redundant surrogate key alongside natural composite unique constraint
   Files: src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:24, src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:60

70. **TE-011** [low] `test-effectiveness` — E2E tests are shallow smoke tests with no functional workflow validation
   Files: tests/ContestJudging.E2ETests/AppE2ETests.cs:23, tests/ContestJudging.E2ETests/AppE2ETests.cs:30
   Related: TEST-002, TEST-007

71. **TE-012** [low] `test-effectiveness` — TrimmingSafetyTests uses fragile string-contains assertions for JSON validation
   Files: tests/ContestJudging.Tests/TrimmingSafetyTests.cs:33, tests/ContestJudging.Tests/TrimmingSafetyTests.cs:34

72. **TE-013** [low] `test-effectiveness` — PartitionService constructor validation (k <= 0, invalid overlap) untested
   Files: src/ContestJudging.Services/Partitioning/PartitionService.cs:16, src/ContestJudging.Services/Partitioning/PartitionService.cs:17, tests/ContestJudging.Tests/PartitionServiceTests.cs:13

73. **TE-015** [low] `test-effectiveness` — ScoringStrategy empty-tiers and single-entry edge cases untested
   Files: src/ContestJudging.Services/Scoring/LinearSpacingScoring.cs:15, src/ContestJudging.Services/Scoring/PercentileScoring.cs:16, src/ContestJudging.Services/Scoring/DefinedIntervalScoring.cs:22, tests/ContestJudging.Tests/ScoringStrategyTests.cs:13

74. **TE-016** [low] `test-effectiveness` — Entry.SetScore boundary values (0 and maxScore) never tested
   Files: src/ContestJudging.Core/Entities/Entry.cs:19, tests/ContestJudging.Tests/CoreTests.cs:27, tests/ContestJudging.Tests/CoreTests.cs:36
   Related: TEST-001

75. **STRUCT-010** [informational] `structure` — Trim analyzer enabled globally but EF Core (Infrastructure) is not trimming-safe
   Files: Directory.Build.props:10, Directory.Build.props:11, src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:48, src/ContestJudging.Web/ContestJudging.Web.csproj:22

76. **TEST-012** [informational] `tests` — TrimmingSafetyTests suppresses trimming warnings it's meant to validate
   Files: tests/ContestJudging.Tests/TrimmingSafetyTests.cs:19

77. **TEST-013** [informational] `tests` — No cancellation token support in async tests or underlying async methods
   Files: tests/ContestJudging.Tests/InfrastructureTests.cs:35, tests/ContestJudging.Tests/ContestManagerTests.cs:21

78. **SEC-008** [informational] `security` — Client-side SQLite — entire database accessible to browser user
   Files: src/ContestJudging.Web/Program.cs:21, src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:41

79. **CICD-010** [informational] `cicd` — dotnet-tools.json manifest has no tools configured
   Files: dotnet-tools.json:4

80. **ARCH-007** [informational] `architecture` — AddScoped DbContext lifetime in Blazor WASM — DbContext effectively singleton, change tracker grows unbounded
   Files: src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:23, src/ContestJudging.Web/Program.cs:21
   Related: ARCH-001

81. **EF-012** [informational] `efcore` — No .IsRequired() or .HasMaxLength() configuration on string entity properties
   Files: src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:54

