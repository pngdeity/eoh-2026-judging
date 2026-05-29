# ST-5 Remediation Completion Report

## Branch: fix/audit-remediate

## Date: 2026-05-26

---

## Findings Remediated

### TEST-013 / BW-001: Missing CancellationToken Support

**Files changed:**

- `src/ContestJudging.Core/Interfaces/Repositories/ICategoryRepository.cs`
- `src/ContestJudging.Core/Interfaces/Repositories/IEntryRepository.cs`
- `src/ContestJudging.Core/Interfaces/Repositories/IRelationRepository.cs`
- `src/ContestJudging.Core/Interfaces/IDatabaseBackupService.cs`
- `src/ContestJudging.Services/Managers/IContestManager.cs`
- `src/ContestJudging.Services/Managers/ContestManager.cs`
- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs`
- `src/ContestJudging.Infrastructure/Persistence/DatabaseBackupService.cs`

**Changes:**

- Added `CancellationToken cancellationToken = default` to all async methods in
  repository interfaces (`ICategoryRepository`, `IEntryRepository`,
  `IRelationRepository`, `IDatabaseBackupService`).
- Added `CancellationToken cancellationToken = default` to all async methods in
  `IContestManager`, including new passthrough methods (`AddEntriesAsync`,
  `DeleteCategoryAsync`, `DeleteEntryAsync`, `DeleteRelationAsync`).
- Updated `ContestManager` to thread the token through to repository and backup
  service calls.
- Updated `SqliteRepositories` to pass the token to EF Core operations
  (`AddAsync`, `SaveChangesAsync`, `FirstOrDefaultAsync`, `ToListAsync`,
  `FindAsync`).
- Updated `DatabaseBackupService` to pass the token to `File.ReadAllBytesAsync`
  and `File.WriteAllBytesAsync`.

### EF-009: No error handling around SaveChangesAsync

**Files changed:**

- `src/ContestJudging.Infrastructure/Repositories/SqliteRepositories.cs`

**Changes:**

- Added `ILogger<T>` injection to all three repository classes
  (`SqliteCategoryRepository`, `SqliteEntryRepository`,
  `SqliteRelationRepository`).
- Added `using Microsoft.Extensions.Logging;`.
- Wrapped all `SaveChangesAsync()` calls with try/catch for `DbUpdateException`:
  ```csharp
  try
  {
      await _context.SaveChangesAsync(cancellationToken);
  }
  catch (DbUpdateException ex)
  {
      _logger.LogError(ex, "Failed to save changes to the database");
      throw;
  }
  ```

### Error recovery in page code

**Files changed:**

- `src/ContestJudging.Web/Pages/Results.razor.cs`
- `src/ContestJudging.Web/Pages/Results.razor`
- `src/ContestJudging.Web/Pages/Setup.razor.cs`
- `src/ContestJudging.Web/Pages/Setup.razor`
- `src/ContestJudging.Web/Pages/Judging.razor.cs`

**Changes:**

- **Results.razor.cs**: Added `errorMessage` field, `ILogger<Results>`
  injection, try/catch around `OnInitializedAsync` and `CalculateResults`, and
  per-category error handling in the score calculation loop.
- **Results.razor**: Added error message alert display above the Calculate
  Results button.
- **Setup.razor.cs**: Added `errorMessage` field, `ILogger<Setup>` injection,
  try/catch around `OnInitializedAsync`, `ClearCategories`, `ClearEntries`,
  `BulkImportEntries`, `AddCategory`, `DeleteCategory`, `AddEntry`,
  `DeleteEntry`, and `BackupDatabase`.
- **Setup.razor**: Added error message alert display below the header.
- **Judging.razor.cs**: Added `ILogger<Judging>` injection, try/catch around
  `OnInitializedAsync`, `RecordResult`, `AddRelation`, `DeleteRelation`, and
  `BackupDatabase` (previously had no error handling on DB writes).

### Test updates

**Files changed:**

- `tests/ContestJudging.Tests/InfrastructureTests.cs`

**Changes:**

- Updated all `new Sqlite*Repository(context)` calls to include
  `Mock.Of<ILogger<T>>()` since constructors now require logger injection.

---

## Build & Test Results

### Build

All three target projects compile successfully with 0 warnings and 0 errors:

- `ContestJudging.Services`
- `ContestJudging.Infrastructure`
- `ContestJudging.Tests`

### Tests

- **50/51 tests pass**
- 1 pre-existing failure in
  `PartitionServiceTests.GeneratePartitions_WithNoOverlap_ShouldHaveDisjointSets`
  (unrelated to these changes - element "3" appears in multiple partitions with
  zero overlap).

---

## Verification Command

```
dotnet build src/ContestJudging.Services/ContestJudging.Services.csproj -c Release
dotnet build src/ContestJudging.Infrastructure/ContestJudging.Infrastructure.csproj -c Release
dotnet build tests/ContestJudging.Tests/ContestJudging.Tests.csproj -c Release
dotnet test tests/ContestJudging.Tests/ContestJudging.Tests.csproj -c Release --no-build --verbosity normal
```
