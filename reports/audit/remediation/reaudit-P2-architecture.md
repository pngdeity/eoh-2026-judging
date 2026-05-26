# Re-Audit Report — P2 Architecture (ARCH-001 through ARCH-008)

**Agent:** PA2-A (domain: architecture)\
**Date:** 2026-05-26\
**Branch:** `fix/audit-remediate`\
**Based on:** `reports/audit/findings-P2-architecture.json`

---

## 1. Remediation Integrity Verification

### ARCH-002 — ContestManager depends on ContestDbContext → **RESOLVED**

**Remediation:** Extract `IDatabaseBackupService` into Core, implement in
Infrastructure, inject into ContestManager.

| Check                                                                     | Result | Evidence                                                                                   |
| ------------------------------------------------------------------------- | ------ | ------------------------------------------------------------------------------------------ |
| No `using ContestJudging.Infrastructure.Persistence` in ContestManager.cs | PASS   | `ContestManager.cs:6-10` — imports only Core.Interfaces, Entities, and Services namespaces |
| No `ContestDbContext` field                                               | PASS   | `ContestManager.cs:16-22` — fields are repository/service/strategy abstractions only       |
| Constructor takes `IDatabaseBackupService`                                | PASS   | `ContestManager.cs:31` — `IDatabaseBackupService backupService`                            |
| ExportDataAsync delegates to `_backupService.ExportAsync()`               | PASS   | `ContestManager.cs:118-120`                                                                |
| ImportDataAsync delegates to `_backupService.ImportAsync()`               | PASS   | `ContestManager.cs:123-125`                                                                |
| Tests use `Mock<IDatabaseBackupService>`                                  | PASS   | `ContestManagerTests.cs:30,65,103,138,158` — all constructors pass mockBackup.Object       |

**Verdict:** Fully remediated. No residual coupling to `ContestDbContext`.

---

### ARCH-004 — ContestDbContext SRP violation (Export/Import methods) → **RESOLVED**

**Remediation:** Remove Export/Import from ContestDbContext, move to
`DatabaseBackupService`.

| Check                                               | Result | Evidence                                                                      |
| --------------------------------------------------- | ------ | ----------------------------------------------------------------------------- |
| No `ExportDatabaseAsync` method in ContestDbContext | PASS   | `ContestDbContext.cs` — 95 lines, only DbSet properties and `OnModelCreating` |
| No `ImportDatabaseAsync` method in ContestDbContext | PASS   | Same                                                                          |
| Methods exist in `DatabaseBackupService`            | PASS   | `DatabaseBackupService.cs:14-32` — ExportAsync, ImportAsync                   |
| SQLite magic-byte validation on import              | PASS   | `DatabaseBackupService.cs:24-30` — validates 16-byte SQLite header            |
| Path configurable via constructor                   | PASS   | `DatabaseBackupService.cs:9` — `string dbPath` parameter                      |

**Verdict:** Fully remediated.

---

### `IDatabaseBackupService` / `DatabaseBackupService` — Abstraction Quality

| Check                                        | Result | Evidence                                                           |
| -------------------------------------------- | ------ | ------------------------------------------------------------------ |
| Interface in Core                            | PASS   | `Core/Interfaces/IDatabaseBackupService.cs` — 7 lines, two methods |
| Implementation in Infrastructure             | PASS   | `Infrastructure/Persistence/DatabaseBackupService.cs`              |
| Implementation only depends on Core + System | PASS   | imports only `ContestJudging.Core.Interfaces`                      |
| No layer violation                           | PASS   | Correct dependency direction: Infrastructure → Core                |

---

### DI Registration — ServiceCollectionExtensions

| Check                                                | Result                               | Evidence                               |
| ---------------------------------------------------- | ------------------------------------ | -------------------------------------- |
| `IDatabaseBackupService` registered                  | PASS                                 | `ServiceCollectionExtensions.cs:34-35` |
| `IBackupService` registered                          | PASS                                 | `ServiceCollectionExtensions.cs:36`    |
| Both Scoped                                          | PASS                                 | `AddScoped` used                       |
| `IDatabaseBackupService` factory uses `"contest.db"` | PASS (with caveat — see RA-ARCH-002) | `ServiceCollectionExtensions.cs:35`    |

---

## 2. Layer Discipline Check

### 2.1 ARCH-001 — Web → Infrastructure dependency → **STILL OPEN**

