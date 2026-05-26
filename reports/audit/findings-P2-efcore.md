# P2-D: EF Core & Persistence Audit

**Agent:** P2-D | **Pass:** 2 | **Domain:** efcore | **Status:** success

---

## Summary

| Metric            | Value              |
| ----------------- | ------------------ |
| Files scanned     | 10                 |
| Findings          | 12                 |
| Lines of code     | ~500               |
| Migration files   | 0                  |
| Transaction usage | 0 explicit         |
| Raw SQL usage     | 0                  |
| Lazy loading      | Disabled (default) |

---

## Findings

### EF-001 — No database migration files; schema managed exclusively via EnsureCreatedAsync()

- **Severity:** high
- **Category:** schema-management
- **Files:**
  `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:53`,
  `src/ContestJudging.Web/Program.cs:56`
- **Evidence:** `glob(**/Migrations/**)` returns zero files.
  `EnsureCreatedAsync()` is called at startup (Program.cs:56). The
  `OnModelCreating` method configures the schema (ContestDbContext.cs:53-63),
  but no versioned migration history exists.
- **Rule violated:** EF Core best practice — `EnsureCreated()` is for
  testing/in-memory scenarios. Production apps should use migrations for
  versioned, upgradeable schema management.
- **Remediation:** Run `dotnet ef migrations add InitialCreate` to generate a
  migration. Replace `EnsureCreatedAsync()` with `Database.MigrateAsync()`. This
  preserves data across schema changes and tracks model snapshots.
- **Effort:** small

### EF-002 — Missing foreign key relationships in entity configuration

- **Severity:** high
- **Category:** schema-design
- **Files:**
  `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:54`
- **Evidence:** `OnModelCreating` only configures primary keys and one composite
  unique index. No `.HasOne()...HasForeignKey()` relationships are defined.
  `EntryScoreEntity` has `EntryId` and `CategoryId` strings; `RelationEntity`
  has `CategoryId`, `EntryAId`, `EntryBId` strings. `EntryEntity` has
  `List<EntryScoreEntity> Scores` — EF Core infers a one-to-many via convention
  shadow properties, but there is zero explicit FK configuration. No
  `.OnDelete()` behavior specified. Manual cascade deletion in repository
  `DeleteAsync` methods (SqliteRepositories.cs:56-61, 173-177) compensates for
  missing FK enforcements.
- **Rule violated:** EF Core schema integrity — without explicit FK
  configuration, referential integrity is not enforced at the database level.
  SQLite has FK enforcement off by default unless `PRAGMA foreign_keys = ON` is
  set.
- **Remediation:** Add Fluent API relationships:
  - `EntryEntity.HasMany(e => e.Scores).WithOne().HasForeignKey(es => es.EntryId).OnDelete(DeleteBehavior.Cascade)`
  - `EntryScoreEntity.HasOne<CategoryEntity>().WithMany().HasForeignKey(es => es.CategoryId).OnDelete(DeleteBehavior.Cascade)`
  - `RelationEntity.HasIndex(r => r.CategoryId)` plus FK config
  - Enable foreign keys at connection open via connection string or startup SQL.
  - Remove manual cascade deletion code after FKs are configured.
- **Effort:** small

### EF-003 — Database restore overwrites file with active DbContext connection; swallowed exception masks data loss

- **Severity:** high
- **Category:** error-handling
- **Files:** `src/ContestJudging.Web/Program.cs:35`,
  `src/ContestJudging.Web/Program.cs:47`,
  `src/ContestJudging.Web/Program.cs:49`, `src/ContestJudging.Web/Program.cs:56`
- **Evidence:** The startup sequence at Program.cs:34-56 resolves the DbContext
  (opening a connection to `contest.db`), then at line 47 calls
  `ImportDataAsync()` which does `File.WriteAllBytesAsync("contest.db", data)` —
  raw file overwrite while the open connection may still reference the old file.
  The catch at line 49 swallows ALL exception types with
  `Console.WriteLine(ex.Message)` — no stack trace, no user notification, no
  rollback. `EnsureCreatedAsync()` at line 56 then runs against the potentially
  corrupted/stale connection.
- **Rule violated:** Best practice — never overwrite an open database file via
  raw filesystem I/O. Never swallow exceptions silently when data operations are
  at stake.
- **Remediation:** Restructure the startup:
  1. Check LocalStorage for backup BEFORE creating the DbContext scope.
  2. If backup exists, dispose of any existing DB connection, restore the file,
     then create a fresh DbContext.
  3. Log `ex.ToString()` instead of `ex.Message`.
  4. Set a flag so the UI can display a restore-failed warning to the user.
  5. Consider copying the file to a temp path first and atomically swapping.
- **Effort:** medium

### EF-004 — O(n*m) client-side join in SqliteEntryRepository queries (CQ-007 confirmed)

