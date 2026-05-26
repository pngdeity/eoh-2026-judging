# P2-C: Blazor WebAssembly Deep Analysis Report

**Agent:** P2-C\
**Domain:** blazor-wasm\
**Pass:** 2\
**Status:** success\
**Date:** 2026-05-25

---

## Scope

All `.razor`, `.razor.cs`, `.cs`, `.css`, and `.html` files under
`src/ContestJudging.Web/` (10 `.razor` files, 3 `.razor.cs` code-behinds, 1
`Program.cs`, 1 `app.css`, 1 `index.html`). Plus relevant
Infrastructure/Services files traced for cross-layer analysis.

---

## Findings Summary

| ID     | Severity      | Title                                                                   |
| ------ | ------------- | ----------------------------------------------------------------------- |
| BW-001 | medium        | OnInitializedAsync methods lack exception handling                      |
| BW-002 | medium        | BackupDatabase invoked excessively on every Setup mutation              |
| BW-003 | medium        | No localStorage quota check before storing SQLite backup                |
| BW-004 | high          | Bootstrap JavaScript not loaded — accordion UI non-functional           |
| BW-005 | medium        | Interactive divs missing ARIA roles, keyboard handlers, and tabindex    |
| BW-006 | medium        | Large lists rendered without Virtualize component                       |
| BW-007 | medium        | DB restore path lacks integrity/schema verification (SEC-004 follow-up) |
| BW-008 | low           | Console.WriteLine used throughout instead of ILogger\<T\>               |
| BW-009 | low           | Orphaned weather.json scaffold file shipped to browser                  |
| BW-010 | informational | STRUCT-001 follow-up: Infrastructure leak depth analysis                |

---

## Detailed Findings

### BW-001: OnInitializedAsync methods lack exception handling

**Severity:** medium\
**Category:** lifecycle / error-handling

All three page components override `OnInitializedAsync` but have no try-catch
around their async data loading. If the SQLite database is corrupt, the
repository throws, or localStorage read fails, the exception propagates to
Blazor's global error boundary, producing a generic "An unhandled error has
occurred" page with a reload link. There is no component-scoped error recovery.

**Files:**

- `src/ContestJudging.Web/Pages/Setup.razor.cs:37` — `OnInitializedAsync` calls
  `RefreshData()` which calls `ExportDataAsync()` and multiple repository reads
- `src/ContestJudging.Web/Pages/Judging.razor.cs:44` — `OnInitializedAsync`
  calls `GetAllAsync()` on two repositories
- `src/ContestJudging.Web/Pages/Results.razor.cs:20` — `OnInitializedAsync`
  calls `GetAllAsync()` on two repositories

**Evidence:**

```csharp
// Setup.razor.cs:37-40
protected override async Task OnInitializedAsync()
{
    await RefreshData();  // no try-catch
}

// Judging.razor.cs:44-48
protected override async Task OnInitializedAsync()
{
    categories = (await CategoryRepository.GetAllAsync()).ToList();
    entries = (await EntryRepository.GetAllAsync()).ToList();  // no try-catch
}
```

**Remediation:** Wrap `OnInitializedAsync` in try-catch; set an `errorMessage`
field and display it in the UI rather than crashing to the global error
boundary. Or use `<ErrorBoundary>` around each page's content.

**Effort:** trivial

---

### BW-002: BackupDatabase invoked excessively on every Setup mutation

**Severity:** medium\
**Category:** performance

`Setup.RefreshData()` calls `BackupDatabase()` which reads the entire SQLite DB
file, base64-encodes it, and writes it to localStorage. `RefreshData()` is
called:

1. On every page load (`OnInitializedAsync`)
2. After every `AddCategory` / `DeleteCategory`
3. After every `AddEntry` / `DeleteEntry`
4. After every `BulkImportEntries`
5. After `ClearCategories` / `ClearEntries`

This means a user adding 20 entries triggers 20 full-database reads + base64
encodes + localStorage writes. For a WASM app running in-browser, this causes
unnecessary I/O churn and GC pressure.

**Files:**

- `src/ContestJudging.Web/Pages/Setup.razor.cs:42` — `BackupDatabase()` inside
  `RefreshData()`
- `src/ContestJudging.Web/Pages/Setup.razor.cs:49` — `ExportDataAsync()` reads
  entire DB file
- `src/ContestJudging.Web/Pages/Setup.razor.cs:55` — base64 encode +
  localStorage write

**Remediation:** Decouple backup from data refresh. Save to localStorage only on
page navigation away (in `Dispose` / `DisposeAsync`) or on a periodic debounced
timer. At minimum, add a dirty flag and only back up when data has actually
changed since the last save.

