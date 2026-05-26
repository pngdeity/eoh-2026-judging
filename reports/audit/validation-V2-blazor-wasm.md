# V2-C: Validation Report — P2-C (blazor-wasm)

**Validator:** V2-C **Validated Agent:** P2-C **Domain:** blazor-wasm **Date:**
2026-05-25

---

## Verdict: **PASS**

All 10 findings pass schema conformance, file/line resolution, ID uniqueness,
and cross-check analysis. No rejections.

---

## Metrics

| Metric         | Value |
| -------------- | ----- |
| Total findings | 10    |
| Passed         | 10    |
| Rejected       | 0     |
| Invalid rate   | 0%    |

---

## Schema Conformance

| Check                                                                   | Result |
| ----------------------------------------------------------------------- | ------ |
| `agent_id` "P2-C" matches `^[PV]\d-[A-E]$`                              | Pass   |
| `pass` = 2 (1-3 range)                                                  | Pass   |
| `domain` "blazor-wasm" in enum                                          | Pass   |
| `status` "success" in enum                                              | Pass   |
| `metrics.files_scanned` = 17 (integer, >=0)                             | Pass   |
| `metrics.findings_count` = 10 (integer, >=0, matches array)             | Pass   |
| All findings have required fields (`id`, `title`, `severity`, `files`)  | Pass   |
| All IDs match `^[A-Z]+-\d{3,}$` (`BW-001` through `BW-010`)             | Pass   |
| All severities in enum {critical, high, medium, low, informational}     | Pass   |
| All `files` arrays have `minItems: 1`, each entry matches `^[^:]+:\d+$` | Pass   |

---

## File/Line Resolution

All 12 unique files exist. All 34 individual file:line references resolve to
valid lines within their file's range. No ranges, no directories, no missing
files.

| File                                                                | Lines | Referenced at lines       | Status |
| ------------------------------------------------------------------- | ----- | ------------------------- | ------ |
| `src/ContestJudging.Web/Pages/Setup.razor.cs`                       | 150   | 37, 42, 49, 55            | Pass   |
| `src/ContestJudging.Web/Pages/Judging.razor.cs`                     | 218   | 44, 82                    | Pass   |
| `src/ContestJudging.Web/Pages/Results.razor.cs`                     | 69    | 20                        | Pass   |
| `src/ContestJudging.Web/wwwroot/index.html`                         | 34    | 14                        | Pass   |
| `src/ContestJudging.Web/Pages/Judging.razor`                        | 228   | 6, 66, 74, 84, 97, 162    | Pass   |
| `src/ContestJudging.Web/Layout/NavMenu.razor`                       | 49    | 4                         | Pass   |
| `src/ContestJudging.Web/Pages/Setup.razor`                          | 135   | 35, 67                    | Pass   |
| `src/ContestJudging.Web/Pages/Results.razor`                        | 82    | 55                        | Pass   |
| `src/ContestJudging.Web/Program.cs`                                 | 67    | 5, 34, 39, 46, 47, 51, 56 | Pass   |
| `src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs` | 82    | 76                        | Pass   |
| `src/ContestJudging.Web/wwwroot/sample-data/weather.json`           | 27    | 1                         | Pass   |
| `src/ContestJudging.Web/ContestJudging.Web.csproj`                  | 39    | 29, 30, 36                | Pass   |

Spot-checks of 6 line references against actual file content confirmed accuracy:

- `Setup.razor.cs:37` → `protected override async Task OnInitializedAsync()`
  with no try-catch
- `Setup.razor.cs:55` → `await LocalStorage.SetItemAsStringAsync(...)` without
  quota guard
- `Program.cs:39` → `if (localStorage.ContainKey("db_backup"))` — restore logic
- `Program.cs:56` → `await context.Database.EnsureCreatedAsync()` — after
  restore
- `index.html:14` → `<script type="importmap"></script>` — no Bootstrap JS
  present
- `Judging.razor:97` → accordion button with `data-bs-toggle="collapse"`

---

## ID Uniqueness

| Check                             | Result            |
| --------------------------------- | ----------------- |
| 10 IDs: `BW-001` through `BW-010` | Pass — all unique |

---

## Cross-Check Analysis

### BW-007 vs SEC-004

- **SEC-004** (P1): "Database backup in localStorage lacks integrity
  verification" — flags missing HMAC/checksum on backup data. Severity: medium.
  Files: Judging.razor.cs:76, Setup.razor.cs:49, Program.cs:39.
- **BW-007** (P2): "DB restore path lacks schema version, integrity, and basic
  validity checks (SEC-004 follow-up)" — extends SEC-004 with schema version
  check, SQLite header magic validation, and post-restore SELECT query
  verification. Severity: medium. Files: Program.cs:39,46,47,56,
  ContestDbContext.cs:76.
- **Verdict:** Consistent. BW-007 explicitly references SEC-004 via
  `related_findings` and build on it with deeper analysis of the full restore
  pipeline. No contradiction.

### BW-010 vs STRUCT-001

- **STRUCT-001** (P1): "Layer isolation violation: Web project directly
  references Infrastructure layer" — flags Web.csproj and Program.cs importing
  Infrastructure. Severity: medium.
- **BW-010** (P2): "STRUCT-001 follow-up: Infrastructure leak is contained to
  Program.cs only — no page components are contaminated" — depth analysis
  confirming the leak is limited to Program.cs, with no spread to Razor
  components. Severity: informational.
- **Verdict:** Complementary, not contradictory. BW-010 confirms STRUCT-001 and
  adds useful scope analysis. The lower severity (informational) is appropriate
  for a confirmatory follow-up rather than a new finding.

---

## Severity Consistency

### BW-004: "Bootstrap JavaScript not loaded — accordion UI non-functional"

**Severity assessed: high.** Analysis: the accordion controls the "Manual
Override / Correction" panel. While non-functional, the primary A/B judging
interface (judge cards, keyboard shortcuts) remains operational. This is a
secondary feature, not the core judging flow. "high" is appropriate — it
correctly signals significant impact without overstating. "critical" would
require the application's primary function to be broken, which it is not.

### Full severity distribution

| Severity      | Count | IDs                                            |
| ------------- | ----- | ---------------------------------------------- |
| high          | 1     | BW-004                                         |
| medium        | 6     | BW-001, BW-002, BW-003, BW-005, BW-006, BW-007 |
| low           | 2     | BW-008, BW-009                                 |
| informational | 1     | BW-010                                         |

No severity anomalies detected. All assignments are consistent with their
documented impact and the Audit Plan rubric.

---

## Markdown Cross-Check

| Check                                                                            | Result           |
| -------------------------------------------------------------------------------- | ---------------- |
| JSON finding count (metrics.findings_count = 10) vs MD table entries (10)        | Pass             |
| JSON IDs: `BW-001` through `BW-010` vs MD IDs in summary table + detail sections | Pass — 1:1 match |

---

## Conclusion

P2-C (blazor-wasm) passes validation with a 0% rejection rate. All required
schema fields, types, enums, and ID patterns are correct. All 34 file:line
references resolve to valid paths and line numbers. IDs are unique. Cross-checks
with P1 findings (SEC-004, STRUCT-001) show consistency without contradiction.
No retry is required for P2-C.
