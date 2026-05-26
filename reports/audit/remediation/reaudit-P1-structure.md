# Re-Audit Report — P1 Structure (PA1-A)

**Date:** 2026-05-26 | **Branch:** `fix/audit-remediate` | **Original Audit:**
`findings-P1-structure.json`

---

## Summary

| Metric             | Value                                                                                                                                                                                      |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Original findings  | 10 (1 critical, 3 medium, 4 low, 2 info)                                                                                                                                                   |
| **Resolved**       | **3** (STRUCT-002, STRUCT-003, STRUCT-005)                                                                                                                                                 |
| Still open         | 7 (STRUCT-001, 004, 006, 007, 008, 009, 010)                                                                                                                                               |
| New findings       | 4 (RA-STRUCT-001 through 004)                                                                                                                                                              |
| **Overall health** | **Slightly better** — CPM is fixed, dead code removed, E2E project properly in solution. Layer isolation violation persists. New test project introduced minor convention inconsistencies. |

---

## Step 1 — Full Discovery

### Projects Enumerated

| # | Project                             | SDK                                   | Layer          |
| - | ----------------------------------- | ------------------------------------- | -------------- |
| 1 | `src/ContestJudging.Core`           | `Microsoft.NET.Sdk`                   | Core           |
| 2 | `src/ContestJudging.Infrastructure` | `Microsoft.NET.Sdk`                   | Infrastructure |
| 3 | `src/ContestJudging.Services`       | `Microsoft.NET.Sdk`                   | Services       |
| 4 | `src/ContestJudging.Web`            | `Microsoft.NET.Sdk.BlazorWebAssembly` | Presentation   |
| 5 | `tests/ContestJudging.Tests`        | `Microsoft.NET.Sdk`                   | Unit Tests     |
| 6 | `tests/ContestJudging.E2ETests`     | `Microsoft.NET.Sdk`                   | E2E Tests      |
| 7 | `tests/ContestJudging.Web.Tests`    | `Microsoft.NET.Sdk.Razor`             | Web UI Tests   |

All 7 projects are in `ContestJudging.slnx` under `/src/` and `/tests/` folders.

### Dependency Graph

```
Core ───────────────────────────────────────────────────────────── (no deps)
  ^                      
  |                      
Infrastructure ──── (Core)                                        
  ^                      
  |                      
Services ─────────── (Core, Infrastructure)                       
  ^         ^                                                    
  |         |                                                    
Web ──────── (Core, Services, Infrastructure)  ← LAYER VIOLATION 
  ^                                                               
  |                                                               
Web.Tests ──────── (Web)                                          
Tests ──────────── (Core, Services, Infrastructure)               
E2ETests ──────── (no project refs) ─ standalone E2E via HTTP
```

**No circular references detected.** The dependency tree is acyclic.

### Layer Isolation Check

| Rule                                                       | Status                         |
| ---------------------------------------------------------- | ------------------------------ |
| Core references nothing                                    | Clean — zero ProjectReferences |
| Infrastructure only references Core                        | Clean                          |
| Services references Core + Infrastructure                  | Expected (DI composition root) |
| **Web references Infrastructure**                          | **VIOLATED** — see STRUCT-001  |
| Web razor code-behind files reference only Core interfaces | Clean                          |

Web's razor pages (`Judging.razor.cs`, `Setup.razor.cs`, `Results.razor.cs`)
correctly inject only Core interfaces (`ICategoryRepository`,
`IEntryRepository`, `IBackupService`, etc.) and Services abstractions
(`IPartitionService`, `IContestManager`). The violation is isolated to
`Program.cs`.

### Namespace Coherence

All source files use namespaces matching their project prefix:

- `ContestJudging.Core.Entities`, `.Interfaces`, `.Interfaces.Repositories`
- `ContestJudging.Infrastructure.Persistence`, `.Repositories`
- `ContestJudging.Services.Managers`, `.Partitioning`, `.Resolution`,
  `.Scoring`, `.Validation`, `.Extensions`
- `ContestJudging.Web.Pages`
- Test files: `ContestJudging.Tests`, `ContestJudging.Web.Tests`,
  `ContestJudging.E2ETests`

No orphaned namespaces, no duplicate namespaces across projects.

### Directory.Build.props / Directory.Packages.props

- Single `Directory.Build.props` at repo root — applied to all 7 projects.
- Single `Directory.Packages.props` at repo root — all `PackageReference` items
  have matching `PackageVersion` entries (CPM violation resolved).
- No nested/overriding `.props` files in subdirectories.

**Issue:** `IsTrimmable` + `EnableTrimAnalyzer` remain global, producing 10
expected-but-avoidable IL2026 warnings in Web.Tests.

### .gitignore

Missing SQLite patterns: `*.db`, `*.sqlite`, `*.sqlite3` still absent. The app
uses `contest.db` by name throughout.

### Orphaned Files

