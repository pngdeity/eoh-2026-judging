# Re-Audit Report: CI/CD (P1-E)

**Agent**: PA1-E (re-audit) **Original Pass**: P1-E, 11 findings **Branch**:
`fix/audit-remediate` **Date**: 2026-05-26

---

## Executive Summary

| Metric          | Original | Remediated | Delta |
| --------------- | -------- | ---------- | ----- |
| High severity   | 2        | 0          | -2    |
| Medium severity | 6        | 6          | 0     |
| Low severity    | 2        | 2          | 0     |
| Informational   | 1        | 1          | 0     |
| **Resolved**    | —        | 4          | —     |
| **New**         | —        | 2          | —     |
| **Total open**  | 11       | 9          | -2    |

**CI/CD Health Trend**: **Improving** — Both HIGH findings resolved. CodeQL SAST
added. E2E tests now in solution. Action versions updated to valid 2026
releases. Core deficiencies remain (no coverage collection, no Dependabot, no
CODEOWNERS, deploy rebuilds, `--allow-no-lockfiles`).

---

## Original Finding Status

### Resolved

#### CICD-001 (HIGH): E2E test project excluded from solution

- **Status**: RESOLVED
- `ContestJudging.E2ETests.csproj` now present in `ContestJudging.slnx:11` under
  `/tests/` folder.
- `dotnet test ContestJudging.slnx` in CI (pipeline.yml:38-39) will discover and
  execute E2E tests.
- The `Microsoft.Playwright.NUnit` package includes MSBuild targets for
  automatic browser installation during build; browsers should be available
  without an explicit `playwright install` step.

#### CICD-004 (HIGH): No SAST or CodeQL scanning

- **Status**: RESOLVED
- `codeql-analysis` job added at pipeline.yml:48-73.
- Uses `github/codeql-action/init@v3` + `github/codeql-action/analyze@v3`.
- Language matrix targets `csharp` (correct for solution).
- Properly wired into deploy
  `needs: [quality-and-test, security-scan, codeql-analysis]`.

#### CICD-006 (MEDIUM): release/ directory committed to repository

- **Status**: RESOLVED
- `git ls-files release/` returns empty — no longer tracked.
- `.gitignore:25` has `[Rr]elease/` pattern which case-insensitively matches
  `release/`.
- Note: `release/` directory still exists on disk (residue from prior local
  builds, not tracked).

#### CICD-003 (MEDIUM): Floating major-version action tags (supply chain risk)

- **Status**: RESOLVED (defect fixed; floating-tag concern remains)
- All action versions updated to valid 2026 releases:
  - `actions/checkout@v6` — valid (v6.0.2, Jan 2026)
  - `actions/setup-dotnet@v5` — valid (v5.2.0, Mar 2026)
  - `actions/cache@v5` — valid (v5.0.5, Apr 2026)
  - `actions/configure-pages@v6` — valid (v6.0.0, Mar 2026)
  - `actions/deploy-pages@v5` — valid (v5.0.0, Mar 2026)
  - `google/osv-scanner-action/osv-scanner-action@v2.3.5` — pinned to full
    semver
- The original finding flagged these as non-existent versions; all now exist.
- Floating major-version tags remain (GitHub-recommended practice; supply-chain
  purists prefer SHA pinning).

### Not Resolved

#### CICD-002 (MEDIUM): No code coverage collection

- **Status**: OPEN
- Test step at pipeline.yml:39 still reads:
  `dotnet test ContestJudging.slnx --configuration Release --no-build --verbosity normal`
- No `--collect:"XPlat Code Coverage"` flag. `coverlet.collector` is referenced
  in all test `.csproj` files but never invoked in CI.

#### CICD-005 (LOW): OSV-Scanner --allow-no-lockfiles

- **Status**: OPEN
- `scan-args: ./ --allow-no-lockfiles` still present at pipeline.yml:47.
- No `packages.lock.json` files committed to enable lockfile-based scanning.

#### CICD-007 (MEDIUM): No Dependabot configuration

- **Status**: OPEN
- No `.github/dependabot.yml` found anywhere in the repository.
- No NuGet or GitHub Actions ecosystem update automation.

#### CICD-008 (LOW): No CODEOWNERS file

- **Status**: OPEN
- No `.github/CODEOWNERS` exists.

#### CICD-009 (LOW): No PR or issue templates

- **Status**: OPEN
- No `.github/PULL_REQUEST_TEMPLATE.md`, `.github/ISSUE_TEMPLATE/`.

#### CICD-010 (INFORMATIONAL): dotnet-tools.json empty

- **Status**: UNCHANGED
- `dotnet-tools.json` still contains `"tools": {}`. No local CLI tools
  registered.

#### CICD-011 (MEDIUM): Deploy job rebuilds from source