Program.cs retains all prohibited Infrastructure references:

- `using ContestJudging.Infrastructure.Persistence` — `Program.cs:6`
- `SQLitePCL.Batteries_V2.Init()` — `Program.cs:21`
- `ContestDbContext` resolved directly — `Program.cs:36`
- `Web.csproj` still references `Microsoft.EntityFrameworkCore.Sqlite` — line 29
- `Web.csproj` still references `SQLitePCLRaw.bundle_e_sqlite3` — line 30
- `Web.csproj` still has ProjectReference to Infrastructure — line 36

**No change from original audit.** ARCH-001 remains open.

---

### 2.2 DatabaseBackupService — No Layer Violations

| Check                                                | Result                               |
| ---------------------------------------------------- | ------------------------------------ |
| Lives in Infrastructure layer                        | PASS (correct for concrete file I/O) |
| Implements Core interface (`IDatabaseBackupService`) | PASS (DIP-compliant)                 |
| No circular references                               | PASS (Infrastructure → Core only)    |
| No dependency on Services or Web                     | PASS                                 |

---

### 2.3 BackupService — Layer Concern

`BackupService` lives in `Services.Managers` and imports `Blazored.LocalStorage`
(line 3). The `Services.csproj` references the `Blazored.LocalStorage` NuGet
package (line 14).

**Analysis:** `Blazored.LocalStorage` provides browser localStorage access — a
platform-specific (Blazor WASM) API. In Clean Architecture, the
Services/Application layer should not directly depend on platform-specific
libraries. However, in a Blazor WASM-only application, browser localStorage is
the sole persistent storage mechanism, making this analogous to a database
driver dependency in Infrastructure. The abstraction (`IBackupService` in Core)
is correctly placed; only the implementation dependency is questionable.

**See RA-ARCH-001.**

---

### 2.4 New Abstraction Dependency Graph (no circular deps)

```
Core.Interfaces
├── IDatabaseBackupService ─── implemented by ─── DatabaseBackupService (Infrastructure)
├── IBackupService ──────────── implemented by ─── BackupService (Services.Managers)
│                                                   ├── depends on: ILocalStorageService (Blazored)
│                                                   ├── depends on: IDatabaseBackupService
│                                                   └── depends on: ILogger<BackupService>
│
ContestManager (Services.Managers)
├── depends on: IDatabaseBackupService (not IBackupService)
├── ExportDataAsync → _backupService.ExportAsync()
└── ImportDataAsync → _backupService.ImportAsync()

Web Pages (Setup.razor.cs, Judging.razor.cs)
├── inject: IContestManager (for ExportDataAsync)
├── inject: IBackupService (for SaveBackupAsync, TryRestoreBackupAsync)
└── Flow: ExportDataAsync() → SaveBackupAsync(bytes)
```

No circular dependencies detected. Clean separation between
`IDatabaseBackupService` (raw file I/O) and `IBackupService` (localStorage
orchestration + schema versioning).

---

## 3. DI Registration Audit

### 3.1 Complete Service Map

| Registration          | Interface                | Implementation                  | Lifetime | Dependencies                                                               | Layer                         |
| --------------------- | ------------------------ | ------------------------------- | -------- | -------------------------------------------------------------------------- | ----------------------------- |
| `AddDbContext`        | —                        | `ContestDbContext`              | Scoped   | `DbContextOptions`                                                         | `Infrastructure.Persistence`  |
| `AddScoped`           | `ICategoryRepository`    | `SqliteCategoryRepository`      | Scoped   | `ContestDbContext`                                                         | `Infrastructure.Repositories` |
| `AddScoped`           | `IEntryRepository`       | `SqliteEntryRepository`         | Scoped   | `ContestDbContext`                                                         | `Infrastructure.Repositories` |
| `AddScoped`           | `IRelationRepository`    | `SqliteRelationRepository`      | Scoped   | `ContestDbContext`                                                         | `Infrastructure.Repositories` |
| `AddScoped`           | `IValidationService`     | `GraphValidationService`        | Scoped   | (none)                                                                     | `Services.Validation`         |
| `AddScoped`           | `IPartitionService`      | `PartitionService`              | Scoped   | (none)                                                                     | `Services.Partitioning`       |
| `AddScoped`           | `IGlobalRankingService`  | `BradleyTerryResolutionService` | Scoped   | (MathNet.Numerics)                                                         | `Services.Resolution`         |
| `AddScoped`           | `IScoringStrategy`       | `LinearSpacingScoring`          | Scoped   | (none)                                                                     | `Services.Scoring`            |
| `AddScoped` (factory) | `IDatabaseBackupService` | `DatabaseBackupService`         | Scoped   | `dbPath` string                                                            | `Infrastructure.Persistence`  |
| `AddScoped`           | `IBackupService`         | `BackupService`                 | Scoped   | `ILocalStorageService`, `IDatabaseBackupService`, `ILogger<BackupService>` | `Services.Managers`           |
| `AddScoped`           | `IContestManager`        | `ContestManager`                | Scoped   | 6 abstraction deps + `IDatabaseBackupService`                              | `Services.Managers`           |