**Effort:** small

---

### BW-003: No localStorage quota check before storing SQLite backup

**Severity:** medium\
**Category:** data-loss

`BackupDatabase()` in both Setup and Judging unconditionally calls
`SetItemAsStringAsync("db_backup", Convert.ToBase64String(data))`. Browser
localStorage has a typical quota of 5-10MB per origin. A SQLite database with
hundreds of entries, categories, and relations can easily exceed this. If the
quota is exceeded, the `setItem` call will throw a `QuotaExceededError`, which
is not caught. The user loses their backup silently, and if they then clear
browser data or the tab crashes, data is lost.

**Files:**

- `src/ContestJudging.Web/Pages/Setup.razor.cs:55` — unchecked localStorage
  write
- `src/ContestJudging.Web/Pages/Judging.razor.cs:82` — unchecked localStorage
  write

**Evidence:**

```csharp
// Setup.razor.cs:52-55
var data = await ContestManager.ExportDataAsync();
if (data.Length > 0)
{
    await LocalStorage.SetItemAsStringAsync("db_backup", Convert.ToBase64String(data));
    // No QuotaExceededError handling
}
```

**Remediation:** Check `data.Length` against a safe threshold (e.g., 4MB) before
writing. Wrap `SetItemAsStringAsync` in try-catch for `QuotaExceededError`. Show
a user-visible warning when the backup cannot be saved, explaining that data may
be lost on tab close.

**Effort:** small

---

### BW-004: Bootstrap JavaScript not loaded — accordion UI non-functional

**Severity:** high\
**Category:** functionality

`index.html` references Bootstrap CSS but **no Bootstrap JavaScript bundle**.
The "Manual Override / Correction" section in `Judging.razor` uses Bootstrap's
accordion component with `data-bs-toggle="collapse"` and
`data-bs-target="#manualEntry"` attributes. Without Bootstrap JS, clicking the
accordion header does nothing — the panel never expands, making manual relation
entry inaccessible.

**Files:**

- `src/ContestJudging.Web/wwwroot/index.html:14` — no `<script>` for Bootstrap
  JS
- `src/ContestJudging.Web/Pages/Judging.razor:97` — `data-bs-toggle="collapse"`
  on accordion button

**Evidence:**

```html
<!-- index.html:9-11 -- only CSS loaded, no JS -->
<link rel="stylesheet" href="lib/bootstrap/dist/css/bootstrap.min.css" />
<link rel="stylesheet" href="css/app.css" />
<link href="ContestJudging.Web.styles.css" rel="stylesheet" />

<!-- Judging.razor:97 -->
<button
  class="accordion-button collapsed ..."
  type="button"
  data-bs-toggle="collapse"
  data-bs-target="#manualEntry"
>
  Manual Override / Correction
</button>
```

**Remediation:** Add
`<script src="lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>` to
`index.html` before the closing `</body>` tag. Alternatively, replace the
Bootstrap accordion with Blazor-native conditional rendering
(`@if (showManualEntry) { ... }`) to avoid the JS dependency altogether.

**Effort:** trivial

---

### BW-005: Interactive divs missing ARIA roles, keyboard handlers, and tabindex

**Severity:** medium\
**Category:** accessibility

Several `<div>` elements act as buttons via `@onclick` but lack the attributes
needed for screen readers and keyboard-only users:

1. Judge cards (Exhibit A / Exhibit B) in `Judging.razor:66,84` — `@onclick` on
   `<div>` with no `role="button"`, `tabindex="0"`, or `@onkeydown`
2. Tie button in `Judging.razor:74` — same issue
3. The keyboard handler container at `Judging.razor:6` has `tabindex="0"` and
   `@onkeydown` but no `role` attribute (should be `role="application"` or
   `role="region"`)
4. Navigation toggle button in `NavMenu.razor:4` has `title` but no `aria-label`

The keyboard shortcuts (A/S/D) work but are undocumented to screen reader users
(no `aria-keyshortcuts`).

**Files:**

- `src/ContestJudging.Web/Pages/Judging.razor:6` — keyboard container missing
  `role`
- `src/ContestJudging.Web/Pages/Judging.razor:66` — Exhibit A card missing
  `role="button"`, `tabindex`
- `src/ContestJudging.Web/Pages/Judging.razor:74` — tie button missing
  `role="button"`, `tabindex`
- `src/ContestJudging.Web/Pages/Judging.razor:84` — Exhibit B card missing
  `role="button"`, `tabindex`