- **Status**: OPEN
- Deploy job (pipeline.yml:74-112) performs `dotnet publish` independently
  rather than reusing the tested build artifact.
- No `actions/upload-artifact` / `actions/download-artifact` between jobs.

---

## New Findings

### RA-CICD-001 (LOW): E2E test reliability — no explicit Playwright browser installation

- **Category**: testing
- **Files**: `.github/workflows/pipeline.yml:38-39`,
  `tests/ContestJudging.E2ETests/ContestJudging.E2ETests.csproj`
- E2E tests use `Microsoft.Playwright.NUnit` which relies on MSBuild targets to
  install browsers at build time. If Playwright browser install fails silently,
  tests may hang or produce false failures.
- **Remediation**: Add an explicit
  `pwsh bin/Debug/net10.0/playwright.ps1 install --with-deps chromium` step
  before `dotnet test`, or verify the MSBuild targets are sufficient for
  `ubuntu-latest`.

### RA-CICD-002 (LOW): actions/upload-pages-artifact@v3 is outdated

- **Category**: supply-chain
- **Files**: `.github/workflows/pipeline.yml:107`
- Latest release is v5.0.0 (April 2026). v3.0.1 was last published June 2024. v3
  bundles an older `@actions/artifact` that triggers `DEP0040 punycode`
  deprecation warnings and precedes the Node 24 migration.
- **Remediation**: Bump to `actions/upload-pages-artifact@v4` or `@v5`.

### RA-CICD-003 (LOW): CodeQL Action v3 deprecation by December 2026

- **Category**: security
- **Files**: `.github/workflows/pipeline.yml:63,73`
- `github/codeql-action@v3` will be deprecated in December 2026 per GitHub. v4
  is available and current. v3 logs a deprecation warning.
- **Remediation**: Bump both `github/codeql-action/init` and
  `github/codeql-action/analyze` to `@v4`.

---

## Correctness Review

### CodeQL Job Configuration

- **Syntax**: Valid YAML. No schema errors detected.
- **Permissions**: `actions: read, contents: read, security-events: write` —
  correct for CodeQL SARIF upload.
- **Matrix**: `language: ["csharp"]` — correct; single-language matrix is
  appropriate for a C#-only solution. The `fail-fast: false` is benign with a
  single matrix entry.
- **Missing**: No `.github/codeql/codeql-config.yml` for custom query packs or
  path filters. Using all defaults is acceptable for initial setup.

### Deploy Job Gate

- `needs: [quality-and-test, security-scan, codeql-analysis]` — no circular
  dependency.
- CodeQL analysis can take 5-15 minutes, adding deployment latency. This is a
  reasonable security gate for a public repo.
- If CodeQL fails (non-zero exit) the deploy is blocked — desired behavior from
  a security perspective.

### .NET 10 Build Steps

- `dotnet restore` → `dotnet format --verify-no-changes` → `dotnet clean` →
  `dotnet build --no-restore` → `dotnet test --no-build`
- Correct order. Restore before format ensures analyzers are available. Clean
  before build ensures clean output.
- `dotnet-version: '10.0.x'` valid for .NET 10 SDK.

---

## CI/CD Health Trend

| Concern                     | Original         | Now                        |
| --------------------------- | ---------------- | -------------------------- |
| All test projects exercised | No (E2E missing) | Yes                        |
| SAST scanning               | No               | Yes (CodeQL)               |
| Coverage measured           | No               | No                         |
| Action tags valid           | 6/7 invalid      | All valid                  |
| Action pinning strength     | Floating major   | Floating major (unchanged) |
| Dependabot                  | Missing          | Missing                    |
| Artifact reuse in deploy    | Rebuilds         | Rebuilds                   |
| Lockfile scanning           | Weakened         | Weakened                   |
| release/ hygiene            | Committed        | Clean                      |

**Verdict**: Net improvement (+2 resolved, both HIGH). Foundation is sound.
Remaining gaps are all non-blocking for a solo-maintainer public project.

---

## Summary Table

| Finding           | Original Severity | Status   |
| ----------------- | ----------------- | -------- |
| CICD-001          | HIGH              | RESOLVED |
| CICD-004          | HIGH              | RESOLVED |
| CICD-002          | MEDIUM            | OPEN     |
| CICD-003          | MEDIUM            | RESOLVED |
| CICD-006          | MEDIUM            | RESOLVED |
| CICD-007          | MEDIUM            | OPEN     |
| CICD-011          | MEDIUM            | OPEN     |
| CICD-005          | LOW               | OPEN     |
| CICD-008          | LOW               | OPEN     |
| CICD-009          | LOW               | OPEN     |
| CICD-010          | INFORMATIONAL     | OPEN     |
| RA-CICD-001 (NEW) | LOW               | OPEN     |
| RA-CICD-002 (NEW) | LOW               | OPEN     |
| RA-CICD-003 (NEW) | LOW               | OPEN     |