Additional registrations in Program.cs: | `AddScoped` | — | `HttpClient` |
Scoped | `BaseAddress` | — | | `AddBlazoredLocalStorage` |
`ILocalStorageService` | — | Singleton (factory default) | — | — |

### 3.2 Captive Dependency Analysis

- **Blazor WASM environment:** `Scoped` == `Singleton` (no request scope).
  Captive dependency is not a practical concern.
- `BackupService` (Scoped) depends on `ILocalStorageService` (Singleton):
  correct direction (transient/scoped → singleton is acceptable).
- No scoped→transient or singleton→scoped reversal detected.

**Verdict:** No captive dependency issues in Blazor WASM context.

### 3.3 ARCH-007 — Scoped DbContext → **STILL OPEN**

`AddDbContext<ContestDbContext>` still registered as Scoped (line 23). No
`AddDbContextFactory` replacement, no `ChangeTracker.Clear()` calls. This
informational finding remains valid.

---

## 4. Original Findings Status Summary

| ID       | Title                             | Original Severity | Status                                     |
| -------- | --------------------------------- | ----------------- | ------------------------------------------ |
| ARCH-001 | Web → Infrastructure dependency   | Medium            | **OPEN** — no change                       |
| ARCH-002 | ContestManager → ContestDbContext | High              | **RESOLVED** — uses IDatabaseBackupService |
| ARCH-003 | Service interfaces in Services    | Medium            | **OPEN** — no change                       |
| ARCH-004 | ContestDbContext SRP violation    | Medium            | **RESOLVED** — Export/Import removed       |
| ARCH-005 | Composition root in Services      | Low               | **OPEN** — no change                       |
| ARCH-006 | Unregistered scoring strategies   | Low               | **OPEN** — no change                       |
| ARCH-007 | Scoped DbContext in WASM          | Informational     | **OPEN** — no change                       |
| ARCH-008 | Inconsistent interface placement  | Informational     | **OPEN** — no change                       |

**Resolved: 2/8 | Open: 6/8**

---

## 5. New Findings

### RA-ARCH-001: BackupService depends on Blazored.LocalStorage in Services layer

- **Severity:** Low
- **Category:** Architecture / Layer Discipline
- **Files:**
  - `src/ContestJudging.Services/Managers/BackupService.cs:3`
  - `src/ContestJudging.Services/ContestJudging.Services.csproj:14`
- **Evidence:** `BackupService` imports `Blazored.LocalStorage` and depends on
  `ILocalStorageService`. `Services.csproj` has a direct `PackageReference` to
  `Blazored.LocalStorage`.
- **Rule violated:** Clean Architecture Dependency Rule — the
  Services/Application layer should not depend on platform/framework-specific
  packages. `Blazored.LocalStorage` is a Blazor browser-specific library. The
  `IBackupService` abstraction is correctly in Core, but the implementation's
  package dependency ties Services to a browser API.
- **Mitigation:** In Blazor WASM, browser localStorage is the sole persistent
  storage mechanism, making this analogous to a database driver reference in
  Infrastructure. The risk is theoretical unless the codebase targets a
  non-Blazor platform.
- **Remediation:** Either (a) accept that Blazored.LocalStorage is an
  infrastructure-level dependency like EF Core SQLite, or (b) extract
  `BackupService` to a dedicated `ContestJudging.Web.Services` or adapter
  project.
- **Related:** —

### RA-ARCH-002: DatabaseBackupService factory hardcodes DB path independently of connectionString

- **Severity:** Low
- **Category:** Architecture / Configuration
- **Files:**
  - `src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:34-35`
  - `src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:21`
