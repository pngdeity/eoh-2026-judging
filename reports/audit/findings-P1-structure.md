# P1-A Structure Audit Report

**Agent:** P1-A | **Pass:** 1 | **Domain:** Structure / Solution Topology |
**Status:** success

---

## 1. Project Inventory

| #  | Project                       | Path                                 | Layer          | SDK                                 |
| -- | ----------------------------- | ------------------------------------ | -------------- | ----------------------------------- |
| 1  | ContestJudging.Core           | `src/ContestJudging.Core/`           | Core           | Microsoft.NET.Sdk                   |
| 2  | ContestJudging.Infrastructure | `src/ContestJudging.Infrastructure/` | Infrastructure | Microsoft.NET.Sdk                   |
| 3  | ContestJudging.Services       | `src/ContestJudging.Services/`       | Services       | Microsoft.NET.Sdk                   |
| 4  | ContestJudging.Web            | `src/ContestJudging.Web/`            | Web            | Microsoft.NET.Sdk.BlazorWebAssembly |
| 5  | ContestJudging.Tests          | `tests/ContestJudging.Tests/`        | Tests          | Microsoft.NET.Sdk                   |
| 6* | ContestJudging.E2ETests       | `tests/ContestJudging.E2ETests/`     | Tests          | Microsoft.NET.Sdk                   |

_\* Not in solution file_

### Orphaned project (not in solution):

- `testapp/` — standalone Blazor WASM scaffold, uses hardcoded package versions,
  namespace `testapp`

---

## 2. Dependency Graph

```
Core ← (no project references)

Infrastructure → Core

Services → Core
Services → Infrastructure

Web → Core          ← should only need Core + Services
Web → Services
Web → Infrastructure ← LAYER VIOLATION

Tests → Core
Tests → Services
Tests → Infrastructure

E2ETests → (no project references, standalone)
```

**Circular references:** None detected. Graph is a DAG.

---

## 3. Findings Summary

### STRUCT-001 — Layer Isolation Violation (medium)

Web project directly references Infrastructure project and uses EF Core / SQLite
packages directly (`Microsoft.EntityFrameworkCore.Sqlite`,
`SQLitePCLRaw.bundle_e_sqlite3`). Web's `Program.cs` calls
`SQLitePCL.Batteries_V2.Init()`, resolves `ContestDbContext`, and imports
`Microsoft.EntityFrameworkCore` — all Infrastructure concerns. Web should only
depend on Services + Core.

**Files:** `src/ContestJudging.Web/ContestJudging.Web.csproj:29-30,36`,
`src/ContestJudging.Web/Program.cs:5,12,21`

### STRUCT-002 — E2E Tests Not in Solution (medium)

`tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj` exists on disk
but is absent from `ContestJudging.slnx`. `dotnet build` from the solution root
will not compile these tests.

### STRUCT-003 — Central Package Management Violation (critical)

`E2ETests.csproj` references `NUnit`, `NUnit.Analyzers`, `NUnit3TestAdapter`,
and `Microsoft.Playwright.NUnit` but none have corresponding `<PackageVersion>`
entries in `Directory.Packages.props`. With
`ManagePackageVersionsCentrally=true`, this is **build-breaking**.

### STRUCT-004 — Missing RootNamespace/AssemblyName (low)

`E2ETests.csproj` lacks explicit `<RootNamespace>` and `<AssemblyName>`
properties that all other projects declare. Relies on implicit defaults.

### STRUCT-005 — Orphaned Placeholder File (low)

`src/ContestJudging.Infrastructure/Class1.cs` is an empty scaffolding artifact
(`public class Class1 { }`).

### STRUCT-006 — Mixed Test Frameworks (informational)

Unit tests use **xUnit** (`ContestJudging.Tests`); E2E tests use **NUnit**
(`ContestJudging.E2ETests`). Two frameworks in one solution without documented
rationale.

### STRUCT-007 — .gitignore Missing SQLite Patterns (medium)

The project creates `contest.db` (SQLite database), but `.gitignore` lacks
patterns for `*.db`, `*.sqlite`, or `*.sqlite3`. Risk of accidentally committing
database files.

### STRUCT-008 — Redundant Property Overrides (low)

`Web.csproj` redundantly sets `TargetFramework`, `Nullable`, and
`ImplicitUsings` to the same values already defined in `Directory.Build.props`.
`E2ETests.csproj` does the same.

### STRUCT-009 — Orphaned testapp Project (medium)

The `testapp/` directory contains a standalone Blazor WASM scaffold outside the
solution, using hardcoded package versions (bypassing central package
management), a non-standard namespace (`testapp`), and containing stale `obj/`
build artifacts.

### STRUCT-010 — Global Trimming Settings (informational)

`Directory.Build.props` sets `IsTrimmable=true` and `EnableTrimAnalyzer=true`
globally, but the Infrastructure project uses EF Core which is not
trimming-safe. The Web project suppresses the resulting warnings
(`WASM0001;IL2111` in `NoWarn`). Trim settings should be scoped to the published
project.

---

## 4. Namespace Coherence

All projects in `src/` and `tests/` declare namespaces matching their project
names (e.g., `ContestJudging.Core.Entities`,
`ContestJudging.Services.Managers`). No cross-layer namespace leaks detected.

**Minor note:** The orphaned `testapp/` project uses namespace `testapp`,
inconsistent with the `ContestJudging.*` convention.

---

## 5. Directory.Build.props / Directory.Packages.props

- **Directory.Build.props** applies uniformly: `net10.0`, `Nullable=enable`,
  `ImplicitUsings=enable`, `EnforceCodeStyleInBuild=true`,
  `AnalysisLevel=latest`. Good.
- **Directory.Packages.props** manages central versions for all packages in
  `src/` and unit tests. E2E tests violate this (see STRUCT-003).
- **Issue:** `IsTrimmable` and `EnableTrimAnalyzer` are applied globally but
  should be scoped to the Blazor WASM project only (see STRUCT-010).

---

## 6. .gitignore

Comprehensive and follows the standard Visual Studio template. Covers `bin/`,
`obj/`, `.vs/`, `*.user`, JetBrains, macOS, Windows, Vim patterns. Missing
SQLite database patterns (see STRUCT-007). Ignoring `.agents/`, `.opencode/`,
`.github/instructions/` as project-specific APM overrides — appropriate.

---

## 7. Overall Assessment

**Grade: Needs Attention (Yellow)**

The dependency hierarchy is logically ordered (Core → Infrastructure → Services
→ Web) and circular-reference-free. However, the Web layer directly depends on
Infrastructure, breaking the intended 4-layer isolation. The E2E test project is
both absent from the solution and has a build-breaking central package
management violation. The orphaned `testapp/` project and `Class1.cs` suggest
incomplete cleanup from scaffolding.

### Recommended Fixes (priority order):

1. **Fix STRUCT-003 immediately** (build-breaking — add NUnit package versions)
2. **Fix STRUCT-002** (add E2ETests to solution file)
3. **Fix STRUCT-001** (move Infrastructure initialization into Services layer)
4. **Fix STRUCT-009** (delete orphaned testapp/)
5. **Fix STRUCT-007** (add SQLite patterns to .gitignore)
6. Resolve remaining low/informational items at leisure

**Files scanned:** 34 | **Findings:** 10 (1 critical, 3 medium, 4 low, 2
informational)
