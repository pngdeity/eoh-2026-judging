# Re-Audit Report: Blazor WASM (P2-C)

**Agent:** PA2-C\
**Date:** 2026-05-26\
**Branch:** `fix/audit-remediate`\
**Base Report:** `reports/audit/findings-P2-blazor-wasm.json`

---

## Summary

| Metric                    | Value                                                      |
| ------------------------- | ---------------------------------------------------------- |
| Original findings         | 10 (BW-001 through BW-010)                                 |
| Fully fixed               | 2 (BW-004, BW-008)                                         |
| Partially fixed           | 1 (BW-007)                                                 |
| Still open                | 7 (BW-001, BW-002, BW-003, BW-005, BW-006, BW-009, BW-010) |
| New issues                | 2 (RA-BW-001, RA-BW-002)                                   |
| Original severity reduced | 0                                                          |

---

## Verified Fixes

### BW-004: Bootstrap accordion replaced with Blazor-native toggle — FIXED

| Check                                     | File                   | Result                                                                  |
| ----------------------------------------- | ---------------------- | ----------------------------------------------------------------------- |
| No `data-bs-toggle="collapse"` attributes | `Judging.razor`        | Confirmed — none present                                                |
| `showManualOverride` field exists         | `Judging.razor.cs:35`  | Confirmed — `private bool showManualOverride;`                          |
| `@onclick` toggle on accordion button     | `Judging.razor:97-101` | Confirmed — `@onclick="() => showManualOverride = !showManualOverride"` |
| `@if (showManualOverride)` conditional    | `Judging.razor:103`    | Confirmed — body wrapped in `@if (showManualOverride)`                  |
| No Bootstrap JS `<script>` tag added      | `index.html`           | Confirmed — no `<script src=...bootstrap...>`                           |
| Only Bootstrap CSS referenced             | `index.html:10`        | Confirmed — only `<link rel="stylesheet" ... bootstrap.min.css" />`     |

**Verdict:** The Bootstrap accordion has been fully replaced with a
Blazor-native `@if` toggle. No Bootstrap JS dependency was introduced. The
`accordion-button` CSS class is retained for visual styling (chevron icon via
`::after` pseudo-element), and the `.collapsed` class toggle correctly controls
the icon rotation — this is purely CSS and does not require Bootstrap JS. No
visual regression expected.

### BW-008: Console.WriteLine replaced with ILogger — FIXED

| Check                                  | File                  | Result                                             |
| -------------------------------------- | --------------------- | -------------------------------------------------- |
| No `Console.WriteLine` in restore path | `Program.cs`          | Confirmed — restore logic moved to `BackupService` |
| Uses `_logger.LogWarning`              | `BackupService.cs:37` | Confirmed                                          |
| Uses `_logger.LogInformation`          | `BackupService.cs:53` | Confirmed                                          |
| Uses `_logger.LogError(ex, ...)`       | `BackupService.cs:56` | Confirmed                                          |

**Verdict:** All error/log output now routed through `ILogger<BackupService>`.

---

## Partially Fixed

### BW-007: Restore integrity verification — PARTIALLY FIXED

| Check                                | File                             | Result                                                                                                                        |
| ------------------------------------ | -------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| Schema version check                 | `BackupService.cs:34-41`         | Confirmed — stores `db_schema_version` in localStorage, compares on restore, discards on mismatch                             |
| SQLite header magic bytes validation | `DatabaseBackupService.cs:23-30` | Confirmed — validates first 16 bytes against `"SQLite format 3\0"`                                                            |
| Post-restore DB integrity check      | `DatabaseBackupService.cs:31`    | **MISSING** — no `PRAGMA integrity_check` or `SELECT 1` after `WriteAllBytesAsync` to verify the restored file is not corrupt |

**Verdict:** Schema versioning (2 tiers) and SQLite magic bytes check are
sufficient for basic integrity. However, the SQLite file could pass the header
check and still contain corrupt pages. A post-restore integrity check (e.g.,
open a connection and run `PRAGMA integrity_check`) would provide
defense-in-depth. Current posture: **acceptable for the threat model but not
comprehensive**.

---

## Still Open

