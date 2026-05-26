# Re-Audit Report: EF Core (P2-D)

**Agent**: PA2-D\
**Domain**: efcore\
**Branch**: `fix/audit-remediate`\
**Date**: 2026-05-26\
**Original audit**: `reports/audit/findings-P2-efcore.json`

## Summary

| Metric           | Original | Remediated                     |
| ---------------- | -------- | ------------------------------ |
| Findings (total) | 12       | 15 (6 resolved, 6 open, 3 new) |
| High severity    | 3        | 2 resolved, 2 new              |
| Medium severity  | 3        | 1 new                          |
| Low severity     | 4        | 3 open                         |
| Informational    | 2        | 1 resolved, 1 open             |

**Overall health**: PARTIALLY REMEDIATED. The three highest-severity original
findings (EF-001, EF-002, EF-003) are properly fixed. However, 6 original
findings remain open and 3 new issues were discovered, 2 of them high severity.

---

## Verified Resolutions

### EF-001 — Schema Version Cookie ✅ RESOLVED

- `BackupService.cs:12`: `private const int CurrentSchemaVersion = 1;` defined.
- `SaveBackupAsync` (`BackupService.cs:24-25`): stores `db_backup` (base64) and
  `db_schema_version` (int) in localStorage.
- `TryRestoreBackupAsync` (`BackupService.cs:33-41`): reads `db_schema_version`,
  compares to `CurrentSchemaVersion`, discards backup and removes both keys on
  mismatch. Logs warning.
- `Program.cs:37`: uses `EnsureCreatedAsync()` — correct architectural decision
  for client-only SQLite (not `MigrateAsync()`).
- Tests at `tests/ContestJudging.Tests/BackupServiceTests.cs` validate all
  scenarios (save, no-backup, version-mismatch, valid-restore, corrupt-base64).

### EF-002 — FK Relationships ✅ RESOLVED

`ContestDbContext.cs:51-93` OnModelCreating fully configured:

| Entity           | FK         | Target         | Delete  |
| ---------------- | ---------- | -------------- | ------- |
| EntryScoreEntity | EntryId    | EntryEntity    | Cascade |
| EntryScoreEntity | CategoryId | CategoryEntity | Cascade |
| RelationEntity   | CategoryId | CategoryEntity | Cascade |
| RelationEntity   | EntryAId   | EntryEntity    | Cascade |
| RelationEntity   | EntryBId   | EntryEntity    | Cascade |

- EntryScoreEntity composite PK `(EntryId, CategoryId)` at line 57.
- Indexes: `RelationEntity.CategoryId` (89), `RelationEntity.EntryAId` (90),
  `RelationEntity.EntryBId` (91), `EntryScoreEntity.EntryId` (92).
- CategoryEntity navigation `List<EntryScoreEntity> Scores` at line 14.
- EntryEntity navigation `List<EntryScoreEntity> Scores` at line 20.
- **No manual cascade deletes** in `SqliteRepositories.cs`. `DeleteAsync`
  methods (lines 51-58, 160-168, 217-226) use `FindAsync` + `Remove` +
  `SaveChangesAsync` only.
- **Zero references** to old surrogate `EntryScoreEntity.Id` anywhere in the
  codebase. Entity class (lines 23-28) has no `Id` property.

### EF-003 — Restore Ordering ✅ RESOLVED

`Program.cs:31-38`:

```
Line 31: var backupService = host.Services.GetRequiredService<IBackupService>();
Line 32: await backupService.TryRestoreBackupAsync();          // ← restore FIRST
Line 34: using (var scope = host.Services.CreateScope())       // ← scope created AFTER
Line 36:     var context = scope.ServiceProvider.GetRequiredService<ContestDbContext>();
Line 37:     await context.Database.EnsureCreatedAsync();
```

No DbContext scope exists during restore. Restore completes before any database
connection is opened.

### EF-008 — Missing Indexes ✅ RESOLVED

Indexes added to `ContestDbContext.cs:89-92`:

- `HasIndex(r => r.CategoryId)` on RelationEntity
- `HasIndex(r => r.EntryAId)` on RelationEntity
- `HasIndex(r => r.EntryBId)` on RelationEntity
- `HasIndex(es => es.EntryId)` on EntryScoreEntity

### EF-010 — Redundant Surrogate Key ✅ RESOLVED

