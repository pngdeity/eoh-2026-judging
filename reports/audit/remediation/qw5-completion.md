# QW-5 Remediation Report

**Agent:** QW-5\
**Tier:** Tier 2 EF Core Polish\
**Findings:** EF-005, EF-008, EF-012, EF-007, SEC-003\
**Status:** Complete — all 51 tests pass, 0 warnings, 0 errors

---

## EF-005: Add AsNoTracking to Read-Only Queries

**File:** `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs`

Added `.AsNoTracking()` to all read-only query methods. Methods that modify
entities (`AddAsync`, `UpdateAsync`, `DeleteAsync`) retain tracking.

| Method                                                            | Change                                                                                          |
| ----------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `SqliteCategoryRepository.GetByIdAsync`                           | Changed `FindAsync(id)` → `AsNoTracking().FirstOrDefaultAsync(c => c.Id == id)`                 |
| `SqliteCategoryRepository.GetAllAsync`                            | Added `.AsNoTracking()` before `.ToListAsync()`                                                 |
| `SqliteEntryRepository.GetByIdAsync`                              | Added `.AsNoTracking()` before `.Include(e => e.Scores)`                                        |
| `SqliteEntryRepository.GetByIdAsync` (categories load)            | Added `.AsNoTracking()` before `.ToListAsync()`                                                 |
| `SqliteEntryRepository.GetAllAsync`                               | Added `.AsNoTracking()` before `.Include(e => e.Scores)`                                        |
| `SqliteEntryRepository.GetAllAsync` (categories load)             | Added `.AsNoTracking()` before `.ToListAsync()`                                                 |
| `SqliteRelationRepository.GetByCategoryIdAsync` (category lookup) | Changed `FindAsync(categoryId)` → `AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId)` |
| `SqliteRelationRepository.GetByCategoryIdAsync` (relations query) | Added `.AsNoTracking()` before `.Where()`                                                       |

---

## EF-008: Missing Indexes on FK Columns

**Status:** Already resolved (pre-existing from EF-002 fix).

**File:** `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs`

Verified FK column indexes exist at lines 94-97:

```csharp
modelBuilder.Entity<RelationEntity>().HasIndex(r => r.CategoryId);
modelBuilder.Entity<RelationEntity>().HasIndex(r => r.EntryAId);
modelBuilder.Entity<RelationEntity>().HasIndex(r => r.EntryBId);
modelBuilder.Entity<EntryScoreEntity>().HasIndex(es => es.EntryId);
```

No changes needed. EF-008 marked resolved.

---

## EF-012: Add IsRequired() / HasMaxLength() on String Properties

**File:** `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs`

Added property constraints for all string FK/ID columns (max length 100):

| Entity             | Property     | Constraint                        |
| ------------------ | ------------ | --------------------------------- |
| `CategoryEntity`   | `Id`         | `.HasMaxLength(100).IsRequired()` |
| `EntryEntity`      | `Id`         | `.HasMaxLength(100).IsRequired()` |
| `EntryScoreEntity` | `EntryId`    | `.HasMaxLength(100).IsRequired()` |
| `EntryScoreEntity` | `CategoryId` | `.HasMaxLength(100).IsRequired()` |
| `RelationEntity`   | `CategoryId` | `.HasMaxLength(100).IsRequired()` |
| `RelationEntity`   | `EntryAId`   | `.HasMaxLength(100).IsRequired()` |
| `RelationEntity`   | `EntryBId`   | `.HasMaxLength(100).IsRequired()` |

---

## EF-007 / SEC-003: Hardcoded Connection Strings

**Status:** Documented as acceptable. This is a client-side WASM app with SQLite
embedded in the browser — no config files or environment variables exist. The
paths are safe and appropriate.

### Comments added:

1. **`src/ContestJudging.Web/Program.cs`** — above the
   `AddContestJudgingServices` call:
   ```
   // Connection string is hardcoded because this is a client-side WASM app
   // with no server-side config file or environment variable support.
   // SQLite is embedded in the browser — the path is safe and appropriate.
   ```

2. **`src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs`** —
   above the `DatabaseBackupService` instantiation:
   ```
   // Database path is hardcoded — client-side WASM app with no config file support.
   // SQLite is embedded in the browser; the path is safe.
   ```

3. **`src/ContestJudging.Infrastructure/Persistence/DatabaseBackupService.cs`**
   — above the `_dbPath` field:
   ```
   // Database path is hardcoded — client-side WASM app with no config file support.
   // SQLite is embedded in the browser; the path is safe.
   ```

---

## Verification

```
Build: Release — 0 warnings, 0 errors
Tests: 51 passed, 0 failed, 0 skipped
```
