# ST-1 Remediation Report — Tier 2 Blazor WASM Findings

Branch: `fix/audit-remediate`

## BW-001: No error handling in OnInitializedAsync

**Files changed:**

- `src/ContestJudging.Web/Pages/Judging.razor.cs`
- `src/ContestJudging.Web/Pages/Setup.razor.cs`
- `src/ContestJudging.Web/Pages/Results.razor.cs`

**Changes:**

- Wrapped each `OnInitializedAsync` body in try/catch blocks catching
  `Exception`.
- On catch, logs via
  `Logger.LogError(ex, "Failed to initialize {Page}", GetType().Name)` and sets
  `errorMessage` to a user-friendly fallback string.
- Injected `ILogger<Judging>`, `ILogger<Setup>`, and `ILogger<Results>` via
  `[Inject]` properties in each code-behind class.
- Added `using Microsoft.Extensions.Logging;` to all three files.

## BW-003: No localStorage quota check

**File changed:** `src/ContestJudging.Services/Managers/BackupService.cs`

**Changes:**

- Added a size gate in `SaveBackupAsync`: if the base64 string exceeds 5 MB (5 *
  1024 * 1024 chars), the method logs a warning with the size and returns early
  without writing to localStorage — preventing silent truncation.
- The check occurs after `Convert.ToBase64String(dbData)` and before
  `_localStorage.SetItemAsync`.

## BW-002: Excessive backup calls

**Files changed:**

- `src/ContestJudging.Web/Pages/Setup.razor.cs`
- `src/ContestJudging.Web/Pages/Judging.razor.cs`

**Changes:**

- Removed `await BackupDatabase()` calls from `RefreshData()` (Setup) and
  `RefreshRelations()` (Judging), which previously caused a backup on every
  single CRUD operation.
- Added a `_needsBackup` boolean flag to both page classes.
- Each data-mutating method (`ClearCategories`, `ClearEntries`,
  `BulkImportEntries`, `AddCategory`, `DeleteCategory`, `AddEntry`,
  `DeleteEntry` in Setup; `RecordResult`, `AddRelation`, `DeleteRelation` in
  Judging) sets `_needsBackup = true` instead of triggering a backup
  immediately.
- Both pages implement `IAsyncDisposable`. In `DisposeAsync`, if `_needsBackup`
  is true, the backup is saved once. Errors during dispose-backup are caught and
  logged.
- This debounces backups: one save on component teardown instead of N saves
  across N mutations.

## BW-005: Missing ARIA roles

**File changed:** `src/ContestJudging.Web/Pages/Judging.razor`

**Changes on the three interactive quick-judge divs:**

- **Exhibit A card (left):** Added `role="button"`, `tabindex="0"`,
  `aria-label="Vote {A} better than {B}"`, and `@onkeydown` handler to support
  Enter/Space activation.
- **Tie button (center):** Added `role="button"`, `tabindex="0"`,
  `aria-label="Mark {A} and {B} as equal"`, and `@onkeydown` handler for
  Enter/Space.
- **Exhibit B card (right):** Added `role="button"`, `tabindex="0"`,
  `aria-label="Vote {B} better than {A}"`, and `@onkeydown` handler for
  Enter/Space.

**Code-behind addition** (`Judging.razor.cs`): Added
`HandleCardKeyDown(KeyboardEventArgs e, Operator resultOp)` method that checks
for `Enter` or `Space` keys and calls `RecordResult`.

## BW-006: No Virtualize on large lists

**Files changed:**

- `src/ContestJudging.Web/Pages/Setup.razor`
- `src/ContestJudging.Web/Pages/Judging.razor`
- `src/ContestJudging.Web/Pages/Results.razor`
- `src/ContestJudging.Web/Pages/Results.razor.cs`

**Changes:**

- **Setup.razor:** Replaced `@foreach (var cat in categories)` with
  `<Virtualize Items="@categories" Context="cat">` in the categories list.
  Replaced `@foreach (var entry in entries)` with
  `<Virtualize Items="@entries" Context="entry">` in the entries list.
- **Judging.razor:** Replaced `@foreach (var rel in relations)` in the relations
  table body with `<Virtualize Items="@relations" Context="rel">`.
- **Results.razor:** Replaced `@foreach (var item in leaderboard)` in the
  leaderboard table body with
  `<Virtualize Items="@leaderboard" Context="item">`.
- **Results.razor.cs:** Added `Rank` property to `LeaderboardItem` class and
  populates it during `CalculateResults()` via a post-sort loop. The Virtualize
  context uses `item.Rank` instead of a locally-scoped `rank` variable that
  would not work inside Virtualize.

## BW-008: Console.WriteLine remaining

**Files changed:** None

**Verification:**

- `rg "Console\.WriteLine" src/` returned zero matches across the entire
  solution. No `Console.WriteLine` calls exist in any page code-behind or
  anywhere in the `src/` tree. Finding is already resolved.

## Build Verification

```
dotnet build src/ContestJudging.Web/ContestJudging.Web.csproj -c Release
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
