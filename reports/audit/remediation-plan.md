# Remediation Plan — EOH 2026 Contest Judging

**Generated:** 2026-05-25\
**Scope:** 1 Critical + 17 High severity findings\
**Total effort estimate:** ~30-40 hours (see per-batch breakdown)

---

## Batch 1: Build Infrastructure (Independent, blocks no other batches)

**Effort:** 30 min | **Order:** Must run first

### STRUCT-003 — CRITICAL: E2E Test CPM Violation

**Severity:** Critical | **Effort:** trivial

**Current code** (`Directory.Packages.props:23`): No NUnit, Playwright package
versions\
**Current code**
(`tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj:14-17`):
PackageReferences without central versions

**Proposed fix:** Add to `Directory.Packages.props` after line 22
(`coverlet.collector`):

```xml
<PackageVersion Include="NUnit" Version="4.3.2" />
<PackageVersion Include="NUnit.Analyzers" Version="4.6.0" />
<PackageVersion Include="NUnit3TestAdapter" Version="5.0.0" />
<PackageVersion Include="Microsoft.Playwright.NUnit" Version="1.52.0" />
```

**Files to touch:**

- `Directory.Packages.props:22`
- `tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj:14` (verify
  builds)

**Verification:**
`dotnet restore ContestJudging.slnx && dotnet build tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj`

**Risk:** None — adding missing package versions only fixes a build error.

**Dependencies:** None\
**Unblocks:** CICD-001

---

### CICD-001 — HIGH: E2E Tests Excluded from Solution

**Severity:** High | **Effort:** trivial

**Current code** (`ContestJudging.slnx:8-10`):

```xml
<Folder Name="/tests/">
    <Project Path="tests/ContestJudging.Tests/ContestJudging.Tests.csproj" />
</Folder>
```

**Proposed fix:** Add after line 9:

```xml
<Project Path="tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj" />
```

**Files to touch:**

- `ContestJudging.slnx:9`

**Verification:**
`dotnet restore ContestJudging.slnx && dotnet build ContestJudging.slnx --configuration Release`

**Risk:** None — adds a project that already exists on disk.

**Dependencies:** STRUCT-003\
**Unblocks:** None (E2E tests are shallow smoke tests only)

---

## Batch 2: EF Core Foundation (Independent from Batch 1)

**Effort:** 3 hours

### EF-001 — HIGH: No Database Migrations

**Severity:** High | **Effort:** small

**Current code** (`src/ContestJudging.Web/Program.cs:56`):

```csharp
await context.Database.EnsureCreatedAsync();
```

**Current code**
(`src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:53-63`):
OnModelCreating exists but no migration snapshot.

**Proposed fix:**

1. Run
   `dotnet ef migrations add InitialCreate --project src/ContestJudging.Infrastructure --startup-project src/ContestJudging.Web`
2. Replace `await context.Database.EnsureCreatedAsync();` with
   `await context.Database.MigrateAsync();` in Program.cs:56

**Files to touch:**

- `src/ContestJudging.Web/Program.cs:56`
- New: `src/ContestJudging.Infrastructure/Migrations/` (generated)

**Verification:**
`dotnet ef migrations script --project src/ContestJudging.Infrastructure --startup-project src/ContestJudging.Web`
— verify generated SQL is correct. Run unit tests.

**Risk:** Low. EnsureCreated creates schema if missing; Migrate applies
versioned schema. Migration must be regenerated if entity model changes.
First-time migration generation may need to handle an existing DB.

**Dependencies:** None\
**Unblocks:** EF-002

---

### EF-002 — HIGH: Missing Foreign Key Relationships

**Severity:** High | **Effort:** small