`EntryScoreEntity` (`ContestDbContext.cs:23-28`): surrogate `Id` removed.
Composite PK `(EntryId, CategoryId)` at line 57. No code references the old
surrogate.

### EF-011 — DbContext SRP Violation ✅ RESOLVED

File I/O methods (`ExportDatabaseAsync`, `ImportDatabaseAsync`) extracted to
`DatabaseBackupService.cs` (33 lines). Implements `IDatabaseBackupService`
interface. `ContestDbContext` no longer contains filesystem I/O. Import includes
SQLite magic number validation (lines 25-30).

---

## Unresolved Original Findings

### EF-004 — O(n*m) Client-Side Join ❌ STILL OPEN

**Severity**: medium | **Category**: performance

`SqliteRepositories.cs:80-88` (`GetByIdAsync`) and
`SqliteRepositories.cs:100-113` (`GetAllAsync`) still iterate:

```csharp
var categories = await _context.Categories.ToListAsync();           // fetch all N categories
foreach (var scoreEntity in entity.Scores)                          // foreach of M scores
{
    var categoryEntity = categories.FirstOrDefault(...);            // O(N) per score = O(N*M)
}
```

**Root cause**: `EntryScoreEntity` lacks a `Category` navigation property (only
shadow navigation via type-based FK config). `.ThenInclude(s => s.Category)` is
impossible without a CLR property. See RA-EF-003 below.

### EF-005 — Missing AsNoTracking ❌ STILL OPEN

**Severity**: medium | **Category**: performance

Zero `.AsNoTracking()` calls in the entire `src/` directory. Affected queries
unchanged from original:

- `SqliteRepositories.cs:24`: `FindAsync` (implicit tracking)
- `SqliteRepositories.cs:30`: `Categories.ToListAsync()`
- `SqliteRepositories.cs:74-75`:
  `Entries.Include(e => e.Scores).FirstOrDefaultAsync()`
- `SqliteRepositories.cs:96-97`: `Entries.Include(e => e.Scores).ToListAsync()`
- `SqliteRepositories.cs:182`: `Categories.FindAsync()`
- `SqliteRepositories.cs:186-188`: `Relations.Where(...).ToListAsync()`

### EF-006 — No Transaction Wrapping ❌ STILL OPEN

**Severity**: medium | **Category**: transactions

`ContestManager.cs:106-113` still has no transaction:

```csharp
foreach (var entry in entries)
{
    if (scores.TryGetValue(entry.Id, out double score))
    {
        entry.SetScore(category, score);
        await _entryRepository.UpdateAsync(entry);  // SaveChangesAsync called per iteration
    }
}
```

Partial updates possible on failure. No `BeginTransactionAsync()` or batched
`SaveChangesAsync`.

### EF-007 — Hardcoded Connection String ❌ STILL OPEN

**Severity**: low | **Category**: configuration

`Program.cs:47`: `"Data Source=contest.db"` hardcoded. Not sourced from
`appsettings.json` or environment variables.

### EF-009 — No Error Handling on SaveChangesAsync ❌ STILL OPEN

**Severity**: low | **Category**: error-handling

All 8 `SaveChangesAsync()` calls in `SqliteRepositories.cs` (lines 38, 47, 57,
133, 156, 166, 214, 224) lack try/catch for `DbUpdateException`.

### EF-012 — No IsRequired/HasMaxLength ❌ STILL OPEN

**Severity**: informational | **Category**: schema-design

No `.IsRequired()` or `.HasMaxLength()` on any string PK/FK properties in
`OnModelCreating`.

---

## New Findings

### RA-EF-001 — SQLite Foreign Key Enforcement Not Enabled

**Severity**: HIGH | **Category**: data-integrity | **Files**: `Program.cs:47`,
`ServiceCollectionExtensions.cs:23`

SQLite has foreign key constraint enforcement **off** by default. Neither the
connection string (`"Data Source=contest.db"`) nor any startup code sets
`PRAGMA foreign_keys = ON`. While EF Core generates explicit DELETE/INSERT
statements for tracked entities, raw database-level referential integrity is
absent:

- No check constraints at the SQLite file level
- External tool access to the DB file will not enforce FKs
- The PK/FK relationships configured in OnModelCreating exist only in EF Core's
  model, not in the physical schema