- **Evidence:** `AddContestJudgingServices` receives `connectionString`
  parameter (`"Data Source=contest.db"`) but the `IDatabaseBackupService`
  factory hardcodes `new DatabaseBackupService("contest.db")` (line 35). If the
  connection string is changed to a different path, the backup service will
  operate on the wrong file.
- **Rule violated:** Principle of Least Surprise — the DB path should be derived
  from a single source of truth (the connection string).
- **Remediation:** Parse the `Data Source` value from the `connectionString`
  parameter and pass it into `DatabaseBackupService`. Example:
  ```csharp
  var builder = new SqliteConnectionStringBuilder(connectionString);
  services.AddScoped<IDatabaseBackupService>(sp =>
      new DatabaseBackupService(builder.DataSource));
  ```
- **Related:** ARCH-001

### RA-ARCH-003: IBackupService in Core exacerbates ARCH-008 interface placement inconsistency

- **Severity:** Informational
- **Category:** Architecture / Consistency
- **Files:**
  - `src/ContestJudging.Core/Interfaces/IBackupService.cs`
  - `src/ContestJudging.Core/Interfaces/IDatabaseBackupService.cs`
  - `src/ContestJudging.Services/Managers/IContestManager.cs`
- **Evidence:** The new `IBackupService` and `IDatabaseBackupService` are
  correctly placed in Core.Interfaces (consistent with `IScoringStrategy`).
  However, `IContestManager`, `IValidationService`, `IPartitionService`, and
  `IGlobalRankingService` remain in the Services project (ARCH-003/ARCH-008).
  This increases the inconsistency: some service interfaces are in Core, others
  are in Services, with no documented rationale.
- **Rule violated:** Architecture consistency.
- **Remediation:** Resolve ARCH-003/ARCH-008 by moving all service interfaces to
  Core.
- **Related:** ARCH-003, ARCH-008

---

## 6. Architecture Health Assessment

### Compared to Original P2 Audit

| Metric          | Original               | Post-Remediation  | Delta |
| --------------- | ---------------------- | ----------------- | ----- |
| Total findings  | 8                      | 11 (+3 new)       | +3    |
| High severity   | 1 (ARCH-002)           | 0                 | -1    |
| Medium severity | 3 (ARCH-001, 003, 004) | 2 (ARCH-001, 003) | -1    |
| Low severity    | 2 (ARCH-005, 006)      | 4 (+2 new)        | +2    |
| Informational   | 2 (ARCH-007, 008)      | 3 (+1 new)        | +1    |
| Resolved        | —                      | 2 (ARCH-002, 004) | +2    |

### Overall Assessment

The remediated codebase is **moderately improved** over the original P2 audit
baseline.

**Strengths:**

- The most severe finding (ARCH-002, HIGH) is fully resolved — ContestManager no
  longer couples to concrete EF Core types, enabling testability with
  `Mock<IDatabaseBackupService>`.
- ARCH-004 is resolved — ContestDbContext is now a pure ORM configuration class.
- New abstractions (`IDatabaseBackupService`, `IBackupService`) follow the
  Dependency Inversion Principle correctly: interfaces in Core, implementations
  in appropriate layers, clean separation of concerns (raw I/O vs. localStorage
  orchestration + schema versioning).
- No circular dependencies introduced.
- Backup pipeline is well-designed: `Program.cs` →
  `IBackupService.TryRestoreBackupAsync()` on startup;
  `Setup.razor.cs`/`Judging.razor.cs` → `IContestManager.ExportDataAsync()` →
  `IBackupService.SaveBackupAsync()`.

**Weaknesses:**

- The six open original findings (ARCH-001, 003, 005, 006, 007, 008) are
  foundational architectural issues. ARCH-001 (Web→Infrastructure) and ARCH-005
  (composition root location) in particular violate Clean Architecture and
  represent increasing technical debt.
- ARCH-003/ARCH-008 (inconsistent interface placement) is now exacerbated by the
  new Core-placed interfaces sitting alongside Services-placed interfaces.
- RA-ARCH-002 (hardcoded DB path) is a minor coupling issue but trivial to fix.

### Recommendation

The ARCH-002/ARCH-004 fixes are high-quality and should be merged. The remaining
open findings (particularly ARCH-001 and ARCH-003) should be addressed in the
next remediation pass, as they form the foundation of a proper Clean
Architecture. RA-ARCH-002 can be fixed trivially alongside ARCH-001.