**Current code**
(`src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:53-63`):

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<CategoryEntity>().HasKey(c => c.Id);
    modelBuilder.Entity<EntryEntity>().HasKey(e => e.Id);
    modelBuilder.Entity<RelationEntity>().HasKey(r => r.Id);
    modelBuilder.Entity<EntryScoreEntity>().HasKey(es => es.Id);
    modelBuilder.Entity<EntryScoreEntity>()
        .HasIndex(es => new { es.EntryId, es.CategoryId })
        .IsUnique();
}
```

**Proposed fix:** Add Fluent API relationships in OnModelCreating (replacing
current block):

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<CategoryEntity>().HasKey(c => c.Id);
    modelBuilder.Entity<EntryEntity>().HasKey(e => e.Id);
    modelBuilder.Entity<RelationEntity>().HasKey(r => r.Id);

    // EntryScoreEntity: composite PK instead of surrogate Id
    modelBuilder.Entity<EntryScoreEntity>().HasKey(es => new { es.EntryId, es.CategoryId });

    // FK: EntryScore -> Entry
    modelBuilder.Entity<EntryScoreEntity>()
        .HasOne<EntryEntity>()
        .WithMany(e => e.Scores)
        .HasForeignKey(es => es.EntryId)
        .OnDelete(DeleteBehavior.Cascade);

    // FK: EntryScore -> Category
    modelBuilder.Entity<EntryScoreEntity>()
        .HasOne<CategoryEntity>()
        .WithMany()
        .HasForeignKey(es => es.CategoryId)
        .OnDelete(DeleteBehavior.Cascade);

    // FK: Relation -> Category
    modelBuilder.Entity<RelationEntity>()
        .HasOne<CategoryEntity>()
        .WithMany()
        .HasForeignKey(r => r.CategoryId)
        .OnDelete(DeleteBehavior.Cascade);

    // Indexes
    modelBuilder.Entity<RelationEntity>().HasIndex(r => r.CategoryId);
    modelBuilder.Entity<RelationEntity>().HasIndex(r => r.EntryAId);
    modelBuilder.Entity<RelationEntity>().HasIndex(r => r.EntryBId);
    modelBuilder.Entity<EntryScoreEntity>().HasIndex(es => es.EntryId);
}
```

Also add navigation property to `CategoryEntity` class:

```csharp
public List<EntryScoreEntity> Scores { get; set; } = new();
```

Also remove manual cascade delete code from SqliteRepositories.cs (lines 56-64
in DeleteAsync, 172-181 in EntryRepository.DeleteAsync) — the FK OnDelete
handles it.

Also remove the `Id` property from `EntryScoreEntity` since composite PK
replaces it.

**Files to touch:**

- `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:12-63`
  (entity classes + OnModelCreating)
- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:51-66`
  (Category DeleteAsync)
- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:167-182`
  (Entry DeleteAsync)

**Verification:** Generate migration after this change:
`dotnet ef migrations add AddForeignKeyRelationships`. Run full test suite.
Verify delete operations still work.

**Risk:** Medium. Removing surrogate key from EntryScoreEntity and manual
cascade code could cause regressions in repository code. All EntryScoreEntity
access by surrogate Id must be converted to composite key access.

**Dependencies:** EF-001 (migration infrastructure must be in place)\
**Unblocks:** None directly

---

## Batch 3: Architecture Cleanup & Error Handling

**Effort:** 4 hours

### ARCH-002 — HIGH: ContestManager Depends on Concrete DbContext

**Severity:** High | **Effort:** small

**Current code**
(`src/ContestJudging.Services/Managers/ContestManager.cs:9,23,32,119-127`):

```csharp
using ContestJudging.Infrastructure.Persistence;  // line 9
private readonly ContestDbContext _context;          // line 23
public ContestManager(..., ContestDbContext context) { _context = context; }  // line 32

public async Task<byte[]> ExportDataAsync()
{
    return await _context.ExportDatabaseAsync();  // line 121
}
public async Task ImportDataAsync(byte[] data)
{
    await _context.ImportDatabaseAsync(data);  // line 126
}
```

**Proposed fix:**

Step 1: Create `IDatabaseBackupService` in Core.Interfaces:

```csharp
// src/ContestJudging.Core/Interfaces/IDatabaseBackupService.cs
namespace ContestJudging.Core.Interfaces;

public interface IDatabaseBackupService
{
    Task<byte[]> ExportAsync();
    Task ImportAsync(byte[] data);
}
```

Step 2: Create `DatabaseBackupService` in Infrastructure:

```csharp
// src/ContestJudging.Infrastructure/Persistence/DatabaseBackupService.cs
namespace ContestJudging.Infrastructure.Persistence;

public class DatabaseBackupService : IDatabaseBackupService
{
    private readonly string _dbPath;
    
    public DatabaseBackupService(string dbPath = "contest.db")
    {
        _dbPath = dbPath;
    }
    
    public async Task<byte[]> ExportAsync()
    {
        if (File.Exists(_dbPath))
            return await File.ReadAllBytesAsync(_dbPath);
        return Array.Empty<byte>();
    }
    
    public async Task ImportAsync(byte[] data)
    {
        if (data == null || data.Length < 16) throw new ArgumentException("Invalid database file");
        // Validate SQLite magic header
        var magic = "SQLite format 3\0";
        for (int i = 0; i < 16; i++)
        {
            if (data[i] != magic[i]) throw new ArgumentException("Not a valid SQLite database file");
        }
        await File.WriteAllBytesAsync(_dbPath, data);
    }
}
```

Step 3: Modify ContestManager — replace `ContestDbContext _context` with
`IDatabaseBackupService _backupService`:

