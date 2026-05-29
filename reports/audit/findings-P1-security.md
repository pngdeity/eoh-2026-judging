# Security Audit Report — Pass 1, Agent P1-D

**Agent ID:** P1-D\
**Domain:** security\
**Pass:** 1\
**Date:** 2026-05-25\
**Status:** success

---

## Scope

- All `.cs` source files under `src/` (28 files, excluding `obj/`/`bin/`)
- `Directory.Packages.props` — NuGet package versions
- `.gitignore` — secret/database exclusion patterns
- `src/ContestJudging.Web/wwwroot/index.html` — CSP/security headers
- `release/web.config` — IIS deployment config
- `.github/workflows/pipeline.yml` — CI/CD

---

## Key Findings Summary

| ID      | Severity      | Title                                                        |
| ------- | ------------- | ------------------------------------------------------------ |
| SEC-001 | medium        | Missing Content-Security-Policy header                       |
| SEC-002 | medium        | Insecure random number generation (System.Random)            |
| SEC-003 | low           | Hardcoded SQLite connection strings                          |
| SEC-004 | medium        | Database backup in localStorage lacks integrity verification |
| SEC-005 | informational | Exception message exposed to browser console                 |
| SEC-006 | medium        | Missing `.db`/`.sqlite` patterns in `.gitignore`             |
| SEC-007 | medium        | No authentication/authorization on administrative operations |
| SEC-008 | informational | Client-side SQLite — all data inherently accessible          |

---

## Detailed Findings

### SEC-001 — Missing Content-Security-Policy Header (medium)

**Rule violated:** OWASP Top 10 A05:2021 — Security Misconfiguration\
**CWE:** CWE-1021 (Improper Restriction of Rendered UI Layers)

The `index.html` file contains no `<meta http-equiv="Content-Security-Policy">`
tag, nor is a CSP header served by the web server. For a Blazor WebAssembly app
that executes .NET IL via WebAssembly and JavaScript interop, a CSP provides
defense-in-depth against XSS, inline script injection, and untrusted resource
loading.

```html
<!-- index.html:4-15 — no CSP meta tag present -->
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>EOH 2026 Contest Judging</title>
  ...
</head>
```

**Remediation:** Add a `Content-Security-Policy` meta tag (or HTTP response
header from the server) with directives appropriate for Blazor WASM:

```
Content-Security-Policy: default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'self' data:;
```

**Effort:** small

---

### SEC-002 — Insecure Random Number Generation (System.Random) (medium)

**Rule violated:** CWE-338 (Use of Cryptographically Weak PRNG)\
**File:** `src/ContestJudging.Services/Partitioning/PartitionService.cs:9`

The `PartitionService` uses `System.Random` for partition generation, including
bridge node ("overlap") selection. `System.Random` is a seeded PRNG with
predictable output if the seed is known or guessable (default seed is
`Environment.TickCount`).

```csharp
// PartitionService.cs:9
private readonly Random _random = new();

// PartitionService.cs:24 — used for randomization that affects competition fairness
var shuffled = allEntryIdsList.OrderBy(x => _random.Next()).ToList();
```

In a competition judging context, partition assignments should ideally be
unbiased and unpredictable to prevent any judge from anticipating pairings.
While this is not a security vulnerability per se (no cryptographic operations
involved), it introduces avoidable predictability.

**Remediation:** Use `System.Security.Cryptography.RandomNumberGenerator` or
`System.Random.Shared` (which is more robust in modern .NET). If
non-cryptographic randomness is acceptable, at least document the reasoning.

**Effort:** trivial

---

### SEC-003 — Hardcoded SQLite Connection Strings (low)

**Files:**

- `src/ContestJudging.Web/Program.cs:66`
- `src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs:21`

SQLite connection strings are hardcoded in two places. While these are not
secrets (SQLite has no authentication), hardcoded configuration values hinder
maintenance and environment switching (in-memory for tests vs. file-based for
production).

```csharp
// Program.cs:66
services.AddContestJudgingServices("Data Source=contest.db");

// ServiceCollectionExtensions.cs:21
public static IServiceCollection AddContestJudgingServices(
    this IServiceCollection services, string connectionString = "Data Source=:memory:")
```

