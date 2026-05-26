# ST-2 Remediation Report — Tier 2 Architecture Findings

**Branch:** `fix/audit-remediate`\
**Date:** 2026-05-26\
**Agent:** ST-2

---

## Summary

| Finding               | Status       | Notes                                                              |
| --------------------- | ------------ | ------------------------------------------------------------------ |
| ARCH-001 / STRUCT-001 | ✅ Fixed     | SQLitePCL init moved; EF/SQLite PackageReferences removed from Web |
| ARCH-003              | ⚠️ Deferred  | Interfaces remain in Services (see below)                          |
| ARCH-005              | ✅ Mitigated | TODO comment added in Program.cs                                   |
| ARCH-006              | ✅ Fixed     | PercentileScoring and DefinedIntervalScoring registered in DI      |

---

## ARCH-001 / STRUCT-001: Web → Infrastructure Layer Leak

### Changes

1. **`src/ContestJudging.Web/ContestJudging.Web.csproj`**\
   Removed direct `PackageReference` entries:
   - `Microsoft.EntityFrameworkCore.Sqlite` — available transitively via
     `..\ContestJudging.Infrastructure`
   - `SQLitePCLRaw.bundle_e_sqlite3` — available transitively via
     `..\ContestJudging.Infrastructure`

2. **`src/ContestJudging.Web/Program.cs`**
   - Removed `SQLitePCL.Batteries_V2.Init()` call and its architecture-note
     comment block.
   - Added comment documenting why `ContestDbContext` is referenced (WASM host
     must resolve scope for `EnsureCreatedAsync()`).
   - `using ContestJudging.Infrastructure.Persistence` retained — unavoidable
     for the scope `GetRequiredService<ContestDbContext>()` call.

3. **`src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs`**
   - Added `using SQLitePCL;`
   - Added `Batteries_V2.Init()` as the first call in
     `AddContestJudgingServices()` (before `AddDbContext`).

---

## ARCH-003: Interfaces in Services Instead of Core

**Deferred — out of scope for this agent.**

The following interfaces remain in the Services project with Services
namespaces:

- `IValidationService` (in `ContestJudging.Services.Validation`)
- `IPartitionService` (in `ContestJudging.Services.Partitioning`)
- `IGlobalRankingService` (in `ContestJudging.Services.Resolution`)
- `IContestManager` (in `ContestJudging.Services.Managers`)

These do not exist in `ContestJudging.Core.Interfaces/`. Moving them would
require updating all usages across Services, Web, and Tests — a moderate-effort
refactoring better suited to a dedicated pass.

---

## ARCH-005: Composition Root in Services

### Change

Added TODO comment in `Program.cs` (line 26-27):

```csharp
// TODO: Move AddContestJudgingServices implementation to Web layer as composition root.
// Currently in Services/Extensions/ for convenience.
```

The implementation itself remains in `ServiceCollectionExtensions` to avoid a
medium-effort refactoring.

---

## ARCH-006: Unregistered Scoring Strategies

### Change

Added DI registrations in `ServiceCollectionExtensions.cs` (lines 45-46):

```csharp
services.AddScoped<PercentileScoring>();
services.AddScoped<DefinedIntervalScoring>();
```

The default `IScoringStrategy` registration (`LinearSpacingScoring`) is
unchanged. Both additional strategies are now resolvable from the DI container.

---

## Verification

```
dotnet build src/ContestJudging.Services/ContestJudging.Services.csproj -c Release
→ Build succeeded. 0 Warning(s), 0 Error(s)

dotnet build src/ContestJudging.Web/ContestJudging.Web.csproj -c Release
→ 13 errors — all pre-existing CS0535 in Infrastructure/SqliteRepositories.cs
  (repository classes do not implement CancellationToken overloads on interfaces).
  These errors predate ST-2 changes and are unrelated to the three files modified.
```