- `testapp/` directory: 62 files still on disk (untracked), never committed to
  git.
- No other orphaned `.cs` files detected.
- `Class1.cs` successfully deleted.

---

## Step 2 — Original Finding Verification

| ID         | Severity | Title                                  | Status       | Evidence                                                                                                                                                            |
| ---------- | -------- | -------------------------------------- | ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| STRUCT-001 | medium   | Web references Infrastructure          | **OPEN**     | `Web.csproj:36` still has Infrastructure ProjectReference. `Program.cs:6,12,21,36` still imports Infrastructure namespace and directly resolves `ContestDbContext`. |
| STRUCT-002 | medium   | E2E not in .slnx                       | **RESOLVED** | `.slnx:11` contains E2ETests project entry.                                                                                                                         |
| STRUCT-003 | critical | E2E CPM violation                      | **RESOLVED** | `Directory.Packages.props:22-25` has NUnit, NUnit.Analyzers, NUnit3TestAdapter, Microsoft.Playwright.NUnit PackageVersions.                                         |
| STRUCT-004 | low      | E2E missing RootNamespace/AssemblyName | **OPEN**     | `E2ETests.csproj` still has no `<RootNamespace>` or `<AssemblyName>`.                                                                                               |
| STRUCT-005 | low      | Class1.cs orphan                       | **RESOLVED** | File deleted. Only 3 legitimate `.cs` files remain in Infrastructure.                                                                                               |
| STRUCT-006 | info     | Mixed test frameworks                  | **OPEN**     | E2E uses NUnit; Tests + Web.Tests use xUnit. Third project added (Web.Tests) uses xUnit, widening the framework gap to 2 vs 1.                                      |
| STRUCT-007 | medium   | .gitignore missing *.db                | **OPEN**     | No `*.db`/`*.sqlite`/`*.sqlite3` patterns in `.gitignore`.                                                                                                          |
| STRUCT-008 | low      | Web.csproj redundant props             | **OPEN**     | `Web.csproj:9-11` still duplicates `TargetFramework`, `Nullable`, `ImplicitUsings` from `Directory.Build.props`.                                                    |
| STRUCT-009 | medium   | Orphaned testapp/                      | **OPEN**     | `testapp/` directory on disk with 62 files, untracked (`??` in git status).                                                                                         |
| STRUCT-010 | info     | Trim analyzer global                   | **OPEN**     | `Directory.Build.props:10-11` still applies trim to all projects. 10 new IL2026 warnings in Web.Tests from this.                                                    |

**Resolved: 3/10 | Open: 7/10**

---

## Step 3 — New Findings

| ID            | Severity | Title                                                                                               |
| ------------- | -------- | --------------------------------------------------------------------------------------------------- |
| RA-STRUCT-001 | low      | Web.Tests.csproj missing RootNamespace and AssemblyName (same pattern as STRUCT-004)                |
| RA-STRUCT-002 | low      | E2ETests + Web.Tests have redundant TargetFramework/Nullable overrides (same pattern as STRUCT-008) |
| RA-STRUCT-003 | low      | E2ETests.csproj has non-standard `LangVersion=latest` override (no other project uses this)         |
| RA-STRUCT-004 | medium   | Stale `testapp/` directory persists with 62 untracked scaffold files on disk                        |

**New: 4 (3 low, 1 medium)**

All new findings are convention/style issues introduced by the remediation
(Web.Tests created by R3-B without full convention adherence) or pre-existing
issues not caught by the original audit (LangVersion override, broader
redundant-props scope).

---

## Step 4 — Comparison

| Dimension      | Before  | After                                            | Delta           |
| -------------- | ------- | ------------------------------------------------ | --------------- |
| Total findings | 10      | 14 (7 open + 3 resolved + 4 new)                 | +4              |
| Critical       | 1       | 0                                                | -1              |
| Medium         | 3       | 3 (STRUCT-001, 007, 009) + 1 new (RA-STRUCT-004) | +1              |
| Low            | 4       | 4 open + 3 new                                   | +3              |
| Info           | 2       | 2 open                                           | 0               |
| Build errors   | 1 (CPM) | 0                                                | N/A             |
| Build warnings | ~       | 10 (IL2026 in Web.Tests)                         | same root cause |

**Key improvements:**

- Critical CPM violation fixed (build no longer broken for E2E project)
- E2E project properly in solution (CI coverage)
- Dead Class1.cs removed

**Key regressions/unchanged:**

- Web→Infrastructure layer violation unchanged (the core architectural defect)
- .gitignore still missing SQLite patterns (risk of accidental DB commits)
- testapp/ directory still on disk
- New Web.Tests project missing convention properties

**Overall structure health: slightly better.** The critical build-blocking issue
is fixed and the solution is structurally complete. However, the primary
architectural concern (layer isolation) and several hygiene items remain
unresolved, and minor convention inconsistencies were introduced by the
remediation.