**Remediation:** Move the connection string to an external configuration source
(e.g., `appsettings.json` for Blazor WASM). Remove the default parameter to
force explicit configuration.

**Effort:** trivial

---

### SEC-004 — Database Backup in localStorage Lacks Integrity Verification (medium)

**Rule violated:** CWE-345 (Insufficient Verification of Data Authenticity)

**Files:**

- `src/ContestJudging.Web/Pages/Judging.razor.cs:76-84`
- `src/ContestJudging.Web/Pages/Setup.razor.cs:49-57`
- `src/ContestJudging.Web/Program.cs:39-53`

The entire SQLite database is serialized to Base64 and stored in `localStorage`
under the key `db_backup`. On app startup, the backup is read and restored
without any integrity check (no HMAC, checksum, or version identifier).

```csharp
// Judging.razor.cs:80-83 — backup with no integrity seal
var data = await ContestManager.ExportDataAsync();
if (data.Length > 0)
{
    await LocalStorage.SetItemAsStringAsync("db_backup", Convert.ToBase64String(data));
}

// Program.cs:41-47 — restore with no validation
var backupBase64 = localStorage.GetItemAsString("db_backup");
if (!string.IsNullOrEmpty(backupBase64))
{
    var backupBytes = Convert.FromBase64String(backupBase64);
    await contestManager.ImportDataAsync(backupBytes);
}
```

A malicious actor (or a buggy script) with access to the browser's localStorage
could modify the backup, inserting crafted SQLite data. Since EF Core loads the
restored database directly, tampered data would be processed by the
application's scoring pipeline without detection.

**Remediation:** Append an HMAC-SHA256 to the backup before storage, and verify
it on restore. Use a hardcoded key (acceptable for a client-side app where the
key is in the WASM binary anyway) or a simple checksum as a lightweight
integrity guard.

**Effort:** small

---

### SEC-005 — Exception Message Exposed to Browser Console (informational)

**File:** `src/ContestJudging.Web/Program.cs:49-51`

Exception messages are written directly to `Console.WriteLine`, which in Blazor
WASM renders to the browser console.

```csharp
catch (Exception ex)
{
    Console.WriteLine($"Failed to restore database: {ex.Message}");
}
```

While this is client-side only (no server leakage), exception messages can
reveal internal data structures. Consider logging only a generic failure message
and rendering the full exception in debug builds.

**Remediation:** Use `ILogger` instead of `Console.WriteLine`. In production
builds, log a generic message; include exception details only in debug mode.

**Effort:** trivial

---

### SEC-006 — Missing `.db` and `.sqlite` Patterns in `.gitignore` (medium)

**File:** `.gitignore`

The application uses a SQLite database file (`contest.db`). The `.gitignore`
file contains `*.dbmdl` (SQL Server model files) but does **not** include `*.db`
or `*.sqlite` patterns. An accidental `git add .` could commit the database
file, potentially leaking contest data (entries, scores, relations) into version
control history.

The `.gitignore` also has `*.pfx` excluded (line 247) and `*.snk` commented out
(line 253), which is correct for preventing certificate leaks.

**Remediation:** Add the following patterns to `.gitignore`:

```
*.db
*.sqlite
*.sqlite3
```

**Effort:** trivial

---

### SEC-007 — No Authentication/Authorization on Administrative Operations (medium)

**Rule violated:** CWE-306 (Missing Authentication for Critical Function)

**Files:**

- `src/ContestJudging.Web/Pages/Setup.razor.cs` — exposes `ClearCategories()`,
  `ClearEntries()`, `AddCategory()`, `AddEntry()`
- `src/ContestJudging.Web/Pages/Judging.razor.cs` — exposes `RecordResult()`,
  `AddRelation()`, `DeleteRelation()`
- `src/ContestJudging.Web/Pages/Results.razor.cs` — exposes `CalculateResults()`

The entire application runs client-side in the browser with no authentication
layer. The Setup page allows any user with access to the URL to:

- Add/delete judging categories
- Add/delete/bulk-import entries
- Clear all categories and entries
- Generate partitions

