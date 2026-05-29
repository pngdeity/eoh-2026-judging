# R2-A Remediation Report — Pass 2, Batch 3: Architecture Cleanup

**Date:** 2026-05-26\
**Agent:** R2-A\
**Branch:** fix/audit-remediate

## Findings Addressed

| ID       | Severity | Description                                                             | Status |
| -------- | -------- | ----------------------------------------------------------------------- | ------ |
| ARCH-004 | Medium   | ContestDbContext SRP violation — mixes ORM config + raw file I/O        | Fixed  |
| ARCH-002 | High     | ContestManager takes concrete ContestDbContext — breaks layer isolation | Fixed  |
| EF-003   | High     | Database restore overwrites file while DbContext has active connection  | Fixed  |
| CQ-003   | High     | Swallowed exception in database restore path                            | Fixed  |

## Changes Made

### Step 1 — New interface: `IDatabaseBackupService`

- **File:** `src/ContestJudging.Core/Interfaces/IDatabaseBackupService.cs` (new)
- Defines `ExportAsync()` and `ImportAsync(byte[])` contracts for database
  backup operations.

### Step 2 — New implementation: `DatabaseBackupService`

- **File:**
  `src/ContestJudging.Infrastructure/Persistence/DatabaseBackupService.cs` (new)
- Implements `IDatabaseBackupService` with file I/O (read/write bytes).
- Includes SQLite magic-number validation on import (`"SQLite format 3\0"`).

### Step 3 — Removed Export/Import from `ContestDbContext`

- **File:** `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs`
- Removed `ExportDatabaseAsync()` and `ImportDatabaseAsync()` methods.
- Removed unused `System.IO` and `System.Threading.Tasks` usings.
- FK relationships (R1-B's work) remain intact.

### Step 4 — Fixed `ContestManager` layer isolation

- **File:** `src/ContestJudging.Services/Managers/ContestManager.cs`
- Removed `using ContestJudging.Infrastructure.Persistence;`.
- Replaced `ContestDbContext _context` field with
  `IDatabaseBackupService _backupService`.
- Constructor now takes `IDatabaseBackupService backupService` instead of
  `ContestDbContext context`.
- `ExportDataAsync()` delegates to `_backupService.ExportAsync()`.
- `ImportDataAsync()` delegates to `_backupService.ImportAsync()`.

### Step 5 — DI registration

- **File:**
  `src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs`
- Registered:
  `services.AddScoped<IDatabaseBackupService>(sp => new DatabaseBackupService("contest.db"))`.

### Step 6 — Fixed Program.cs restore ordering (EF-003 + CQ-003)

- **File:** `src/ContestJudging.Web/Program.cs`
- **EF-003 Fix:** Moved localStorage database restore BEFORE `IServiceScope`
  creation. The DbContext no longer exists when the database file is
  overwritten.
- **CQ-003 Fix:** Exception handling in restore path upgraded from `LogWarning`
  to `LogError`, preserving inner exception details.
- Backup restore now uses `IDatabaseBackupService` directly from `host.Services`
  root provider, not `IContestManager`.
- Schema version check (R1-B's work) retained.

### Step 7 — Updated tests

- **File:** `tests/ContestJudging.Tests/ContestManagerTests.cs`
- All 3 test methods now create `new Mock<IDatabaseBackupService>()` and pass
  `mockBackup.Object` to ContestManager constructor instead of `null!`.

## Build Result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Test Result

```
Passed!  - Failed: 0, Passed: 33, Skipped: 0, Total: 33
```

All 33 tests pass, including the 3 `ContestManagerTests` with
`Mock<IDatabaseBackupService>`.

## Verification

- `using ContestJudging.Infrastructure.Persistence` is no longer present in
  `ContestManager.cs` (confirmed via grep).
- `ContestDbContext` no longer has file I/O methods — ORM config only.
- `ContestManager` depends solely on `IDatabaseBackupService` interface — no
  Infrastructure direct dependency.
- Database restore runs before any `DbContext` scope, preventing concurrent
  connection overwrite.