```csharp
private readonly IDatabaseBackupService _backupService;

public ContestManager(
    ICategoryRepository categoryRepository,
    IEntryRepository entryRepository,
    IRelationRepository relationRepository,
    IValidationService validationService,
    IGlobalRankingService globalRankingService,
    IScoringStrategy scoringStrategy,
    IDatabaseBackupService backupService)  // changed
{
    // ... assign all fields including _backupService = backupService;
}

public async Task<byte[]> ExportDataAsync()
{
    return await _backupService.ExportAsync();
}

public async Task ImportDataAsync(byte[] data)
{
    await _backupService.ImportAsync(data);
}
```

Step 4: Remove `ExportDatabaseAsync`/`ImportDatabaseAsync` from
`ContestDbContext` (lines 65-80).

Step 5: Register new service in `ServiceCollectionExtensions.cs`:

```csharp
services.AddScoped<IDatabaseBackupService>(sp => 
    new DatabaseBackupService("contest.db"));
```

Step 6: Update tests — inject `new Mock<IDatabaseBackupService>()` instead of
`null!`.

**Files to touch:**

- `src/ContestJudging.Core/Interfaces/IDatabaseBackupService.cs` (new)
- `src/ContestJudging.Infrastructure/Persistence/DatabaseBackupService.cs` (new)
- `src/ContestJudging.Services/Managers/ContestManager.cs:9,23,32,119-127`
- `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:65-80`
  (delete Export/Import methods)
- `src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:33`
  (add registration)
- `tests/ContestJudging.Tests/ContestManagerTests.cs:30,64,101` (replace null!
  with mock)
- Remove `using ContestJudging.Infrastructure.Persistence;` from
  ContestManager.cs:9

**Verification:** `dotnet test --filter ContestManagerTests` — all 3 tests now
use Moq mock instead of null!. Add test: `ExportDataAsync_ReturnsData` and
`ImportDataAsync_CallsBackupService`.

**Risk:** Medium. Changes DI registration; all consumers must be updated.

**Dependencies:** None strictly (but Batch 2 migration must exist if moving
EF-001)\
**Unblocks:** TE-002, TE-003, TEST-004

---

### EF-003 — HIGH: Database Restore Overwrites File with Active DbContext

**Severity:** High | **Effort:** medium\
_Partially overlaps with CQ-003_

**Current code** (`src/ContestJudging.Web/Program.cs:32-57`):

```csharp
using (var scope = host.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ContestDbContext>(); // line 34
    var localStorage = scope.ServiceProvider.GetRequiredService<ISyncLocalStorageService>();
    var contestManager = scope.ServiceProvider.GetRequiredService<IContestManager>();

    if (localStorage.ContainKey("db_backup"))
    {
        var backupBase64 = localStorage.GetItemAsString("db_backup");
        if (!string.IsNullOrEmpty(backupBase64))
        {
            try
            {
                var backupBytes = Convert.FromBase64String(backupBase64);
                await contestManager.ImportDataAsync(backupBytes);  // OVERWRITES while context is open
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to restore database: {ex.Message}");  // SWALLOWED
            }
        }
    }

    await context.Database.EnsureCreatedAsync();  // RUNS AFTER POTENTIALLY CORRUPTED RESTORE
}
```

**Proposed fix:** Restructure to check and restore before creating the main
scope. After ARCH-002 is done:

```csharp
// Check localStorage for backup BEFORE any DbContext is created
var localStorage = host.Services.GetRequiredService<ISyncLocalStorageService>();
if (localStorage.ContainKey("db_backup"))
{
    var backupBase64 = localStorage.GetItemAsString("db_backup");
    if (!string.IsNullOrEmpty(backupBase64))
    {
        try
        {
            var backupBytes = Convert.FromBase64String(backupBase64);
            var backupService = host.Services.GetRequiredService<IDatabaseBackupService>();
            await backupService.ImportAsync(backupBytes);
        }
        catch (Exception ex)
        {
            // Log full exception; set an error flag for the UI
            var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Startup");
            logger.LogError(ex, "Failed to restore database from LocalStorage");
        }
    }
}

// NOW create the main scope and migrate
using (var scope = host.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ContestDbContext>();
    await context.Database.MigrateAsync();
}
```

Also replace `Console.WriteLine` with `ILogger` usage.

**Files to touch:**

- `src/ContestJudging.Web/Program.cs:31-57` (full rewrite of startup section)

**Verification:**

1. Manual test: launch app with localStorage containing valid backup, verify
   restore works.
2. Manual test: launch app with localStorage containing corrupt data, verify
   graceful degradation.
3. `dotnet test` — ensure no regressions in existing flow.