When deployed to GitHub Pages (as configured in the CI/CD pipeline), these
operations are publicly accessible to anyone who knows the URL. There is no
login, no password protection, and no session validation.

**Remediation:** For a client-side-only app, options include:

1. Add a simple passphrase check on the Setup page (stored in `localStorage`).
2. If server-side deployment is planned, add ASP.NET Core Identity or a simple
   token-based auth.
3. At minimum, document the "trusted judge device" deployment model and warn
   against public hosting without additional protection.

**Effort:** small

---

### SEC-008 — Client-Side SQLite — All Data Inherently Accessible (informational)

**Architecture note.** Since this is a Blazor WebAssembly app using EF Core with
SQLite (via `SQLitePCLRaw.bundle_e_sqlite3`), the entire database runs in the
browser's WebAssembly sandbox. Any user with browser DevTools can:

- Inspect the SQLite database contents via the filesystem API
- Modify localStorage entries directly
- Call JavaScript interop to manipulate application state
- Use the browser network tab to observe all fetched data

This is inherent to Blazor WASM and is not a code defect. However, it should be
documented clearly: this app is designed to run on a judge's local trusted
device, and all data should be considered publicly readable on that device.

**Remediation:** Document the trust model in a `SECURITY.md` or `README.md`. If
data confidentiality is required, consider a backend API with server-side
SQLite.

**Effort:** trivial (documentation only)

---

## NuGet Package Audit

**Source:** `Directory.Packages.props`

| Package                                     | Version | Release Date (approx) | Status          |
| ------------------------------------------- | ------- | --------------------- | --------------- |
| Microsoft.EntityFrameworkCore.Sqlite        | 10.0.5  | 2025-11               | Current .NET 10 |
| Microsoft.Extensions.DependencyInjection    | 10.0.5  | 2025-11               | Current .NET 10 |
| Microsoft.AspNetCore.Components.WebAssembly | 10.0.5  | 2025-11               | Current .NET 10 |
| SQLitePCLRaw.bundle_e_sqlite3               | 3.0.2   | 2025-10               | Recent          |
| MathNet.Numerics                            | 5.0.0   | 2025-03               | Current major   |
| Blazored.LocalStorage                       | 4.5.0   | 2024-03               | Stable          |
| Microsoft.NET.Test.Sdk                      | 17.14.1 | 2025-11               | Current         |
| xunit                                       | 2.9.3   | 2025-08               | Current         |
| coverlet.collector                          | 6.0.4   | 2024-12               | Stable          |
| Moq                                         | 4.20.72 | 2024-08               | Stable          |

All packages are recent with no known critical CVEs as of May 2026. The CI/CD
pipeline includes an OSV-Scanner step (`google/osv-scanner-action@v2.3.5`) for
dependency vulnerability scanning.

---

## Config & Deployment Files

### `release/web.config`

An IIS `web.config` with URL rewrite rules for SPA fallback. No sensitive
values. No CSP or security headers emitted. The MIME type mappings are standard
for Blazor WASM.

### `.github/workflows/pipeline.yml`

Standard .NET CI/CD. Includes:

- `dotnet format --verify-no-changes` (formatting check)
- `osv-scanner-action` (vulnerability scanning)
- Deploy to GitHub Pages

No secrets management issues observed. GitHub Pages deployment uses
`GITHUB_TOKEN` via standard actions.

---

## `.gitignore` Analysis

| Pattern Type              | Status        | Details                 |
| ------------------------- | ------------- | ----------------------- |
| `.env`                    | Present       | Line 7                  |
| `*.db` / `*.sqlite`       | **Missing**   | See SEC-006             |
| `*.pfx`                   | Present       | Line 247                |
| `*.snk`                   | Commented out | Line 253                |
| `appsettings.Development` | **Missing**   | Not explicitly excluded |
| `**/*.pdf` / `**/*.docx`  | Present       | Lines 485-486           |

---

## Metrics

| Metric        | Value |
| ------------- | ----- |
| Files scanned | 28    |
| Lines of code | 1,629 |
| Findings      | 8     |
| Critical      | 0     |
| High          | 0     |
| Medium        | 5     |
| Low           | 1     |
| Informational | 2     |
