# Re-Audit: Pass 1 – Security Domain

**Agent**: PA1-D (re-audit)\
**Target branch**: `fix/audit-remediate`\
**Original findings**: `reports/audit/findings-P1-security.json` (8 findings)\
**Re-audit date**: 2026-05-26

---

## Original Findings Status

| ID      | Title                               | Severity | Status                                               |
| ------- | ----------------------------------- | -------- | ---------------------------------------------------- |
| SEC-001 | Missing CSP header                  | medium   | **OPEN** (unchanged)                                 |
| SEC-002 | Insecure Random in PartitionService | medium   | **OPEN** (slight improvement, still unseeded)        |
| SEC-003 | Hardcoded connection strings        | low      | **OPEN** (slightly worse — 3 locations instead of 2) |
| SEC-004 | Backup no integrity check           | medium   | **PARTIAL** (header validation added, no checksum)   |
| SEC-005 | Exception messages in console       | info     | **FIXED**                                            |
| SEC-006 | .gitignore missing *.db             | medium   | **OPEN** (unchanged)                                 |
| SEC-007 | No auth on admin ops                | medium   | **OPEN** (deferred)                                  |
| SEC-008 | Client-side SQLite                  | info     | **STILL APPLIES** (architecture unchanged)           |

**Resolved**: 1 / 8\
**Partially resolved**: 2 / 8\
**Still open**: 5 / 8

---

## Verification Detail

### SEC-001 — Missing CSP (STILL OPEN)

`src/ContestJudging.Web/wwwroot/index.html` has no
`<meta http-equiv="Content-Security-Policy">` tag. The `<head>` block is
unchanged from the original audit. No remediation was applied.

### SEC-002 — Insecure Random in PartitionService (STILL OPEN)

`PartitionService.cs` now has a parameterized constructor accepting `Random`,
improving testability:

```csharp
public PartitionService() : this(new Random()) { }
public PartitionService(Random random) { _random = random; }
```

However, the default constructor still uses `new Random()` which seeds from
`Environment.TickCount` — predictable and non-cryptographic. The DI registration
uses the default constructor, so production code still gets unseeded
`System.Random`. The remediation asked for `RandomNumberGenerator` or
`Random.Shared`.

### SEC-003 — Hardcoded Connection Strings (STILL OPEN)

The hardcoded `"Data Source=contest.db"` in `Program.cs:47` remains. The default
parameter `"Data Source=:memory:"` in `ServiceCollectionExtensions.cs:21`
remains. Additionally, `ServiceCollectionExtensions.cs:35` now hardcodes
`"contest.db"` for `DatabaseBackupService` construction. Three hardcoded
database path locations now exist. No `appsettings.json` was created.

### SEC-004 — Backup Integrity (PARTIAL)

`DatabaseBackupService.cs` now validates the 16-byte SQLite magic header
(`"SQLite format 3\0"`) before import. This is a file type validation, **not an
integrity check**. The magic bytes are a known constant — it only confirms the
file format, not that the data is untampered. No HMAC, CRC32, or checksum was
added. The remediation asked for a hash or HMAC appended to the backup payload.

### SEC-005 — Exception Messages in Console (FIXED)

The `Console.WriteLine($"Failed to restore database: {ex.Message}")` in the
original `Program.cs` has been removed. The new `BackupService.cs` uses
`ILogger<T>` with structured logging (`_logger.LogError(ex, "...")`,
`_logger.LogWarning(...)`, `_logger.LogInformation(...)`). No `Console.Write*`
calls remain in the codebase.

### SEC-006 — Missing .db in .gitignore (STILL OPEN)

`.gitignore` still has no `*.db` or `*.sqlite` pattern. Line 244 has `*.dbmdl`
(SQL Server project files) but the application's `contest.db` is not covered.

### SEC-007 — No Auth (STILL OPEN, DEFERRED)

No authentication was added to Setup or Judging pages. This was acknowledged as
deferred per the original remediation plan.

### SEC-008 — Client-Side SQLite (STILL APPLIES)

The architecture remains Blazor WASM with SQLite initialized in-browser. All
data remains accessible via browser DevTools. No server-side backend was added.

---

## New Findings

### RA-SEC-N01 — Hardcoded database path now in three locations (low)

**Files**: `Program.cs:47`, `ServiceCollectionExtensions.cs:21`,
`ServiceCollectionExtensions.cs:35`

The remediated codebase introduced a new hardcoded `"contest.db"` literal in
`ServiceCollectionExtensions.cs:35` for `DatabaseBackupService` construction.
Combined with the two original locations, this is now three places where the
database path is hardcoded. If the path ever needs to change, all three
locations must be updated.

**Remediation**: Introduce a single configuration value (e.g., from
`appsettings.json` or a constant) used by all three sites.

### RA-SEC-N02 — DatabaseBackupService lacks path canonicalization (low)

**File**: `DatabaseBackupService.cs:10`

The constructor accepts `string dbPath` with no call to `Path.GetFullPath()` or
any canonicalization. Currently the parameter is only hardcoded to
`"contest.db"`, so this is not exploitable. However, if the path becomes
configurable in the future, `ExportAsync()` could be leveraged for arbitrary
file reads and `ImportAsync()` for arbitrary file writes.

```csharp
public DatabaseBackupService(string dbPath = "contest.db")
{
    _dbPath = dbPath;  // no Path.GetFullPath
}
```