**Risk:** Medium-High. Changes startup order which is critical path. Must test
both clean start and restore-from-backup paths.

**Dependencies:** ARCH-002 (for IDatabaseBackupService)\
**Unblocks:** None

---

### CQ-003 — HIGH: Swallowed Exception in Database Restore

**Severity:** High | **Effort:** small

This is the same code as EF-003. Both are addressed by the combined fix above.

**Files to touch:**

- `src/ContestJudging.Web/Program.cs:49` (replace Console.WriteLine with
  ILogger)

**Verification:** Same as EF-003.

**Risk:** Same as EF-003.

**Dependencies:** None (though group with EF-003)\
**Unblocks:** None

---

## Batch 4: Algorithm Refactoring (Independent from Batch 3)

**Effort:** 3 hours

### CQ-002 — HIGH: Duplicate IsTotalOrder/IsValidOrder Code

**Severity:** High | **Effort:** small

**Current code:** Three methods in
`src/ContestJudging.Services/Validation/GraphValidationService.cs`:

- `IsTotalOrder` (lines 43-127) — 85 lines
- `IsValidOrder` (lines 129-212) — 84 lines
- `GetSortedTiers` (lines 214-310) — 97 lines

Each independently builds UnionFind, adjacency list, in-degree map, and runs
Kahn's BFS. The only difference:

- `IsTotalOrder` checks `queue.Count > 1` at line 109
- `IsValidOrder` skips that check (accepts branching)
- `GetSortedTiers` batches by tier level and has `if (u == v) continue` at line
  264

**Proposed fix:** Extract shared infrastructure into private methods:

```csharp
private (UnionFind uf, Dictionary<string, HashSet<string>> adjList, 
         Dictionary<string, int> inDegree, Dictionary<string, HashSet<string>>? rootToMembers)
    BuildTopologicalGraph(IEnumerable<Relation> relations, IEnumerable<string> allEntryIds, 
                          bool buildMembership = false)
{
    // UnionFind construction + adjacency + in-degree building
    // Returns the prepared graph structures
}

private List<string> TryTopologicalSort(
    Dictionary<string, HashSet<string>> adjList, 
    Dictionary<string, int> inDegree,
    out bool hasCycle)
{
    // Kahn's algorithm BFS
    // Returns ordered list or partial order if cycle detected
    // hasCycle = true if not all nodes processed
}

// IsTotalOrder becomes:
public bool IsTotalOrder(...) 
{
    var (uf, adj, inDeg, _) = BuildTopologicalGraph(relations, allEntryIds);
    var queue = new Queue<string>(inDeg.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
    int processedNodes = 0;
    while (queue.Count > 0) 
    {
        if (queue.Count > 1) return false; // uniqueness check
        string u = queue.Dequeue();
        processedNodes++;
        // ... process neighbors
    }
    return processedNodes == inDeg.Count;
}

// IsValidOrder wraps TryTopologicalSort
// GetSortedTiers wraps TryTopologicalSort + batching
```

Full refactoring replaces the ~260 lines of duplicated logic with a single
60-line shared method and 3 thin wrappers (15-25 lines each).

**Files to touch:**

- `src/ContestJudging.Services/Validation/GraphValidationService.cs:43-310`
  (rewrite all three methods)
- `src/ContestJudging.Services/Validation/GraphValidationService.cs:264` (fix
  self-loop behavior per ALGO-001)

**Verification:** `dotnet test --filter ValidationServiceTests` — all 8 existing
validation tests must pass unchanged. After refactor, add tests for LessThan
operator coverage (TE-008).

**Risk:** Medium. Heavy refactor of core algorithm with many edge cases. All 8
existing tests must pass identically. Self-loop change at line 264 changes
behavior from `continue` (silent skip) to throwing — verify no production code
relies on silent skip.

**Dependencies:** None\
**Unblocks:** ALGO-001 (self-loop fix), ALGO-002 (duplicate code), CQ-006
(extracted methods reduce nesting), TE-014 (test coverage clarity)

---

## Batch 5: UI Fixes (Independent)

**Effort:** 15 min

### BW-004 — HIGH: Bootstrap JS Not Loaded — Accordion Non-Functional

**Severity:** High | **Effort:** trivial

**Current code** (`src/ContestJudging.Web/wwwroot/index.html:9-15`):

```html
<link rel="stylesheet" href="lib/bootstrap/dist/css/bootstrap.min.css" />
<!-- NO Bootstrap JS bundle -->
```

**Proposed fix:** Add after line 10 (or before `</body>`):

```html
<script src="lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
```

If the Bootstrap JS file is not in wwwroot/lib, either:

- Copy it from a CDN-NuGet package, or
- Replace the accordion with pure Blazor conditional rendering (preferred for
  trimming safety)

