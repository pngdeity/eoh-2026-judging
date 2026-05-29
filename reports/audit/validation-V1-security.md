# Validation Report — V1-D (Pass 1, Security Domain)

**Validator:** V1-D\
**Validated agent:** P1-D\
**Date:** 2026-05-25\
**Verdict:** **PASS**

---

## Summary

| Metric            | Value      |
| ----------------- | ---------- |
| Total findings    | 8          |
| Passed            | 8          |
| Rejected          | 0          |
| Invalid rate      | 0.00%      |
| P1-D retry needed | No (< 50%) |

---

## Step 1 — Schema Conformance: PASS

- Top-level required fields (`agent_id`, `pass`, `domain`, `findings`,
  `metrics`) all present with correct types.
- `agent_id` (`P1-D`) matches pattern `^[PV]\d-[A-E]$`.
- `domain` (`security`) is in the enum.
- `pass` is integer 1 (valid range 1–3).
- `status` (`success`) is in the enum.
- `metrics.findings_count` (8) matches actual findings array length (8).

**Per-finding required fields** (`id`, `title`, `severity`, `files`) present for
all 8 findings:

- All 8 `id` values match pattern `^[A-Z]+-\d{3,}$`.
- All `severity` values are in enum (`medium`, `low`, `informational`).
- All `files` arrays have `minItems >= 1` and each entry matches pattern
  `^[^:]+:\d+$`.
- All optional `effort` and `status` fields use valid enum values where present.

## Step 2 — File/Line Resolution: PASS

All 16 `file:line` references resolve to existing files with line numbers within
the file's actual range:

| Finding | File                                                                  | Line | File Range | Valid |
| ------- | --------------------------------------------------------------------- | ---- | ---------- | ----- |
| SEC-001 | src/ContestJudging.Web/wwwroot/index.html                             | 4    | 34 lines   | Yes   |
| SEC-002 | src/ContestJudging.Services/Partitioning/PartitionService.cs          | 9    | 43 lines   | Yes   |
| SEC-002 | src/ContestJudging.Services/Partitioning/PartitionService.cs          | 24   | 43 lines   | Yes   |
| SEC-003 | src/ContestJudging.Web/Program.cs                                     | 66   | 67 lines   | Yes   |
| SEC-003 | src/ContestJudging.Services/Extensions/ServiceCollectionExtensions.cs | 21   | 39 lines   | Yes   |
| SEC-004 | src/ContestJudging.Web/Pages/Judging.razor.cs                         | 76   | 218 lines  | Yes   |
| SEC-004 | src/ContestJudging.Web/Pages/Setup.razor.cs                           | 49   | 150 lines  | Yes   |
| SEC-004 | src/ContestJudging.Web/Program.cs                                     | 39   | 67 lines   | Yes   |
| SEC-005 | src/ContestJudging.Web/Program.cs                                     | 49   | 67 lines   | Yes   |
| SEC-006 | .gitignore                                                            | 1    | 500 lines  | Yes   |
| SEC-007 | src/ContestJudging.Web/Pages/Setup.razor.cs                           | 59   | 150 lines  | Yes   |
| SEC-007 | src/ContestJudging.Web/Pages/Setup.razor.cs                           | 68   | 150 lines  | Yes   |
| SEC-007 | src/ContestJudging.Web/Pages/Judging.razor.cs                         | 147  | 218 lines  | Yes   |
| SEC-007 | src/ContestJudging.Web/Pages/Judging.razor.cs                         | 180  | 218 lines  | Yes   |
| SEC-008 | src/ContestJudging.Web/Program.cs                                     | 21   | 67 lines   | Yes   |
| SEC-008 | src/ContestJudging.Infrastructure/Persistence/ContestDbContext.cs     | 41   | 82 lines   | Yes   |

Evidence snippets were spot-checked against actual file content (SEC-002,
SEC-003, SEC-004, SEC-005, SEC-007, SEC-008) — all match.

## Step 3 — ID Uniqueness: PASS

Eight unique IDs (SEC-001 through SEC-008). No duplicates. No conflicts with
reserved patterns.

## Step 4 — Severity Consistency: PASS

| Severity      | Count | Findings                                    |
| ------------- | ----- | ------------------------------------------- |
| medium        | 5     | SEC-001, SEC-002, SEC-004, SEC-006, SEC-007 |
| low           | 1     | SEC-003                                     |
| informational | 2     | SEC-005, SEC-008                            |

All severity assignments are consistent with the described issues:

- SEC-001 (missing CSP): medium — defense-in-depth for WASM app, appropriate.
- SEC-002 (weak PRNG): medium — competition fairness concern, reasonable.
- SEC-003 (hardcoded config): low — SQLite has no credentials, appropriate.
- SEC-004 (no integrity check): medium — localStorage tampering risk,
  reasonable.
- SEC-005 (exception message): informational — Console.WriteLine in browser,
  appropriate.
- SEC-006 (missing .gitignore): medium — preventative but high potential impact,
  acceptable.
- SEC-007 (no auth): medium — public admin page, correct.
- SEC-008 (client-side SQLite): informational — architecture note, correct.

## Step 5 — Markdown Cross-Check: PASS

- `findings-P1-security.md` contains 8 findings, same as JSON.
- All 8 IDs match between Markdown table, detailed sections, and JSON.
- Severity counts match: 5 medium, 1 low, 2 informational in both.
- Finding titles match between Markdown summary table and JSON.

---

## Rejected Findings

_None._

---

## Minor Observations (non-blocking)

1. **SEC-006**: `files` references `.gitignore:1` (start of file) but the actual
   evidence is about patterns at ~line 244. The line is in range, so this is a
   precision issue, not a rejection.
2. **SEC-008**: `files` references `ContestDbContext.cs:41` (class declaration)
   while the evidence snippet references line 68 (`var path = "contest.db"`).
   Both lines are valid; the evidence line is more directly relevant. Not a
   rejection.