### BW-001: No exception handling in OnInitializedAsync — STILL OPEN

All three page components lack try-catch in their lifecycle methods:

| File               | Line  | Current Code                                      |
| ------------------ | ----- | ------------------------------------------------- |
| `Setup.razor.cs`   | 36-39 | `await RefreshData();` (no try-catch)             |
| `Judging.razor.cs` | 44-48 | `categories = ...; entries = ...;` (no try-catch) |
| `Results.razor.cs` | 20-24 | `categories = ...; entries = ...;` (no try-catch) |

**Severity:** Medium\
**Impact:** Unhandled exceptions in lifecycle methods hit the global error
boundary instead of showing a user-friendly error.

### BW-002: Excessive backup calls — STILL OPEN

The `IBackupService` abstraction did not change the calling pattern:

| File               | Backups triggered per action                                                                                                                                                      |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Setup.razor.cs`   | OnInitializedAsync + AddCategory + DeleteCategory + AddEntry + DeleteEntry + ClearCategories + ClearEntries + BulkImportEntries — each calls `RefreshData()` → `BackupDatabase()` |
| `Judging.razor.cs` | OnCategoryChanged + RecordResult + AddRelation + DeleteRelation — each calls `RefreshRelations()` → `BackupDatabase()`                                                            |

**Severity:** Medium\
**Impact:** Full DB export + base64 encode + localStorage write on every CRUD
operation. The `IBackupService` abstraction cleaned up the code but did not
address the excessive invocation pattern.

### BW-003: No localStorage quota check — STILL OPEN

| File               | Line  | Issue                                                                                                           |
| ------------------ | ----- | --------------------------------------------------------------------------------------------------------------- |
| `BackupService.cs` | 21-26 | `SaveBackupAsync` performs no size threshold check before `SetItemAsync`, no try-catch for `QuotaExceededError` |

**Severity:** Medium\
**Impact:** Uncaught quota errors can silently fail backup persistence.

### BW-005: Missing ARIA roles on interactive elements — STILL OPEN

| File            | Line  | Issue                                                                                        |
| --------------- | ----- | -------------------------------------------------------------------------------------------- |
| `Judging.razor` | 64-71 | `<div class="judge-card" @onclick>` — no `role="button"`, no `tabindex="0"`, no `@onkeydown` |
| `Judging.razor` | 73-80 | `<div class="tie-button" @onclick>` — no `role="button"`, no `tabindex="0"`, no `@onkeydown` |
| `Judging.razor` | 82-89 | `<div class="judge-card" @onclick>` — same as above                                          |
| `NavMenu.razor` | 4     | `<button title="..." @onclick>` — has `title` but no explicit `aria-label`                   |

**Severity:** Medium\
**Impact:** Keyboard-only users cannot activate judge cards or tie button.
Screen readers cannot identify them as interactive controls.

### BW-006: Large lists without Virtualize — STILL OPEN

| File            | Line    | Pattern                                                  |
| --------------- | ------- | -------------------------------------------------------- |
| `Setup.razor`   | 35-42   | `@foreach (var cat in categories)` — categories list     |
| `Setup.razor`   | 67-74   | `@foreach (var entry in entries)` — entries list         |
| `Judging.razor` | 165-181 | `@foreach (var rel in relations)` — relations table      |
| `Results.razor` | 55-67   | `@foreach (var item in leaderboard)` — leaderboard table |

**Severity:** Medium\
**Impact:** 200+ items cause excessive DOM nodes and slow initial render.

### BW-009: Orphaned weather.json — STILL OPEN

`src/ContestJudging.Web/wwwroot/sample-data/weather.json` still exists.

### BW-010: Infrastructure leak — STILL OPEN

`Program.cs:5` still imports `ContestJudging.Infrastructure.Persistence`, and
lines 34-38 still create a scope and call `EnsureCreatedAsync()` on
`ContestDbContext` directly. The restore path has been abstracted via
`IBackupService` (improvement), but the Infrastructure references remain.

---

## New Issues

### RA-BW-001: Scoped service resolved from root provider (DI antipattern)

| Severity | Category  | File         | Line |
| -------- | --------- | ------------ | ---- |
| Low      | lifecycle | `Program.cs` | 31   |

`IBackupService` is registered as scoped (`ServiceCollectionExtensions.cs:36`)
but resolved from `host.Services` (the root provider) on line 31 before any
scope is created:

```csharp
var backupService = host.Services.GetRequiredService<IBackupService>();
```

In Blazor WebAssembly, the root provider can resolve scoped services (it wraps
in an implicit scope), so this works in practice. However, it is an antipattern
that:

- Bypasses the intended scoped lifetime management
- Could cause issues if `IBackupService` or its dependencies
  (`IDatabaseBackupService`, `ILocalStorageService`) were ever refactored to use
  scoped resources that require explicit disposal

**Recommendation:** Wrap the restore call in a scope:

```csharp
using (var scope = host.Services.CreateScope())
{
    var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
    await backupService.TryRestoreBackupAsync();
}
```

### RA-BW-002: No post-restore DB integrity validation (BW-007 residual)

| Severity | Category       | File                       | Line |
| -------- | -------------- | -------------------------- | ---- |
| Low      | data-integrity | `DatabaseBackupService.cs` | 31   |

The `ImportAsync` method validates the SQLite header magic bytes (good) but does
not verify the restored database is internally consistent. A corrupted page
could pass the header check.

**Recommendation:** After `WriteAllBytesAsync`, open a temporary connection and
run `PRAGMA integrity_check` or `SELECT 1` to confirm the file is a usable
database.

---

## Architecture Observations

### BackupService Design Quality

The `IBackupService` / `BackupService` abstraction is well-designed:

- Clean interface with `SaveBackupAsync` and `TryRestoreBackupAsync`
- Schema versioning with safe fallback (discard on mismatch)
- Proper ILogger integration
- Proper SQLite magic bytes validation in `DatabaseBackupService`

The only gap is the post-restore integrity check (RA-BW-002) and the unchanged
excessive calling pattern (BW-002).

### Program.cs Startup Flow

The restructured startup flow is correct:

```
1. Build host
2. TryRestoreBackupAsync() — restore DB from localStorage if available
3. CreateScope + EnsureCreatedAsync() — create schema if DB doesn't exist
4. Run app
```

This is a cleaner separation than the original (where restore was inside the
scope). No race conditions or ordering issues identified.

---

## Blazor-Native Accordion: Visual Regression Analysis

The accordion toggle at `Judging.razor:97-101` retains the `accordion-button`
CSS class. Bootstrap's accordion styling uses:

- `::after` pseudo-element with SVG background image for the chevron
- `.collapsed` class to control `transform: rotate()` and `background-image` of
  `::after`
- `:not(.collapsed)` to set active colors

These are all **pure CSS** properties — no JavaScript required. The toggle is:

```csharp
class="accordion-button @(showManualOverride ? "" : "collapsed")"
```

When expanded (`showManualOverride=true`), `.collapsed` is removed → chevron
rotates → active colors apply. When collapsed, `.collapsed` is present → chevron
points down → default colors.

**No visual regression detected.** The styling is entirely CSS-driven.

---

## Overall Health Assessment

| Dimension              | Score     | Notes                                                                      |
| ---------------------- | --------- | -------------------------------------------------------------------------- |
| Functional correctness | Improved  | BW-004 accordion fix works correctly                                       |
| Backup architecture    | Improved  | Clean `IBackupService` abstraction, schema versioning, SQLite validation   |
| Performance            | Unchanged | BW-002 (excessive backup calls) and BW-006 (missing Virtualize) unresolved |
| Error handling         | Unchanged | BW-001 (no try-catch in lifecycle) unresolved                              |
| Accessibility          | Unchanged | BW-005 (missing ARIA) unresolved                                           |
| Data integrity         | Improved  | BW-007 partially addressed; schema version + header validation added       |
| Code quality           | Improved  | BW-008 (ILogger) fixed; scaffolding removed from restore path              |
| Dead code              | Unchanged | BW-009 weather.json still present                                          |
| Architecture           | Unchanged | BW-010 infrastructure leak still present                                   |

**Cumulative risk reduction from original P2 audit: ~20%** (2/10 fully fixed,
1/10 partially fixed, 7/10 still open).