**Files to touch:**

- `src/ContestJudging.Web/wwwroot/index.html:10`

**Verification:** Build, launch app, navigate to Judging page, click "Manual
Override / Correction" button — accordion should expand/collapse.

**Risk:** Low. Adding a script tag. If JS file doesn't exist at that path,
publish will fail. Verify file exists first.

**Dependencies:** None\
**Unblocks:** None

---

## Batch 6: CI/CD Improvements (Independent)

**Effort:** 1 hour

### CICD-004 — HIGH: No SAST/CodeQL Scanning

**Severity:** High | **Effort:** small

**Current code** (`pipeline.yml:52-60`): Only OSV-Scanner dependency scanning,
no SAST.

**Proposed fix:** Add a CodeQL job to `.github/workflows/pipeline.yml`:

```yaml
codeql-analysis:
  name: CodeQL Analysis
  runs-on: ubuntu-latest
  permissions:
    actions: read
    contents: read
    security-events: write
  strategy:
    fail-fast: false
    matrix:
      language: ["csharp"]
  steps:
    - name: Checkout repository
      uses: actions/checkout@v6.1.0

    - name: Initialize CodeQL
      uses: github/codeql-action/init@v3
      with:
        languages: ${{ matrix.language }}

    - name: Setup .NET
      uses: actions/setup-dotnet@v5
      with:
        dotnet-version: "10.0.x"

    - name: Build
      run: dotnet build ContestJudging.slnx --configuration Release

    - name: Perform CodeQL Analysis
      uses: github/codeql-action/analyze@v3
```

Or use `github/codeql-action/init@v3.32.0` pinned for supply chain safety.

**Files to touch:**

- `.github/workflows/pipeline.yml:52` (after security-scan job)

**Verification:** PR on this branch — CodeQL job appears in GitHub Actions and
runs without errors.

**Risk:** Minimal. CodeQL may produce false positives. Start with
`actions: read` to avoid issues.

**Dependencies:** None\
**Unblocks:** None

---

## Batch 7: Test Quality Improvements

**Effort:** 12-16 hours

### TE-001 — HIGH: PartitionService Tests Non-Deterministic

**Severity:** High | **Effort:** small

**Current code**
(`src/ContestJudging.Services/Partitioning/PartitionService.cs:9`):

```csharp
private readonly Random _random = new();
```

**Current code** (`tests/ContestJudging.Tests/PartitionServiceTests.cs:22,31`):

```csharp
var partitions = service.GeneratePartitions(allEntryIds, k, overlap);
Assert.Equal(10, common.Count);  // non-deterministic!
```

**Proposed fix:**

Step 1: Add overload to PartitionService accepting a `Random` instance:

```csharp
public class PartitionService : IPartitionService
{
    private readonly Random _random;
    
    public PartitionService() : this(new Random()) { }
    
    public PartitionService(Random random)
    {
        _random = random;
    }
    // ...
}
```

Step 2: Update tests to use seeded Random:

```csharp
var service = new PartitionService(new Random(42));
var partitions = service.GeneratePartitions(allEntryIds, k, overlap);
Assert.Equal(10, common.Count);  // now deterministic
```

**Files to touch:**

- `src/ContestJudging.Services/Partitioning/PartitionService.cs:9` (add
  constructor overload)
- `tests/ContestJudging.Tests/PartitionServiceTests.cs:16,41` (use seeded
  Random)

**Verification:** Run `dotnet test --filter PartitionServiceTests` 10 times —
must pass identically each time.

**Risk:** Low — additive change. Default parameterless constructor keeps
existing production behavior.

**Dependencies:** None\
**Unblocks:** None

---

### TE-002 — HIGH: ContestManager Export/Import Untestable

**Severity:** High | **Effort:** medium\
_Resolved by ARCH-002_

After ARCH-002 is implemented (IDatabaseBackupService extracted), add tests:

```csharp
[Fact]
public async Task ExportDataAsync_DelegatesToBackupService()
{
    var mockBackup = new Mock<IDatabaseBackupService>();
    mockBackup.Setup(b => b.ExportAsync()).ReturnsAsync(new byte[] { 1, 2, 3 });
    var manager = new ContestManager(..., mockBackup.Object);
    var result = await manager.ExportDataAsync();
    Assert.Equal(new byte[] { 1, 2, 3 }, result);
}

[Fact]
public async Task ImportDataAsync_DelegatesToBackupService()
{
    var mockBackup = new Mock<IDatabaseBackupService>();
    var manager = new ContestManager(..., mockBackup.Object);
    var data = new byte[] { 1, 2, 3 };
    await manager.ImportDataAsync(data);
    mockBackup.Verify(b => b.ImportAsync(data), Times.Once);
}
```