- `src/ContestJudging.Web/Layout/NavMenu.razor:4` — toggle button missing
  `aria-label`

**Remediation:** Either replace judge-card `<div>` elements with `<button>`
elements (which get all ARIA semantics for free) or add `role="button"`,
`tabindex="0"`, `@onkeydown:Enter="() => RecordResult(...)"`,
`@onkeydown:Space="() => RecordResult(...)"`, and `aria-label` to each. Add
`aria-keyshortcuts="A S D"` to the judging container.

**Effort:** small

---

### BW-006: Large lists rendered without Virtualize component

**Severity:** medium\
**Category:** performance

None of the four list-rendering sections in the app use Blazor's built-in
`<Virtualize>` component, despite
`Microsoft.AspNetCore.Components.Web.Virtualization` being available in
`_Imports.razor`. For contests with 200+ entries, rendering all DOM nodes at
once causes sluggish initial render and high memory usage in the browser.

**Files:**

- `src/ContestJudging.Web/Pages/Setup.razor:35` —
  `@foreach (var cat in categories)` — no virtualization
- `src/ContestJudging.Web/Pages/Setup.razor:67` —
  `@foreach (var entry in entries)` — no virtualization
- `src/ContestJudging.Web/Pages/Judging.razor:162` —
  `@foreach (var rel in relations)` — no virtualization
- `src/ContestJudging.Web/Pages/Results.razor:55` —
  `@foreach (var item in leaderboard)` — no virtualization

**Remediation:** Replace `@foreach` loops with
`<Virtualize Items="@categories" Context="cat">` for lists that can exceed 50
items. For Setup lists where all items should be visible (small N), this is
optional; for Relations and Leaderboard, Virtualize provides a measurable
benefit.

**Effort:** small

---

### BW-007: DB restore path lacks integrity/schema verification (SEC-004 follow-up)

**Severity:** medium\
**Category:** data-integrity

When the app starts, `Program.cs:39-53` reads a base64-encoded SQLite file from
localStorage and writes it directly to the filesystem via `ImportDataAsync`,
then calls `EnsureCreatedAsync()`. Several risks compound here:

1. **No HMAC/checksum** (SEC-004) — tampered data is accepted silently
2. **No schema version check** — if the app schema changes between versions, an
   old backup restores successfully but subsequent EF Core operations will fail
   with obscure SQLite errors (e.g., missing columns)
3. **No basic SQLite validity check** — `ImportDataAsync` writes raw bytes to
   disk without verifying the SQLite header magic (`SQLite format 3\0`)
4. **EnsureCreatedAsync is insufficient** — it checks if tables exist but does
   not verify column schemas match the current EF Core model

A corrupted or version-mismatched backup causes hard-to-diagnose failures at
runtime rather than on restore.

**Files:**

- `src/ContestJudging.Web/Program.cs:39` — restore logic
- `src/ContestJudging.Web/Program.cs:46` — `Convert.FromBase64String` with no
  length/sanity check
- `src/ContestJudging.Web/Program.cs:47` — `ImportDataAsync` with no
  post-restore validation
- `src/ContestJudging.Web/Program.cs:56` — `EnsureCreatedAsync` after restore
  without schema verification
- `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:76` —
  `ImportDatabaseAsync` writes bytes with no validation

**Related:** SEC-004

**Remediation:**

1. Store a schema version number alongside the backup (or prepend it to the
   stored string)
2. On restore, compare the backup's schema version to the current app's expected
   version; reject mismatches
3. Validate the first 16 bytes of the raw SQLite file against the magic header
   before writing
4. After restore, attempt a simple `SELECT 1` query to confirm the DB is
   functional before proceeding

**Effort:** small

---

### BW-008: Console.WriteLine used throughout instead of ILogger\<T\>

**Severity:** low\
**Category:** maintainability / observability

Three locations use `Console.WriteLine` for error reporting and tracing. Blazor
WASM supports `ILogger<T>` via `Microsoft.Extensions.Logging`. Console output is
unstructured, cannot be filtered by log level, and is invisible to monitoring
tools.

**Files:**

- `src/ContestJudging.Web/Program.cs:51` —
  `Console.WriteLine($"Failed to restore database: {ex.Message}")`

**Evidence:**

```csharp
catch (Exception ex)
{
    Console.WriteLine($"Failed to restore database: {ex.Message}");
}
```

Additionally, comments like `// TRICKY OPTIMIZATION #2: Save to LocalStorage`
(Setup.razor.cs:51, Judging.razor.cs:78, Program.cs:38) describe implementation
details that would be better served by `ILogger.LogDebug()` calls.