- **Severity:** medium
- **Category:** performance
- **Files:**
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:87`,
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:91`,
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:107`,
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:115`
- **Evidence:** Both `GetByIdAsync` and `GetAllAsync` load ALL categories via
  `_context.Categories.ToListAsync()` (lines 87, 107), then inside nested loops
  (lines 89-96, 110-122) perform a linear scan
  `categories.FirstOrDefault(c => c.Id == scoreEntity.CategoryId)` for every
  EntryScoreEntity. For N scores and M categories, this is O(N*M) client-side.
  In a contest with 50 entries x 5 categories = 250 scores, each category lookup
  scans 5 items = 1250 comparisons executed in C# rather than a SQL JOIN.
- **Rule violated:** EF Core query efficiency — prefer database-side JOINs over
  client-side correlation loops.
- **Remediation:** Add a `Category` navigation property to `EntryScoreEntity`:
  ```csharp
  public CategoryEntity? Category { get; set; }
  ```
  Configure the FK in `OnModelCreating`. Then use
  `.Include(e => e.Scores).ThenInclude(s => s.Category)` and access
  `scoreEntity.Category` directly. This emits a single SQL JOIN and eliminates
  the O(N*M) scan entirely.
- **Effort:** small
- **Related findings:** CQ-007

### EF-005 — Missing .AsNoTracking() on all read-only repository queries

- **Severity:** medium
- **Category:** performance
- **Files:**
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:30`,
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:80`,
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:103`,
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:200`
- **Evidence:** `GetAllAsync()` (lines 30, 103-105), `GetByIdAsync()` (lines
  80-82), and `GetByCategoryIdAsync()` (lines 200-202) materialize entity
  instances with change tracking enabled. These repositories only ever return
  domain objects mapped from the entities; the tracked entity instances are
  discarded after the method returns. The change tracker accumulates snapshots
  for every entity loaded, increasing memory pressure in Blazor WASM's
  constrained environment.
- **Rule violated:** EF Core performance best practice — use `.AsNoTracking()`
  for read-only queries to avoid change tracker overhead.
- **Remediation:** Add `.AsNoTracking()` to `GetAllAsync`, `GetByIdAsync`, and
  `GetByCategoryIdAsync` queries. Example:
  `await _context.Categories.AsNoTracking().ToListAsync();`. For `UpdateAsync`
  methods that mutate the entity, keep tracking enabled (which they already
  are).
- **Effort:** trivial

### EF-006 — CalculateGlobalScoresAsync performs individual entry updates without transaction wrapping

- **Severity:** medium
- **Category:** transactions
- **Files:** `src/ContestJudging.Services/Managers/ContestManager.cs:107`,
  `src/ContestJudging.Services/Managers/ContestManager.cs:112`
- **Evidence:** `CalculateGlobalScoresAsync` (ContestManager.cs:106-114)
  iterates over `entries` and calls `_entryRepository.UpdateAsync(entry)` for
  each one. `UpdateAsync` in SqliteEntryRepository (line 163) calls
  `SaveChangesAsync()` independently per entry. If the 3rd of 10 entries fails
  during update, entries 1-2 have modified scores while entries 3-10 retain
  their original values. There is no atomicity guarantee across the batch.
- **Rule violated:** ACID best practice — multi-entity mutations that represent
  a single logical operation should be wrapped in a transaction.
- **Remediation:** Inject a transaction-scoped operation or add a
  `BeginTransactionAsync()` / `CommitTransactionAsync()` pair in ContestManager.
  Alternatively, refactor to accept `IEnumerable<Entry>` and call
  `SaveChangesAsync()` once after all modifications (with change tracking
  enabled, modifications to loaded entities are batched). Since the repository's
  `UpdateAsync` currently calls SaveChangesAsync per entry, either remove
  SaveChangesAsync from the repository method or add a batch-update method.
- **Effort:** small

### EF-007 — Connection string hardcoded in Program.cs rather than sourced from configuration

- **Severity:** low
- **Category:** configuration
- **Files:** `src/ContestJudging.Web/Program.cs:66`
- **Evidence:** `"Data Source=contest.db"` is hardcoded as a string literal
  passed to `AddContestJudgingServices`. The extension method defaults to
  `"Data Source=:memory:"` (ServiceCollectionExtensions.cs:21) but this default
  is misleading since the caller always overrides it.
- **Rule violated:** Twelve-Factor App / .NET conventions — configuration should
  come from `appsettings.json`, environment variables, or configuration
  providers, not string literals.
- **Remediation:** Read the connection string from
  `builder.Configuration.GetConnectionString("ContestDb")` or an appsetting.
  Store the path in `appsettings.json`. This also makes swapping to `:memory:`
  for tests trivial.
- **Effort:** trivial

### EF-008 — Missing indexes on frequently queried foreign key columns

- **Severity:** low
- **Category:** schema-design
- **Files:**
  `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:54`
- **Evidence:** `RelationEntity.CategoryId` is used in WHERE clauses at
  SqliteRepositories.cs:57, cs:173, and cs:201 but has no index.
  `EntryScoreEntity.EntryId` is used in WHERE clause at cs:176. Without indexes,
  SQLite performs full table scans on these columns. In Contests with thousands
  of scores/relations, this becomes a measurable cost even in WASM.