**Files to touch:**

- `tests/ContestJudging.Tests/ContestManagerTests.cs:130` (add 2 new tests)

**Verification:** `dotnet test --filter ContestManagerTests.ExportDataAsync` and
`--filter ContestManagerTests.ImportDataAsync`

**Risk:** None — pure additive tests.

**Dependencies:** ARCH-002\
**Unblocks:** None

---

### TE-003 — HIGH: LocalStorage Backup/Restore Zero Coverage

**Severity:** High | **Effort:** medium

After ARCH-002 provides IDatabaseBackupService, extract backup logic from page
code-behind into a dedicated `IBackupService` with a mockable interface. Then
add tests:

```csharp
// New: src/ContestJudging.Services/Managers/BackupService.cs
public class BackupService : IBackupService
{
    private readonly IDatabaseBackupService _dbBackup;
    private readonly ILocalStorageService _localStorage;
    
    // BackupAsync, RestoreAsync with try-catch and ILogger
}

// Test: tests/ContestJudging.Tests/BackupServiceTests.cs
[Fact]
public async Task BackupAsync_StoresBase64InLocalStorage() { ... }
[Fact]
public async Task RestoreAsync_ReadsFromLocalStorage_ReturnsData() { ... }
[Fact]
public async Task RestoreAsync_NoBackupKey_ReturnsNull() { ... }
[Fact]
public async Task RestoreAsync_CorruptBase64_LogsError() { ... }
```

**Files to touch:**

- `src/ContestJudging.Core/Interfaces/IBackupService.cs` (new)
- `src/ContestJudging.Services/Managers/BackupService.cs` (new)
- `src/ContestJudging.Web/Pages/Setup.razor.cs:42-57` (use IBackupService)
- `src/ContestJudging.Web/Pages/Judging.razor.cs:72,76-84` (use IBackupService)
- `src/ContestJudging.Web/Program.cs:39-54` (use IBackupService)
- `tests/ContestJudging.Tests/BackupServiceTests.cs` (new)
- `src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:34`
  (register IBackupService)

**Verification:** `dotnet test --filter BackupServiceTests` — 4 new tests.
Manual integration test: launch app, add data, close, reopen, verify data
persists.

**Risk:** Medium. Extracting new abstraction and changing DI. All backup paths
in Setup and Judging pages need updating.

**Dependencies:** ARCH-002 (IDatabaseBackupService), BW-007 (adds schema
validation)\
**Unblocks:** None

---

### TE-004 — HIGH: BradleyTerry Convergence Paths Untested

**Severity:** High | **Effort:** medium

**Current tests** (`ResolutionServiceTests.cs:13,41`): Both use ≤3 entries,
converge in 1 iteration. Never hit the rank-stability early exit at line 90-111.

**Proposed fix:** Add tests:

```csharp
[Fact]
public void ResolveGlobalStrengths_ConvergesWithManyEntries()
{
    var service = new BradleyTerryResolutionService();
    // 20 entries in a linear order: E1 > E2 > E3 > ... > E20
    var entries = Enumerable.Range(1, 20).Select(i => new Entry($"E{i}")).ToList();
    var cat = new Category("cat1", 10);
    var allEntryIds = entries.Select(e => e.Id).ToList();
    
    var relations = new List<Relation>();
    for (int i = 0; i < 19; i++)
        relations.Add(new Relation(cat, entries[i], Operator.GreaterThan, entries[i+1]));
    
    var strengths = service.ResolveGlobalStrengths(relations, allEntryIds);
    
    Assert.Equal(20, strengths.Count);
    for (int i = 0; i < 19; i++)
        Assert.True(strengths[$"E{i+1}"] > strengths[$"E{i+2}"]);
}

[Fact]
public void ResolveGlobalStrengths_EmptyInput_ReturnsEmpty()
{
    var service = new BradleyTerryResolutionService();
    var result = service.ResolveGlobalStrengths(Array.Empty<Relation>(), Array.Empty<string>());
    Assert.Empty(result);
}

[Fact]
public void ResolveGlobalStrengths_SingleEntry_ReturnsLogZero()
{
    var service = new BradleyTerryResolutionService();
    var result = service.ResolveGlobalStrengths(Array.Empty<Relation>(), new[] { "E1" });
    Assert.Single(result);
    Assert.Equal(0.0, result["E1"]);
}

[Fact]
public void ResolveGlobalStrengths_MaxIterationsExhausted_ReturnsBestEffort()
{
    // 50 entries with complex partial rankings forcing many iterations
    // ...
}
```

**Files to touch:**

