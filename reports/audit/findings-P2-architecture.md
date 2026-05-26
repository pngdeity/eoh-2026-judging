# P2-A Architecture Audit Report

**Agent:** P2-A | **Pass:** 2 | **Domain:** architecture

---

## Summary

| Metric         | Value |
| -------------- | ----- |
| Files scanned  | 32    |
| Findings count | 8     |
| Lines of code  | ~2800 |
| Critical       | 1     |
| High           | 1     |
| Medium         | 3     |
| Low            | 2     |
| Informational  | 1     |

---

## Layer Dependency Map

```
Web (Blazor WASM UI)
 ├── Core          ✓ (correct — UI needs domain types)
 ├── Services      ✓ (correct — UI needs application services)
 └── Infrastructure ✗ (ARCH-001 — direct concrete dependency)

Services (Application/Business)
 ├── Core          ✓ (correct)
 └── Infrastructure ✗ (ARCH-002 — ContestManager takes ContestDbContext directly)

Infrastructure (Persistence)
 └── Core          ✓ (correct)

Core (Domain)
 └── (none)        ✓ (zero dependencies)
```

---

## Findings

### ARCH-001 — Web project directly references Infrastructure layer [medium]

**Category:** architecture **Severity:** medium (confirmed from STRUCT-001)
**Status:** open

**Files:**

- `src/ContestJudging.Web/Program.cs:5` —
  `using ContestJudging.Infrastructure.Persistence;`
- `src/ContestJudging.Web/Program.cs:21` — `SQLitePCL.Batteries_V2.Init();`
- `src/ContestJudging.Web/Program.cs:34` —
  `scope.ServiceProvider.GetRequiredService<ContestDbContext>()`
- `src/ContestJudging.Web/ContestJudging.Web.csproj:29` —
  `<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />`
- `src/ContestJudging.Web/ContestJudging.Web.csproj:30` —
  `<PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" />`
- `src/ContestJudging.Web/ContestJudging.Web.csproj:36` —
  `<ProjectReference Include="..\ContestJudging.Infrastructure\..." />`

**Evidence:** The Web project has both a direct ProjectReference to
Infrastructure AND direct PackageReferences to EF Core SQLite and SQLitePCL.
These packages are already referenced by the Infrastructure project
(`Infrastructure.csproj:14-15`), making them duplicated transitive dependencies.
`Program.cs` directly imports the `ContestJudging.Infrastructure.Persistence`
namespace to resolve `ContestDbContext` and calls the SQLitePCL native
initialization directly from the UI layer.

**Rule violated:** Clean Architecture Dependency Rule — the outer layer (Web/UI)
must not depend on inner layer (Infrastructure) for anything except DI
composition. The UI layer should not know what database is used, what ORM is
used, or how native SQLite is initialized.

**Remediation:**

1. Remove `<ProjectReference Include="..\ContestJudging.Infrastructure\...">`
   from Web.csproj
2. Remove `<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />`
   and `<PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" />` from
   Web.csproj (they are already transitive via Services → Infrastructure)
3. Move `SQLitePCL.Batteries_V2.Init()` into
   `ServiceCollectionExtensions.AddContestJudgingServices()` in the Services
   layer
4. Move the startup database restore/resolution of `ContestDbContext` into an
   extension method in Services (or replace with an `IDatabaseInitializer`
   abstraction)
5. Use `builder.Services.AddContestJudgingServices()` as the single composition
   entry point

**Related:** ARCH-002, ARCH-005, STRUCT-001

**Effort:** medium

---

### ARCH-002 — ContestManager directly depends on concrete ContestDbContext [high]

**Category:** architecture **Severity:** high **Status:** open

**Files:**

- `src/ContestJudging.Services/Managers/ContestManager.cs:9` —
  `using ContestJudging.Infrastructure.Persistence;`
- `src/ContestJudging.Services/Managers/ContestManager.cs:23` —
  `private readonly ContestDbContext _context;`
- `src/ContestJudging.Services/Managers/ContestManager.cs:32` —
  `ContestDbContext context)` constructor parameter