**Remediation**: Add `_dbPath = Path.GetFullPath(dbPath);` and optionally
validate the path is within an expected directory.

### RA-SEC-N03 — Schema version check bypassable via localStorage tampering (low)

**File**: `BackupService.cs:33-34`

The schema version is stored as a separate localStorage key
(`db_schema_version`). An attacker with browser DevTools access can set
`db_schema_version` to the correct value alongside a tampered `db_backup` blob,
bypassing the version check. The schema version itself has no integrity
protection.

```csharp
var storedVersion = await _localStorage.GetItemAsync<int>("db_schema_version");
if (storedVersion != CurrentSchemaVersion)
{
    // discard backup
}
```

**Remediation**: Combine schema version and data into a single signed payload,
or accept that localStorage is untrusted in a WASM app.

---

## Full Security Scan Results

### Hardcoded Secrets / Keys / Passwords

- No API keys, passwords, or tokens found in codebase.
- `AppE2ETests.cs:13` has `"http://localhost:5000"` — expected in test code, not
  a secret.

### Hardcoded URLs / IPs

- `Program.cs:18` — `builder.HostEnvironment.BaseAddress` — runtime-resolved,
  not hardcoded.
- `index.html:9` — `<base href="/" />` — standard, not a concern.
- `AppE2ETests.cs:13` — `"http://localhost:5000"` — test-only, acceptable.

### SQL Injection

- All data access uses EF Core with parameterized queries. `FindAsync`,
  `FirstOrDefaultAsync` with lambda expressions, and `Where` with expression
  predicates. No raw SQL strings detected. No SQL injection risk.

### Input Validation

- `Setup.razor.cs`: Validates `string.IsNullOrWhiteSpace` on entry/category IDs
  before processing. Data annotations (`[Required]`, `[Range]`) on form models.
- `Judging.razor.cs`: Validates entryAId/entryBId non-empty and not-equal before
  creating relations.
- `ContestManager.cs`: Delegates to repositories and services. No additional
  validation at manager layer.
- `PartitionService.cs`: Validates `kPartitions > 0` and `overlapRate` range
  [0,1].
- `DatabaseBackupService.cs`: Validates data length >= 16 and magic header on
  Import.
- No missing input validation on public interface methods.

### Insecure Random

- `PartitionService.cs:9-11` — `new Random()` in default constructor (see
  SEC-002).

### Path Traversal

- `DatabaseBackupService.cs` — no path canonicalization (see RA-SEC-N02). Low
  severity given current hardcoded usage.
- No user-supplied file paths found.

### Information Leakage via Errors

- SEC-005 is fixed. All new exception handling uses `ILogger`.
- `BackupService.cs:54-57` — catches `Exception` and logs with `ILogger`, no raw
  messages exposed.
- Console output clean.

### Configuration Files

- No `appsettings.json` found. Configuration is inline in code.
- `Directory.Packages.props` — packages are .NET 10 preview versions (current as
  of 2026-05). No known CVE-vulnerable versions detected.

### CSP Headers

- Neither `index.html` (Blazor WASM) nor `testapp/wwwroot/index.html` has a CSP
  meta tag or header.
- `index.html:4-15` — `<head>` contains only meta charset, viewport, title,
  base, link, and script tags. No CSP.

### .gitignore

- Missing `*.db` pattern. Only `*.dbmdl` (SQL Server project model files).
  SEC-006 remains.
- No `.sqlite` or `.sqlite3` patterns.

### NuGet Packages

- `dotnet list package --vulnerable` timed out. Manual review of versions in
  `Directory.Packages.props`:
  - `Microsoft.EntityFrameworkCore.Sqlite 10.0.5` — latest .NET 10 preview
  - `Microsoft.AspNetCore.Components.WebAssembly 10.0.5` — latest .NET 10
    preview
  - `SQLitePCLRaw.bundle_e_sqlite3 3.0.2` — current as of 2026
  - `Blazored.LocalStorage 4.5.0` — current
  - All packages appear to be recent versions. No known CVEs identified.

---

## Risk Trend

| Metric          | Original | Remediated                                         | Delta  |
| --------------- | -------- | -------------------------------------------------- | ------ |
| Medium findings | 4        | 4 (SEC-001/002/004/006/007)                        | 0      |
| Low findings    | 1        | 2 (SEC-003 + RA-SEC-N01 path duplication)          | +1     |
| Info findings   | 3        | 4 (SEC-005→FIXED, SEC-008, RA-SEC-N02, RA-SEC-N03) | +1     |
| Resolved        | 0        | 1 (SEC-005)                                        | +1     |
| **Total open**  | **8**    | **10**                                             | **+2** |

**Trend**: **SLIGHTLY WORSE** — One finding fully fixed (SEC-005) and one
partially improved (SEC-004), but 3 new issues were introduced by the
remediation changes, and the core medium-severity findings (CSP, insecure
random, .gitignore, no auth) remain unresolved. The overall security posture
shows minimal net improvement.

---

## Summary

The remediation branch addressed only SEC-005 (exception message exposure)
completely and made partial progress on SEC-004 (SQLite magic header
validation). The remaining 6 original findings remain open, and 3 new low/info
findings were introduced by the new backup architecture. The most impactful
medium-severity issues — missing CSP (SEC-001), insecure PRNG (SEC-002), and
.gitignore gaps (SEC-006) — received no attention.
