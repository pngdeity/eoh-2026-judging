# V1-C Validation Report — P1-C (tests domain)

**Validator:** V1-C | **Pass:** 1 | **Date:** 2026-05-25

## Verdict: PASS

All 13 findings conform to the schema, all file references resolve, IDs are
unique, and severities are consistent.

## Summary

| Metric               | Value |
| -------------------- | ----- |
| Total findings       | 13    |
| Passed               | 13    |
| Rejected             | 0     |
| Invalid rate         | 0%    |
| P1-C retry required? | No    |

## Step 1 — Schema Conformance

All top-level required fields present (`agent_id`, `pass`, `domain`, `findings`,
`metrics`). `agent_id` "P1-C" matches `^[PV]\d-[A-E]$`. `domain` "tests" is a
valid enum value. `status` "success" is valid. `metrics` has required
`files_scanned` (9) and `findings_count` (13).

All 13 findings have required fields (`id`, `title`, `severity`, `files`). All
IDs match `^[A-Z]+-\d{3,}$`. All `files[]` entries match `^[^:]+:\d+$`. All
severity values are valid enum members.

## Step 2 — File/Line Resolution

All 17 unique file paths exist relative to repo root and all 23 file:line
references resolve to valid line numbers within the respective files:

| File                           | Lines | Referenced At | Valid? |
| ------------------------------ | ----- | ------------- | ------ |
| CoreTests.cs                   | 55    | 9, 12, 36     | Yes    |
| ScoringStrategyTests.cs        | 88    | 12            | Yes    |
| ValidationServiceTests.cs      | 231   | 153           | Yes    |
| PartitionServiceTests.cs       | 55    | 37            | Yes    |
| InfrastructureTests.cs         | 160   | 18, 35        | Yes    |
| ContestManagerTests.cs         | 131   | 21, 30        | Yes    |
| TrimmingSafetyTests.cs         | 52    | 19            | Yes    |
| AppE2ETests.cs                 | 38    | 3             | Yes    |
| ContestJudging.E2ETests.csproj | 24    | 11, 16        | Yes    |
| Program.cs                     | 67    | 1             | Yes    |
| Setup.razor.cs                 | 150   | 1             | Yes    |
| Judging.razor.cs               | 218   | 1             | Yes    |
| Results.razor.cs               | 69    | 1             | Yes    |
| ContestManager.cs              | 129   | 25, 119       | Yes    |
| ServiceCollectionExtensions.cs | 39    | 21            | Yes    |
| Class1.cs                      | 6     | 3             | Yes    |
| Directory.Packages.props       | 24    | 1             | Yes    |

## Step 3 — ID Uniqueness

IDs TEST-001 through TEST-013 are sequentially numbered and all unique. No
duplicates. Pattern `^[A-Z]+-\d{3,}$` satisfied by all IDs.

## Step 4 — Severity Consistency

| Severity      | Count | Finding IDs          | Assessment                                        |
| ------------- | ----- | -------------------- | ------------------------------------------------- |
| high          | 2     | TEST-001, TEST-002   | Defect masking + entire project gap — appropriate |
| medium        | 4     | TEST-003,004,005,008 | CI/build integrity — appropriate                  |
| low           | 4     | TEST-006,007,009,010 | Style/gap/coverage — appropriate                  |
| informational | 3     | TEST-011,012,013     | Dead code/contradictions/minor — appropriate      |

No unreasonable assignments found. The two `high` findings concern defect
masking through sequential `Assert.Throws` and an entirely untested project
layer, both justified. Informational findings are correctly scoped as
non-blocking observations.

## Step 5 — Markdown Cross-Check

- JSON: 13 findings (TEST-001 through TEST-013)
- Markdown: 13 findings (TEST-001 through TEST-013)
- All IDs, titles, severities, categories, and remediation text correspond
  between the two artifacts.

Minor discrepancy noted: TEST-010 `.md` lists one file reference
(`ContestManager.cs:119`) while `.json` contains two (`ContestManager.cs:119` +
`ContestManagerTests.cs:30`). This is a documentation presentation difference,
not a schema or correctness issue — the `.json` is the canonical artifact and
includes both references.

## Conclusion

P1-C's output is valid. No retry needed. Proceed to Pass 2 synthesis.