- `tests/ContestJudging.Tests/ContestManagerTests.cs:30` —
  `new ContestManager(..., null!);` — test forced to pass null for concrete
  dependency
- `tests/ContestJudging.Tests/ContestManagerTests.cs:64` — same null! pattern
- `tests/ContestJudging.Tests/ContestManagerTests.cs:101` — same null! pattern

**Evidence:** `ContestManager` (in the Services/Application layer) has a direct
constructor dependency on `ContestDbContext` — a concrete EF Core `DbContext`
from the Infrastructure layer. This violates the Dependency Inversion Principle:
high-level modules should depend on abstractions, not concretions. The
consequence is visible in tests: all three `ContestManagerTests` methods are
forced to pass `null!` for the `ContestDbContext` parameter because the concrete
DbContext cannot be easily mocked.

The `ContestManager` uses `_context` only for `ExportDataAsync()` (line 121:
`_context.ExportDatabaseAsync()`) and `ImportDataAsync()` (line 126:
`_context.ImportDatabaseAsync()`). These two methods do not need the full
DbContext — they only need file-based import/export.

**Rule violated:** SOLID: Dependency Inversion Principle. High-level modules
(ContestManager in Services) must not depend on low-level modules
(ContestDbContext in Infrastructure). Both should depend on abstractions. Clean
Architecture: inner layers must not know about outer layer details.

**Remediation:**

1. Extract an `IDatabaseBackupService` interface in Core (or Services) with
   `ExportAsync()` and `ImportAsync(byte[])` methods
2. Implement `DatabaseBackupService` in Infrastructure (using file I/O, not EF
   Core)
3. Have `ContestManager` depend on `IDatabaseBackupService` instead of
   `ContestDbContext`
4. This also fixes ARCH-004 (SRP on ContestDbContext) since the backup methods
   should be removed from the DbContext

**Related:** ARCH-001, ARCH-004

**Effort:** small

---

### ARCH-003 — Service interfaces defined in Services project, not Core [medium]

**Category:** architecture **Severity:** medium **Status:** open

**Files:**

- `src/ContestJudging.Services/Validation/IValidationService.cs:1` — interface
  in Services, not Core
- `src/ContestJudging.Services/Partitioning/IPartitionService.cs:1` — interface
  in Services, not Core
- `src/ContestJudging.Services/Resolution/IGlobalRankingService.cs:1` —
  interface in Services, not Core
- `src/ContestJudging.Services/Managers/IContestManager.cs:1` — interface in
  Services, not Core
- `src/ContestJudging.Web/Pages/Setup.razor.cs:11` —
  `using ContestJudging.Services.Partitioning;` (for IPartitionService)
- `src/ContestJudging.Web/Pages/Judging.razor.cs:11` —
  `using ContestJudging.Services.Partitioning;` (for IPartitionService)
- `src/ContestJudging.Web/Pages/Results.razor.cs:3` —
  `using ContestJudging.Services.Managers;` (for IContestManager)

**Evidence:** Four service interfaces (`IValidationService`,
`IPartitionService`, `IGlobalRankingService`, `IContestManager`) are defined in
the Services project rather than Core. This contrasts with `IScoringStrategy`
(correctly in `ContestJudging.Core.Interfaces`) and the three repository
interfaces (`ICategoryRepository`, `IEntryRepository`, `IRelationRepository` —
correctly in `ContestJudging.Core.Interfaces.Repositories`).

The consequence: Web pages that inject these interfaces must import Services
namespaces (`using ContestJudging.Services.Partitioning;`,
`using ContestJudging.Services.Managers;`). This creates an implicit coupling
where the UI layer knows about the Services project's namespace structure, not
just its abstractions.

**Rule violated:** SOLID: Dependency Inversion Principle. Abstractions should be
owned by the layer that defines the policy (Core or a dedicated Application
project), not by the implementation layer (Services). Clean Architecture:
Dependency Rule — all compile-time dependencies point inward, and abstractions
live in the innermost layer that needs them.

**Remediation:**

