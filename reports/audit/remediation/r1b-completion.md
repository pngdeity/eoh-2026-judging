# R1-B Remediation Report — EF-001 & EF-002

**Agent:** R1-B\
**Date:** 2026-05-26\
**Findings Fixed:** EF-001 (high), EF-002 (high)\
**Branch:** fix/audit-remediate

---

## Build Result

```
dotnet build ContestJudging.slnx --configuration Release
→ Build succeeded. 0 Warning(s) 0 Error(s)
```

## Test Result

```
dotnet test
→ ContestJudging.Tests.dll: 33 Passed, 0 Failed, 0 Skipped
→ ContestJudging.E2ETests.dll: 2 Failed (pre-existing Playwright browser env issue, unrelated)
```

Infrastructure tests specifically:

```
dotnet test --filter "FullyQualifiedName~InfrastructureTests"
→ 5 Passed, 0 Failed
```

---

## EF-001: Schema Version Cookie

### Architectural Decision

EF Core migrations are a server-side pattern. This is a standalone Blazor WASM
app deployed to GitHub Pages with client-side SQLite + localStorage
backup/restore. A schema version cookie pattern is used instead.

### Changes

#### 1. `src/ContestJudging.Web/Program.cs`

**Added `using Microsoft.Extensions.Logging;`** (line 13).

**Added schema version constant and ILogger** (lines 30, 33):

Before (lines 29-57):

```csharp
var host = builder.Build();

// Ensure database is created and restored from LocalStorage if available
using (var scope = host.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ContestDbContext>();
    var localStorage = scope.ServiceProvider.GetRequiredService<ISyncLocalStorageService>();
    var contestManager = scope.ServiceProvider.GetRequiredService<IContestManager>();

    // TRICKY OPTIMIZATION #2: Restore from LocalStorage
    if (localStorage.ContainKey("db_backup"))
    {
        var backupBase64 = localStorage.GetItemAsString("db_backup");
        if (!string.IsNullOrEmpty(backupBase64))
        {
            try
            {
                var backupBytes = Convert.FromBase64String(backupBase64);
                await contestManager.ImportDataAsync(backupBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to restore database: {ex.Message}");
            }
        }
    }

    await context.Database.EnsureCreatedAsync();
}
```

After (lines 30-72):

```csharp
const int CurrentSchemaVersion = 1;
var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

using (var scope = host.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ContestDbContext>();
    var localStorage = scope.ServiceProvider.GetRequiredService<ISyncLocalStorageService>();
    var contestManager = scope.ServiceProvider.GetRequiredService<IContestManager>();

    if (localStorage.ContainKey("db_backup"))
    {
        var schemaVersion = 0;
        if (localStorage.ContainKey("db_schema_version"))
        {
            schemaVersion = localStorage.GetItem<int>("db_schema_version");
        }

        if (schemaVersion == CurrentSchemaVersion)
        {
            var backupBase64 = localStorage.GetItemAsString("db_backup");
            if (!string.IsNullOrEmpty(backupBase64))
            {
                try
                {
                    var backupBytes = Convert.FromBase64String(backupBase64);
                    await contestManager.ImportDataAsync(backupBytes);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to restore database from LocalStorage");
                }
            }
        }
        else
        {
            logger.LogWarning("Schema version mismatch: expected {Expected}, found {Found}. Discarding backup.", CurrentSchemaVersion, schemaVersion);
        }
    }

    await context.Database.EnsureCreatedAsync();
}
```

Changes:

- Added `const int CurrentSchemaVersion = 1;` constant.
- Added `ILoggerFactory` usage to create a structured logger instead of
  `Console.WriteLine`.
- Checks `db_schema_version` before restoring. If version matches, proceeds with
  restore; if mismatch, logs warning and skips restore, letting
  `EnsureCreatedAsync` create a fresh DB.
- Base64 decode is still in a try/catch with proper ILogger.