**Remediation:** Inject `ILogger<T>` into startup code and components. Replace
`Console.WriteLine` with `_logger.LogError(ex, "Failed to restore database")` to
capture the full stack trace.

**Effort:** trivial

---

### BW-009: Orphaned weather.json scaffold file shipped to browser

**Severity:** low\
**Category:** dead-code

`wwwroot/sample-data/weather.json` is a leftover from the
`dotnet new blazorwasm` template. It contains hardcoded weather data and serves
no purpose in this application. However, since it's in `wwwroot/`, it is
published and becomes part of the browser download payload (albeit small).

**Files:**

- `src/ContestJudging.Web/wwwroot/sample-data/weather.json:1`

**Remediation:** Delete the file and the containing `sample-data/` directory.

**Effort:** trivial

---

### BW-010: STRUCT-001 follow-up — Infrastructure leak depth analysis

**Severity:** informational\
**Category:** architecture

STRUCT-001 flagged that `Web.csproj` directly references
`ContestJudging.Infrastructure`. This is the depth analysis:

**What files import Infrastructure types?**

Only `Program.cs`:

- Line 5: `using ContestJudging.Infrastructure.Persistence;`
- Line 34:
  `var context = scope.ServiceProvider.GetRequiredService<ContestDbContext>();`

No `.razor` or `.razor.cs` files reference Infrastructure namespaces or types.
Page code-behinds (Setup, Judging, Results) only import
`ContestJudging.Core.Entities`, `ContestJudging.Core.Interfaces.Repositories`,
`ContestJudging.Services.Managers`, and `ContestJudging.Services.Partitioning`.

**What in the .csproj creates the leak?**

- Line 29: `<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />`
- Line 30: `<PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" />`
- Line 36: `<ProjectReference Include="..\ContestJudging.Infrastructure\..." />`

**Root cause:** `Program.cs` needs `ContestDbContext` to call
`EnsureCreatedAsync()` because the
`ServiceCollectionExtensions.AddContestJudgingServices()` method does not
perform database initialization. It only registers services. If
`AddContestJudgingServices` handled `EnsureCreatedAsync` internally (e.g., via a
hosted service or an `InitializeAsync` method on `IContestManager`), Program.cs
would not need to resolve `ContestDbContext` directly, and the Infrastructure
reference could be removed from Web.csproj entirely.

**Files:**

- `src/ContestJudging.Web/Program.cs:5` — Infrastructure import
- `src/ContestJudging.Web/Program.cs:34` — direct `ContestDbContext` resolution
- `src/ContestJudging.Web/ContestJudging.Web.csproj:29` — EF Core Sqlite
  PackageReference
- `src/ContestJudging.Web/ContestJudging.Web.csproj:30` — SQLitePCLRaw
  PackageReference
- `src/ContestJudging.Web/ContestJudging.Web.csproj:36` — Infrastructure
  ProjectReference

**Related:** STRUCT-001

---

## Metrics

| Metric               | Value                   |
| -------------------- | ----------------------- |
| Files scanned        | 17                      |
| Findings count       | 10                      |
| .razor files         | 10                      |
| .razor.cs files      | 3                       |
| .cs files (Web only) | 1 (Program.cs)          |
| wwwroot files        | 2 (index.html, app.css) |

---

## Areas Without Findings (Clean)

- **async void:** None found. All lifecycle methods use `async Task`.
- **OnParametersSet:** No components override it — no expensive recomputation
  risk.
- **Dispose/IDisposable:** No components need IDisposable — no event
  subscriptions, and scoped services are container-managed.
- **StateHasChanged:** No explicit calls needed — Blazor's async state machine
  handles re-rendering after `await`.
- **Subscription leaks:** No C# event subscriptions (`+=`) in any component.
- **IJSRuntime/JSRuntime:** No IJSRuntime calls anywhere in the Web project —
  zero JS interop.
- **Race conditions from concurrent user interaction:** Blazor WASM's
  synchronization context serializes UI events. The single-threaded nature
  prevents true concurrency races. No cross-component mutable shared state
  exists.
- **CSS/JS isolation:** Bootstrap CSS and app.css load in `<head>` but do not
  block rendering (Blazor WASM boot is async). No render-blocking patterns.
- **State isolation:** Components do not share mutable state directly; all state
  is in private fields. Data persistence goes through the database. The only
  shared concern is backup/restore race (BW-003 covers the quota issue; no
  explicit cross-component race exists).