1. Move `IValidationService`, `IPartitionService`, `IGlobalRankingService`, and
   `IContestManager` to `ContestJudging.Core.Interfaces`
2. Update all `using` directives in implementations and consumers accordingly
3. Consider creating a `ContestJudging.Core.Interfaces.Services` sub-namespace
   or a separate `ContestJudging.Application.Abstractions` project for
   service-level interfaces distinct from domain-level interfaces

**Related:** ARCH-008

**Effort:** medium

---

### ARCH-004 — ContestDbContext mixes ORM responsibilities with raw file I/O [medium]

**Category:** architecture **Severity:** medium **Status:** open

**Files:**

- `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:66` —
  `public async Task<byte[]> ExportDatabaseAsync()`
- `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs:76` —
  `public async Task ImportDatabaseAsync(byte[] data)`

**Evidence:** The `ContestDbContext` class serves dual responsibilities:

1. EF Core database context (entity mapping, relations, queries via `DbSet`
   properties)
2. Raw file I/O operations on `contest.db` (`File.ReadAllBytesAsync` at line 71,
   `File.WriteAllBytesAsync` at line 79)

The `ExportDatabaseAsync` and `ImportDatabaseAsync` methods bypass EF Core
entirely, reading and writing the SQLite file directly. These methods hardcode
the database filename `contest.db` (lines 68, 78) and have no connection to the
EF Core model, schema, or tracking. This is a clear Single Responsibility
violation.

Additionally, `ExportDatabaseAsync` silently returns an empty array if the file
doesn't exist (line 73), without distinguishing between "no file" and "empty
file".

**Rule violated:** SOLID: Single Responsibility Principle. A class should have
only one reason to change. `ContestDbContext` should change only when the data
model changes, not when the backup mechanism changes. Clean Architecture:
persistence details (raw file backup) should be separated from data access (EF
Core context).

**Remediation:**

1. Extract `ExportDatabaseAsync` and `ImportDatabaseAsync` into a separate
   `DatabaseBackupService : IDatabaseBackupService` class in Infrastructure
2. Inject the connection string or database path via configuration, not
   hardcoded
3. Register `IDatabaseBackupService` in DI
4. This also enables fixing ARCH-002 since `ContestManager` would depend on the
   backup abstraction instead of `ContestDbContext`

**Related:** ARCH-002

**Effort:** small

---

### ARCH-005 — Composition root (DI registration) in Services layer instead of outermost layer [low]

**Category:** architecture **Severity:** low **Status:** open

**Files:**

- `src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:5` —
  `using ContestJudging.Infrastructure.Persistence;`
- `src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:6` —
  `using ContestJudging.Infrastructure.Repositories;`
- `src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:21` —
  `public static IServiceCollection AddContestJudgingServices(this IServiceCollection services, string connectionString)`
- `src/ContestJudging.Web/Program.cs:7` —
  `using ContestJudging.Services.Extensions;`
- `src/ContestJudging.Web/Program.cs:64` —
  `static void AddServices(IServiceCollection services)`

**Evidence:** The DI composition logic
(`ServiceCollectionExtensions.AddContestJudgingServices`) lives in the Services
project, which itself depends on Infrastructure. This means
ServiceCollectionExtensions has compile-time knowledge of every Infrastructure
implementation (`SqliteCategoryRepository`, `SqliteEntryRepository`,
`SqliteRelationRepository`, `ContestDbContext`).

In Clean Architecture, the composition root should be in the outermost layer
(Web) because:

- Only the outermost layer should know about all dependencies
- Inner layers should not be forced to reference outer-layer concrete types
- Changing Infrastructure implementations should not require rebuilding Services

Currently, Services.csproj already references Infrastructure.csproj (line 12),
so the composition root placement reinforces this coupling.

Note: The Web project's `Program.cs` wraps `AddContestJudgingServices` in a
local function `AddServices` (line 64). This intermediate layer does not solve
the problem — the actual DI logic remains in Services.

**Rule violated:** Clean Architecture Composition Root pattern. The composition
root should be as close to the application entry point as possible, in the
outermost layer.