- **Rule violated:** Database performance — index columns used in WHERE and JOIN
  clauses.
- **Remediation:** Add `.HasIndex(r => r.CategoryId)` on `RelationEntity` and
  `.HasIndex(es => es.EntryId)` on `EntryScoreEntity` (separate from the
  composite unique index). These can be added to the `OnModelCreating` method.
- **Effort:** trivial

### EF-009 — No error handling around SaveChangesAsync calls in repository methods

- **Severity:** low
- **Category:** error-handling
- **Files:**
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:38`,
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:47`,
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:64`,
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:140`,
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:163`,
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:180`,
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:228`,
  `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs:238`
- **Evidence:** All 8 `SaveChangesAsync()` calls in SqliteRepositories.cs lack
  try/catch blocks. Constraint violations (e.g., duplicate EntryScore on the
  unique composite index, line 62), concurrency conflicts, or I/O errors
  propagate as unhandled exceptions directly to the Blazor UI layer. While the
  validation layer should prevent invalid data from reaching persistence, no
  defense-in-depth exists.
- **Rule violated:** Defensive programming — persistence operations should catch
  and wrap data-access exceptions with domain-meaningful error types.
- **Remediation:** Wrap `SaveChangesAsync()` in try/catch. Catch
  `DbUpdateException` and inspect its inner exception to provide user-actionable
  messages (e.g., "An entry with that name already exists"). Re-throw as a
  custom domain exception or log with context.
- **Effort:** small

### EF-010 — EntryScoreEntity has redundant surrogate key alongside natural composite unique constraint

- **Severity:** low
- **Category:** schema-design
- **Files:**
  `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:24`,
  `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:59`
- **Evidence:** `EntryScoreEntity` has an auto-increment `int Id` as the primary
  key (line 26), and a unique composite index on `(EntryId, CategoryId)` (lines
  60-62). The composite (EntryId, CategoryId) is the natural key — an entry can
  have at most one score per category. The surrogate `Id` is never referenced by
  any other entity (no FK points to EntryScoreEntity's Id) and is never used in
  any query throughout the codebase.
- **Rule violated:** Schema normalization — avoid redundant surrogate keys when
  the natural key is sufficient and no referencing FKs require it.
- **Remediation:** Remove the `Id` property and make `(EntryId, CategoryId)` the
  composite primary key via
  `modelBuilder.Entity<EntryScoreEntity>().HasKey(es => new { es.EntryId, es.CategoryId });`.
  This simplifies the schema and enforces the uniqueness invariant at the PK
  level rather than a secondary unique index.
- **Effort:** trivial

### EF-011 — DbContext class holds file I/O methods (SRP violation)

- **Severity:** informational
- **Category:** architecture
- **Files:**
  `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:65`,
  `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:76`
- **Evidence:** `ContestDbContext` contains two methods, `ExportDatabaseAsync()`
  and `ImportDatabaseAsync()`, that use `System.IO.File` to read/write
  `contest.db` as raw bytes. These are not EF Core operations — they bypass the
  DbContext entirely to perform filesystem-level backup/restore. This gives the
  DbContext two unrelated responsibilities: data access and file I/O.
- **Rule violated:** Single Responsibility Principle — the DbContext should only
  be responsible for database access and entity modeling.
- **Remediation:** Move `ExportDatabaseAsync()` and `ImportDatabaseAsync()` into
  a separate `DatabaseBackupService` registered in DI. This service would accept
  the connection string or DB path and encapsulate the filesystem I/O. The
  existing `ContestManager` already delegates to these methods
  (ContestManager.cs:121, 126) so the refactor is localized.
- **Effort:** small

### EF-012 — No .IsRequired() or .HasMaxLength() configuration on string entity properties

- **Severity:** informational
- **Category:** schema-design
- **Files:**
  `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:54`
- **Evidence:** `OnModelCreating` configures only primary keys and one index.
  All string properties (`CategoryEntity.Id`, `EntryEntity.Id`,
  `EntryScoreEntity.EntryId`, `EntryScoreEntity.CategoryId`,
  `RelationEntity.CategoryId`, `RelationEntity.EntryAId`,
  `RelationEntity.EntryBId`) lack `.IsRequired()` and `.HasMaxLength()`. EF Core
  defaults strings to `nvarchar(max)` and nullable, which in SQLite maps to
  `TEXT` — functionally fine but semantically imprecise. The nullable default
  means non-null strings with `= string.Empty` initializers are misleading.
- **Rule violated:** EF Core schema precision — explicit column constraints
  improve model clarity and enable database-level validation.
- **Remediation:** Add `.IsRequired()` and `.HasMaxLength(256)` (or appropriate
  length) to all string PK/FK properties in `OnModelCreating`. This documents
  intent and generates more constrained DDL.
- **Effort:** trivial