**Remediation**: Add `foreign_keys=true` to the connection string:
`"Data Source=contest.db;foreign_keys=true"` or execute
`PRAGMA foreign_keys = ON` at connection open.

### RA-EF-002 — Schema Change Breaks Existing Databases Without Migration Path

**Severity**: HIGH | **Category**: schema-management | **Files**:
`ContestDbContext.cs:23,57`, `Program.cs:37`

The change from surrogate `Id` (int PK) to composite `(EntryId, CategoryId)`
(string PK) on `EntryScoreEntity` is an incompatible schema change.
`EnsureCreatedAsync()` only creates tables for new databases — it does NOT alter
existing tables. Users with a pre-remediation `contest.db` file will encounter
EF Core mapping errors at startup because the existing `EntryScores` table still
has the old `Id` column and no composite PK.

The `db_schema_version` cookie (EF-001) protects localStorage backups but does
NOT protect an existing `contest.db` file on disk from being stale.

**Remediation**: Implement a startup check: if the database file exists, read
schema info to determine if migration is needed. Either (a) re-implement via
migrations, or (b) add a version marker to the DB itself and
auto-delete/recreate on version mismatch with user confirmation.

### RA-EF-003 — EntryScoreEntity Lacks Navigation Properties Blocking ThenInclude

**Severity**: MEDIUM | **Category**: schema-design | **Files**:
`ContestDbContext.cs:23-28,59-69`

`EntryScoreEntity` has no CLR navigation properties:

```csharp
public class EntryScoreEntity
{
    public string EntryId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public double Score { get; set; }
    // No Category Category { get; set; }
    // No EntryEntity Entry { get; set; }
}
```

FK relationships exist in OnModelCreating (lines 59-69) but use type-based
`HasOne<...>()` without navigation, creating shadow navigations only. This
means:

- `.ThenInclude(s => s.Category)` is impossible — LINQ queries cannot traverse
  the FK
- The EF-004 O(n*m) join cannot be resolved until this is fixed
- Shadow navigation is fragile: the relationship exists at the EF Core level but
  is invisible to developer tooling

**Remediation**: Add `public CategoryEntity Category { get; set; } = null!;` and
`public EntryEntity Entry { get; set; } = null!;` to `EntryScoreEntity`. Update
FK config to use navigation expressions:
`.HasOne(es => es.Category).WithMany(c => c.Scores)`. Then refactor repository
queries to use `.Include(e => e.Scores).ThenInclude(s => s.Category)`.

---

## Additional Observations

### FK Cascade Correctness

EntryScoreEntity deletion when Category or Entry is removed will be handled
correctly by EF Core's change tracker — EF Core generates multi-statement DELETE
commands for cascading deletes. The `DeleteBehavior.Cascade` configuration at
lines 63, 69, 75, 81, 87 covers all paths. However, this depends on EF Core
tracking the entities; raw SQL bypasses it (see RA-EF-001).

### Dual Entry FK on RelationEntity

`RelationEntity` has two FK relationships to `EntryEntity` (EntryAId, EntryBId).
Both use `HasOne<EntryEntity>().WithMany()` without navigation properties. Since
the FK property names are different, EF Core 6+ handles this correctly. When an
Entry is deleted, EF Core generates DELETE statements for both `EntryAId` and
`EntryBId` matches. No duplicate row deletion issue exists — SQLite DELETE is
idempotent for the same row.

### BackupService API

`ContainKeyAsync` (missing 's') is the correct Blazored.LocalStorage API name —
verified as a known quirk of that library. The tests confirm consistent usage.

---

## Metrics

| Metric                   | Value                                                           |
| ------------------------ | --------------------------------------------------------------- |
| Original findings        | 12                                                              |
| Resolved                 | 6 (EF-001, EF-002, EF-003, EF-008, EF-010, EF-011)              |
| Still open               | 6 (EF-004, EF-005, EF-006, EF-007, EF-009, EF-012)              |
| New findings             | 3 (RA-EF-001, RA-EF-002, RA-EF-003)                             |
| High severity remaining  | 2 (RA-EF-001, RA-EF-002)                                        |
| Files scanned            | 8 source files, 1 test file                                     |
| Remediation pass quality | PARTIAL — core architecture fixed, quality refinements deferred |