**Remediation:**

1. Move `ServiceCollectionExtensions.AddContestJudgingServices` to the Web
   project
2. Remove the Services → Infrastructure project reference
3. Define all DI registration in Program.cs (or a `DependencyInjection` folder
   in Web)
4. Services layer should only reference Core and define its interfaces there
   (see ARCH-003)

**Related:** ARCH-001, ARCH-002, ARCH-003

**Effort:** medium

---

### ARCH-006 — Two IScoringStrategy implementations exist but are never registered in DI [low]

**Category:** architecture **Severity:** low **Status:** open

**Files:**

- `src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:33` —
  `services.AddScoped<IScoringStrategy, LinearSpacingScoring>();`
- `src/ContestJudging.Services/Scoring/PercentileScoring.cs:1` — class exists
  but never registered
- `src/ContestJudging.Services/Scoring/DefinedIntervalScoring.cs:1` — class
  exists but never registered

**Evidence:** The DI registration at ServiceCollectionExtensions.cs:33 registers
only `LinearSpacingScoring` as the `IScoringStrategy` implementation. Two other
implementations (`PercentileScoring` and `DefinedIntervalScoring`) exist in the
codebase but are never wired into the DI container. Neither class is
instantiated anywhere in the production code.

`DefinedIntervalScoring` also has a parameterized constructor (takes
`IEnumerable<double> rankPoints`), which means it cannot be registered via the
simple `AddScoped<IScoringStrategy, DefinedIntervalScoring>()` pattern — it
needs a factory registration. This means even if someone wanted to use it, the
DI setup doesn't support it.

Scoring strategy tests (`ScoringStrategyTests.cs`) instantiate these classes
directly, confirming they are functional but unreachable from the application's
DI pipeline.

**Rule violated:** Principle of Least Surprise. Having multiple strategy
implementations of which only one is wired in DI is a latent configuration
error. Either the alternatives should be selectable (e.g., via configuration) or
removed as dead code.

**Remediation:**

1. Determine if `PercentileScoring` and `DefinedIntervalScoring` are intended
   for production use
2. If yes: add configuration-based registration (e.g., an enum or appsetting to
   select scoring strategy at startup)
3. If no: delete the unused implementation files
4. For `DefinedIntervalScoring`, if keeping, add a factory registration that
   reads rank points from configuration

**Related:** CQ-001 (dead Class1)

**Effort:** trivial

---

### ARCH-007 — AddScoped overuse in Blazor WASM — DbContext lives for app lifetime [informational]

**Category:** architecture **Severity:** informational **Status:** open

**Files:**

- `src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:23` —
  `services.AddDbContext<ContestDbContext>(options => options.UseSqlite(connectionString));`
- `src/ContestJudging.Web/Program.cs:21` — `SQLitePCL.Batteries_V2.Init();`

**Evidence:** All eight service registrations in `ServiceCollectionExtensions`
use `AddScoped` (lines 23, 26-28, 30-34). In Blazor WASM, there is no
per-request or per-circuit scope — `AddScoped` behaves identically to
`AddSingleton` unless `OwningComponentBase` is used. The codebase does not use
`OwningComponentBase`.

The critical concern is `ContestDbContext`. EF Core's `DbContext` is designed to
be short-lived: it accumulates tracked entities in its change tracker over time.
A `DbContext` that lives for the application's entire lifetime will:

- Grow the change tracker unboundedly as users add categories, entries, and
  relations
- Degrade query performance as the number of tracked entities increases
- Never release memory for entities that are no longer needed

The `AddDbContext<T>` extension method registers the context as `Scoped` by
default. In Blazor WASM this means it's effectively a singleton. The
`EnsureCreatedAsync` call in `Program.cs:56` only runs once, and the
`ExportDataAsync`/`ImportDataAsync` methods bypass the DbContext's change
tracker entirely (they read/write the SQLite file directly), so there is no
lifecycle management for tracked entities.