- `tests/ContestJudging.Tests/ResolutionServiceTests.cs:62` (add 3-4 new test
  methods)

**Verification:** `dotnet test --filter ResolutionServiceTests` — 6 tests total
(2 existing + 4 new). Verify convergence test hits rank-stability path.

**Risk:** Low — purely additive tests.

**Dependencies:** None\
**Unblocks:** None

---

### TE-005 — HIGH: CalculateScoresFromStrengths Untested

**Severity:** High | **Effort:** small

**Current tests** (`ScoringStrategyTests.cs:13-87`): Only `CalculateScores`
(tier-based) is tested. `CalculateScoresFromStrengths` (strength-based, used by
ContestManager) has zero coverage.

**Proposed fix:** Add for each strategy:

```csharp
[Fact]
public void LinearSpacing_CalculateScoresFromStrengths_VariedStrengths()
{
    var strategy = new LinearSpacingScoring();
    var strengths = new Dictionary<string, double> { { "A", 3.0 }, { "B", 1.0 }, { "C", 0.0 } };
    var scores = strategy.CalculateScoresFromStrengths(strengths, 100);
    Assert.Equal(100.0, scores["A"]); // max strength -> max score
    Assert.Equal(33.33, scores["B"], 2); // mid strength -> mid score
    Assert.Equal(0.0, scores["C"]); // min strength -> 0
}

[Fact]
public void LinearSpacing_CalculateScoresFromStrengths_AllSameStrength()
{
    var strategy = new LinearSpacingScoring();
    var strengths = new Dictionary<string, double> { { "A", 5.0 }, { "B", 5.0 } };
    var scores = strategy.CalculateScoresFromStrengths(strengths, 100);
    Assert.Equal(100.0, scores["A"]); // range < epsilon -> all get max
    Assert.Equal(100.0, scores["B"]);
}

[Fact]
public void LinearSpacing_CalculateScoresFromStrengths_SingleEntry()
{
    var strategy = new LinearSpacingScoring();
    var strengths = new Dictionary<string, double> { { "A", 0.5 } };
    var scores = strategy.CalculateScoresFromStrengths(strengths, 10);
    Assert.Equal(10.0, scores["A"]); // single entry -> max score
}

// Repeat for PercentileScoring and DefinedIntervalScoring
```

**Files to touch:**

- `tests/ContestJudging.Tests/ScoringStrategyTests.cs:87` (add 2-3 test methods
  per strategy)

**Verification:** `dotnet test --filter ScoringStrategyTests` — all old + new
tests pass. Verify edge case `range < 1e-9` is exercised.

**Risk:** None — additive tests.

**Dependencies:** None\
**Unblocks:** None

---

### TEST-001 — HIGH: No Parameterized Tests

**Severity:** High | **Effort:** small

**Current code** (`tests/ContestJudging.Tests/CoreTests.cs:12-16`):

```csharp
[Fact]
public void Category_Constructor_ThrowsWhenMaxScoreIsOneOrLess()
{
    Assert.Throws<ArgumentOutOfRangeException>(() => new Category("cat1", 1));
    Assert.Throws<ArgumentOutOfRangeException>(() => new Category("cat1", 0));
}
```

**Proposed fix:**

```csharp
[Theory]
[InlineData(1)]
[InlineData(0)]
[InlineData(-1)]
public void Category_Constructor_ThrowsWhenMaxScoreIsInvalid(double invalidMaxScore)
{
    Assert.Throws<ArgumentOutOfRangeException>(() => new Category("cat1", invalidMaxScore));
}
```

Similarly for `Entry_SetScore_InvalidScore_Throws`:

```csharp
[Theory]
[InlineData(11)]  // > MaxScore
[InlineData(-1)]  // < 0
public void Entry_SetScore_InvalidScore_Throws(double invalidScore)
{
    var entry = new Entry("entry1");
    var category = new Category("cat1", 10);
    Assert.Throws<ArgumentOutOfRangeException>(() => entry.SetScore(category, invalidScore));
}
```

**Files to touch:**

- `tests/ContestJudging.Tests/CoreTests.cs:12` (convert to Theory)
- `tests/ContestJudging.Tests/CoreTests.cs:36` (convert to Theory)

**Verification:** `dotnet test --filter CoreTests` — each Theory shows 2-3 cases
in test results. All pass.

**Risk:** None — additive/refactoring of test code only.

**Dependencies:** None\
**Unblocks:** None

---

### TEST-002 — HIGH: Web Project Has Zero Unit Tests

**Severity:** High | **Effort:** large

No bUnit or web-layer tests exist. The Web project contains pure-logic
components that should be testable:

- `Judging.FindSuggestedPair()` — pairing algorithm
- `Judging.HandleKeyDown()` — keyboard routing
- `Judging.AddRelation()` — validation logic
- `Setup.GeneratePartitions()` — partition delegate
- `Results.CalculateResults()` — result aggregation

**Proposed fix:** Add a `ContestJudging.Web.Tests` project using bunit:

```bash
dotnet new xunit -o tests/ContestJudging.Web.Tests -n ContestJudging.Web.Tests
```

Add bunit package:

```xml
<PackageReference Include="bunit" Version="1.38.5" />
<ProjectReference Include="..\..\src\ContestJudging.Web\ContestJudging.Web.csproj" />
```

Test examples:

```csharp
// JudgingTests.cs
[Trait("Category", "Unit")]
public class JudgingTests : TestContext
{
    [Fact]
    public void FindSuggestedPair_TwoEntriesNoRelations_ReturnsPair()
    {
        // Render Judging component, inject mocks, verify FindSuggestedPair
    }
    
    [Fact]
    public void AddRelation_SameEntry_ShowsError()
    {
        // Verify error message when entryA == entryB
    }
    
    [Theory]
    [InlineData("a", Operator.GreaterThan)]
    [InlineData("arrowleft", Operator.GreaterThan)]
    [InlineData("d", Operator.LessThan)]
    [InlineData("arrowright", Operator.LessThan)]
    [InlineData("s", Operator.EqualTo)]
    public void HandleKeyDown_DispatchsCorrectOperator(string key, Operator expected)
    {
        // Verify keyboard handler dispatches correct operator
    }
}

// SetupTests.cs
[Trait("Category", "Unit")]
public class SetupTests : TestContext
{
    [Fact]
    public void AddCategory_GeneratesCorrectEntity() { ... }
    [Fact]
    public void BulkImportEntries_DedupesCorrectly() { ... }
}
```

**Files to touch (minimal scope):**

- `tests/ContestJudging.Web.Tests/ContestJudging.Web.Tests.csproj` (new)
- `tests/ContestJudging.Web.Tests/JudgingTests.cs` (new)
- `tests/ContestJudging.Web.Tests/SetupTests.cs` (new)
- `tests/ContestJudging.Web.Tests/ResultsTests.cs` (new)
- `Directory.Packages.props:22` (add bunit version)
- `ContestJudging.slnx:9` (add project)
- `.github/workflows/pipeline.yml:50` (will pick up from solution)

**Verification:** `dotnet test --filter Category=Unit` runs all unit tests
including new Web tests.

**Risk:** Medium. bunit for Blazor WASM has quirks — JS interop calls
(localStorage, SQLite) need mocking. Start with pure-logic tests
(FindSuggestedPair, validation) before testing rendering.

**Dependencies:** CQ-001 (dead Class1.cs unrelated)\
**Unblocks:** None

---

## Batch 8: Cleanup (Independent, trivial)

**Effort:** 5 min

### CQ-001 — HIGH: Dead Class1.cs in Infrastructure

**Severity:** High | **Effort:** trivial

**Current code** (`src/ContestJudging.Infrastructure/Class1.cs:1-6`):

```csharp
namespace ContestJudging.Infrastructure;
public class Class1
{
}
```

**Proposed fix:** `rm src/ContestJudging.Infrastructure/Class1.cs`

**Files to touch:**

- `src/ContestJudging.Infrastructure/Class1.cs` (delete)

**Verification:** `dotnet build ContestJudging.slnx` — succeeds without
Class1.cs.

**Risk:** None — no references anywhere in the codebase.

**Dependencies:** None\
**Unblocks:** None

---

## Execution Order Summary

| Order | Batch                 | Findings                                                   | Effort | Depends On       |
| ----- | --------------------- | ---------------------------------------------------------- | ------ | ---------------- |
| 1     | Build Infrastructure  | STRUCT-003, CICD-001                                       | 30 min | —                |
| 2     | EF Core Foundation    | EF-001, EF-002                                             | 3 hr   | —                |
| 3     | Architecture Cleanup  | ARCH-002, EF-003, CQ-003                                   | 4 hr   | Batch 2          |
| 4     | Algorithm Refactoring | CQ-002                                                     | 3 hr   | —                |
| 5     | UI Fixes              | BW-004                                                     | 15 min | —                |
| 6     | CI/CD                 | CICD-004                                                   | 1 hr   | —                |
| 7     | Test Quality          | TE-001, TE-002, TE-003, TE-004, TE-005, TEST-001, TEST-002 | 16 hr  | Batch 3, Batch 4 |
| 8     | Cleanup               | CQ-001                                                     | 5 min  | —                |

**Total findings:** 1 Critical + 17 High = 18\
**Total estimated effort:** ~28 hours