#### 2. `src/ContestJudging.Web/Pages/Setup.razor.cs`

**Added `private const int CurrentSchemaVersion = 1;`** (after line 24).

**Updated `BackupDatabase()`** (lines 51-59):

Before:

```csharp
private async Task BackupDatabase()
{
    // TRICKY OPTIMIZATION #2: Save to LocalStorage
    var data = await ContestManager.ExportDataAsync();
    if (data.Length > 0)
    {
        await LocalStorage.SetItemAsStringAsync("db_backup", Convert.ToBase64String(data));
    }
}
```

After:

```csharp
private async Task BackupDatabase()
{
    var data = await ContestManager.ExportDataAsync();
    if (data.Length > 0)
    {
        await LocalStorage.SetItemAsStringAsync("db_backup", Convert.ToBase64String(data));
        await LocalStorage.SetItemAsync("db_schema_version", CurrentSchemaVersion);
    }
}
```

#### 3. `src/ContestJudging.Web/Pages/Judging.razor.cs`

Same changes as Setup.razor.cs:

- Added `private const int CurrentSchemaVersion = 1;` after line 25.
- Updated `BackupDatabase()` to also save `db_schema_version`.

---

## EF-002: Foreign Key Relationships

### Architectural Decision

`EnsureCreatedAsync()` creates schema from the model directly, so no migration
step is needed. FK relationships with `OnDelete(DeleteBehavior.Cascade)` replace
manual cascade delete code in repositories. `EntryScoreEntity` uses composite PK
`(EntryId, CategoryId)` instead of surrogate `Id`.

### Changes

#### 1. `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs`

**Added navigation property to CategoryEntity** (line 16):

```csharp
public class CategoryEntity
{
    public string Id { get; set; } = string.Empty;
    public double MaxScore { get; set; }
    public List<EntryScoreEntity> Scores { get; set; } = new();  // ADDED
}
```

**Removed surrogate `Id` from EntryScoreEntity** (was line 26):

```csharp
// REMOVED: public int Id { get; set; }
public class EntryScoreEntity
{
    public string EntryId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public double Score { get; set; }
}
```

**Replaced OnModelCreating** (lines 53-83):

Before:

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

After:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<CategoryEntity>().HasKey(c => c.Id);
    modelBuilder.Entity<EntryEntity>().HasKey(e => e.Id);
    modelBuilder.Entity<RelationEntity>().HasKey(r => r.Id);

    modelBuilder.Entity<EntryScoreEntity>().HasKey(es => new { es.EntryId, es.CategoryId });

    modelBuilder.Entity<EntryScoreEntity>()
        .HasOne<EntryEntity>()
        .WithMany(e => e.Scores)
        .HasForeignKey(es => es.EntryId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<EntryScoreEntity>()
        .HasOne<CategoryEntity>()
        .WithMany(c => c.Scores)
        .HasForeignKey(es => es.CategoryId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<RelationEntity>()
        .HasOne<CategoryEntity>()
        .WithMany()
        .HasForeignKey(r => r.CategoryId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<RelationEntity>()
        .HasOne<EntryEntity>()
        .WithMany()
        .HasForeignKey(r => r.EntryAId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<RelationEntity>()
        .HasOne<EntryEntity>()
        .WithMany()
        .HasForeignKey(r => r.EntryBId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<RelationEntity>().HasIndex(r => r.CategoryId);
    modelBuilder.Entity<RelationEntity>().HasIndex(r => r.EntryAId);
    modelBuilder.Entity<RelationEntity>().HasIndex(r => r.EntryBId);
    modelBuilder.Entity<EntryScoreEntity>().HasIndex(es => es.EntryId);
}
```

Changes:

- EntryScoreEntity: composite PK `(EntryId, CategoryId)` instead of surrogate
  `Id`.
- EntryScoreEntity → Entry FK with Cascade on delete.
- EntryScoreEntity → Category FK with Cascade on delete.
- RelationEntity → Category FK with Cascade on delete.
- RelationEntity → Entry FK (EntryAId) with Cascade on delete.
- RelationEntity → Entry FK (EntryBId) with Cascade on delete.
- Indexes on RelationEntity: CategoryId, EntryAId, EntryBId.
- Index on EntryScoreEntity: EntryId.

#### 2. `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs`

**SqliteCategoryRepository.DeleteAsync** (lines 51-59):

Before (manual cascade of relations + scores):

```csharp
public async Task DeleteAsync(string id)
{
    var entity = await _context.Categories.FindAsync(id);
    if (entity != null)
    {
        // Cascade delete manually
        var relations = await _context.Relations.Where(r => r.CategoryId == id).ToListAsync();
        _context.Relations.RemoveRange(relations);

        var scores = await _context.EntryScores.Where(es => es.CategoryId == id).ToListAsync();
        _context.EntryScores.RemoveRange(scores);

        _context.Categories.Remove(entity);
        await _context.SaveChangesAsync();
    }
}
```

After (FK OnDelete handles cascades):

```csharp
public async Task DeleteAsync(string id)
{
    var entity = await _context.Categories.FindAsync(id);
    if (entity != null)
    {
        _context.Categories.Remove(entity);
        await _context.SaveChangesAsync();
    }
}
```

**SqliteEntryRepository.DeleteAsync** (lines 160-168):

Before (manual cascade of relations + scores):

```csharp
public async Task DeleteAsync(string id)
{
    var entity = await _context.Entries.FindAsync(id);
    if (entity != null)
    {
        // Cascade delete manually
        var relations = await _context.Relations.Where(r => r.EntryAId == id || r.EntryBId == id).ToListAsync();
        _context.Relations.RemoveRange(relations);

        var scores = await _context.EntryScores.Where(es => es.EntryId == id).ToListAsync();
        _context.EntryScores.RemoveRange(scores);

        _context.Entries.Remove(entity);
        await _context.SaveChangesAsync();
    }
}
```

After (FK OnDelete handles cascades):

```csharp
public async Task DeleteAsync(string id)
{
    var entity = await _context.Entries.FindAsync(id);
    if (entity != null)
    {
        _context.Entries.Remove(entity);
        await _context.SaveChangesAsync();
    }
}
```

#### 3. `tests/ContestJudging.Tests/InfrastructureTests.cs` (line 73)

Updated `RelationRepository_AddAndGet_Succeeds` to add entries to the database
before creating relations. This was necessary because the new FK constraints on
`EntryAId`/`EntryBId` now enforce referential integrity — entries must exist in
the DB before relations can reference them.

Before:

```csharp
var entryA = new Entry("A");
var entryB = new Entry("B");
var relation = new Relation(cat, entryA, Operator.GreaterThan, entryB);
```

After:

```csharp
var entryA = new Entry("A");
var entryB = new Entry("B");
await entryRepo.AddAsync(entryA);
await entryRepo.AddAsync(entryB);
var relation = new Relation(cat, entryA, Operator.GreaterThan, entryB);
```

Also added `var entryRepo = new SqliteEntryRepository(context);` to the test
setup.

---

## Issues Encountered

1. **Test `EntryRepository_Delete_Cascades` initially failed** — The task
   instructions specified FK relationships for EntryScoreEntity→Entry,
   EntryScoreEntity→Category, and RelationEntity→Category, but did not mention
   RelationEntity→Entry relationships. However, the existing test expects
   cascading deletion from Entry→Relation. Added
   `RelationEntity.HasOne<EntryEntity>().WithMany().HasForeignKey(r => r.EntryAId/EntryBId).OnDelete(DeleteBehavior.Cascade)`
   to resolve.

2. **Test `RelationRepository_AddAndGet_Succeeds` failed** after adding FK
   constraints — The test created entry objects but never persisted them to the
   database. With FK enforcement now active, entries must exist before relations
   can reference them. Updated the test to add entries via the repository first.