**Rule violated:** EF Core best practices — DbContext instances should be
short-lived. In Blazor WASM, consider `IDbContextFactory<T>` for creating
short-lived contexts on demand, or explicitly call `ChangeTracker.Clear()`
periodically.

**Remediation:**

1. Use `AddDbContextFactory<ContestDbContext>` instead of
   `AddDbContext<ContestDbContext>`
2. Inject `IDbContextFactory<ContestDbContext>` into repositories and
   create/dispose DbContext per operation
3. Alternatively, call `context.ChangeTracker.Clear()` after significant
   operations to prevent unbounded tracker growth
4. For the startup restore flow in `Program.cs`, create the context via factory
   and dispose it after use

**Related:** ARCH-001

**Effort:** medium

---

### ARCH-008 — Inconsistent interface placement across Core and Services [informational]

**Category:** architecture **Severity:** informational **Status:** open

**Files:**

- `src/ContestJudging.Core/Interfaces/IScoringStrategy.cs:1` — interface in Core
- `src/ContestJudging.Core/Interfaces/Repositories/ICategoryRepository.cs:1` —
  interface in Core
- `src/ContestJudging.Core/Interfaces/Repositories/IEntryRepository.cs:1` —
  interface in Core
- `src/ContestJudging.Core/Interfaces/Repositories/IRelationRepository.cs:1` —
  interface in Core
- `src/ContestJudging.Services/Validation/IValidationService.cs:1` — interface
  in Services, not Core
- `src/ContestJudging.Services/Partitioning/IPartitionService.cs:1` — interface
  in Services, not Core
- `src/ContestJudging.Services/Resolution/IGlobalRankingService.cs:1` —
  interface in Services, not Core
- `src/ContestJudging.Services/Managers/IContestManager.cs:1` — interface in
  Services, not Core

**Evidence:** The codebase has no consistent rule for where interfaces live.
Repository interfaces and `IScoringStrategy` are in
`ContestJudging.Core.Interfaces`. The four service-level interfaces are in the
Services project alongside their implementations. There is no documented
rationale for the split.

This creates confusion for developers: "Where do I put a new interface?" It also
means the project lacks a clear Architectural Decision Record for interface
ownership. A consistent rule (e.g., "all abstractions live in Core") or a
structured approach (e.g., "domain abstractions in Core, application
abstractions in a separate Application.Abstractions project") should be adopted.

**Rule violated:** Architecture consistency — projects with mixed interface
placement strategies are harder to maintain and onboard new developers onto.

**Remediation:**

1. Adopt a single rule for interface placement. Recommendation: all interfaces
   in Core (following the repository interface pattern)
2. Move `IValidationService`, `IPartitionService`, `IGlobalRankingService`, and
   `IContestManager` to `ContestJudging.Core.Interfaces`
3. Document the rule in the project README or AGENTS.md

**Related:** ARCH-003

**Effort:** small

---

## Overall Assessment

The codebase's architecture shows a well-intentioned Clean Architecture
structure that has eroded in key places. The fundamental dependency chain is
correct (Core is pure, Infrastructure → Core, Services → Core), but two critical
leaks undermine the design:

1. **Services → Infrastructure coupling** is the root cause of most issues.
   `ContestManager` taking a concrete `ContestDbContext` (ARCH-002) and
   `ServiceCollectionExtensions` knowing all Infrastructure types (ARCH-005)
   together mean the Services layer cannot exist without Infrastructure — it's
   not truly independent.

2. **Web → Infrastructure coupling** (ARCH-001) is a symptom that trickles up
   from the Services leak. Because Services doesn't fully encapsulate
   Infrastructure, Web must reach through to `ContestDbContext` and `SQLitePCL`
   directly.

The highest-priority fix is ARCH-002 (`ContestManager` → `ContestDbContext`
dependency). Extracting an `IDatabaseBackupService` abstraction would resolve
ARCH-002, ARCH-004 (SRP on DbContext), and enable ARCH-001 (removing Web→Infra
references) as a follow-up.

Interface placement (ARCH-003, ARCH-008) and composition root location
(ARCH-005) are structural improvements that should be tackled after the core
dependency leaks are fixed.
