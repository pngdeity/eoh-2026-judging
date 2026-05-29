# EOH-2026-Judging Multipass Multiagent Codebase Audit Plan

**Branch:** `fix/audit-codebase` **Project:** .NET 10 Blazor WebAssembly app —
4-layer architecture (Core / Infrastructure / Services / Web) **Artifact
directory:** `reports/audit/`

---

## Pass 0 — Preflight

### 0.0 Technology Stack Awareness & Documentation Verification

**Goal:** Ensure all agents work from current API documentation, not stale
training data. This pass runs before any analysis agent is dispatched and
repeats between passes if technology versions are found to be outdated.

| Step  | Action                                                                                                                                                                                                                                                                                                                   |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 0.0.1 | **Enumerate the technology stack.** Read all `.csproj`, `Directory.Packages.props`, `dotnet-tools.json`, and any dependency manifests. Compile a complete list of every library, framework, and tool with its pinned version. Include the .NET SDK version (`global.json` or `Directory.Build.props` `TargetFramework`). |
| 0.0.2 | **Resolve library IDs via Context7.** For each library, run `ctx7 library <name> "<relevant query>"` to obtain the canonical library ID. Record the library ID and the latest indexed version.                                                                                                                           |
| 0.0.3 | **Audit version freshness.** Compare project-pinned versions against the latest indexed versions. Flag any package >2 major versions behind. Produce a `reports/audit/tech-stack.json` manifest.                                                                                                                         |
| 0.0.4 | **Query current docs for architectural patterns.** For each technology, query `ctx7 docs <libraryId> "<architectural concern>"` to validate the expected patterns. Key queries depend on the technology:                                                                                                                 |
|       | - **ASP.NET Core / Blazor:** Service lifetime behavior in WASM, `AddDbContextFactory` vs `AddDbContext`, render mode restrictions                                                                                                                                                                                        |
|       | - **EF Core:** Migration applicability for client-side SQLite, `EnsureCreatedAsync()` limitations, transaction patterns                                                                                                                                                                                                  |
|       | - **Blazored.LocalStorage:** API surface (quota checks, serialization limits, error handling)                                                                                                                                                                                                                            |
|       | - **xUnit:** `[Theory]` / `[InlineData]` best practices, async test patterns, `Assert.Throws` behavior                                                                                                                                                                                                                   |
|       | - **Moq:** Setup/Verify patterns, strict vs loose mocks, `It.IsAny` usage                                                                                                                                                                                                                                                |
|       | - **MathNet.Numerics:** Available distribution estimation APIs, linear algebra patterns                                                                                                                                                                                                                                  |
| 0.0.5 | **Produce a technology constraints document** (`reports/audit/tech-constraints.md`) that every downstream agent MUST read before analysis. This document captures:                                                                                                                                                       |
|       | - Architecture-specific constraints (e.g., "standalone Blazor WASM — no server, no migrations runner, `EnsureCreatedAsync` is standard for client-side SQLite")                                                                                                                                                          |
|       | - API deprecations to watch for                                                                                                                                                                                                                                                                                          |
|       | - Version-specific behavior differences                                                                                                                                                                                                                                                                                  |
|       | - Remediation guardrails (e.g., "do not suggest EF migrations for client-only SQLite", "do not suggest server-side patterns for WASM")                                                                                                                                                                                   |
| 0.0.6 | **Inject constraints into agent prompts.** Every analysis agent's prompt MUST include the relevant sections from `tech-constraints.md` for its domain. This prevents agents from proposing architecturally inappropriate remediations.                                                                                   |

#### Documentation Verification Agent (runs retroactively if Pass 0.0 is added post-audit)

If the audit was executed without Pass 0.0, a retroactive **Doc Verification
Agent** runs after Pass 3 to validate findings against current docs:

| Step | Action                                                                                              |
| ---- | --------------------------------------------------------------------------------------------------- |
| R.1  | For each domain, query current docs for the 3 most impactful findings that make API-specific claims |
| R.2  | Classify each claim as CONFIRMED, PARTIALLY CONTESTED, or CONTESTED                                 |
| R.3  | Flag findings whose remediation is architecturally inappropriate for the project type               |
| R.4  | Produce `reports/audit/validation-doc-verification.md`                                              |

#### Technology Version Lag Escalation

| Lag Severity    | Threshold                   | Action                                                                                                           |
| --------------- | --------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| **Current**     | Same major.minor            | Proceed normally                                                                                                 |
| **Minor drift** | Same major, behind on minor | Note in tech-constraints. Agents should check for breaking changes in changelog.                                 |
| **Major drift** | >2 major versions behind    | Escalate to human. Agents should treat API findings as suspect until confirmed. Consider upgrading before audit. |
| **Unsupported** | EOL or preview-only         | Escalate to human. Do not audit against unsupported versions.                                                    |

### 0.1—0.5 Standard Preflight

| Step | Action                                                               |
| ---- | -------------------------------------------------------------------- |
| 0.1  | Confirm audit scope and severity rubric                              |
| 0.2  | Create `reports/audit/` directory on `fix/audit-codebase`            |
| 0.3  | Establish the JSON output schema and the Markdown reporting template |
| 0.4  | Establish per-pass timeout windows                                   |
| 0.5  | Define "done" for each pass                                          |

### JSON Output Schema

Every agent emits a JSON artifact alongside its markdown report. Schema defined
in `FINDING-SCHEMA.json`.

### Severity Rubric

| Severity          | Definition                                                                                                  |
| ----------------- | ----------------------------------------------------------------------------------------------------------- |
| **Critical**      | Data loss, security breach, correctness failure (algorithm returns wrong answer), crash-on-startup          |
| **High**          | Performance degradation under load, memory leak, broken critical feature, EF Core N+1                       |
| **Medium**        | Code smell likely to cause bugs, test gaps in important paths, accessibility issues, missing error handling |
| **Low**           | Style violations, minor naming issues, dead code, missing nullability annotations                           |
| **Informational** | Observations, suggestions, documentation gaps                                                               |

### Pass Timeout Windows

| Pass   | Timeout (per agent) |
| ------ | ------------------- |
| Pass 1 | 5 minutes           |
| Pass 2 | 8 minutes           |
| Pass 3 | 5 minutes           |

---

## Pass 1 — Discovery

5 parallel agents + 5 parallel validators. Zero mutual dependency.

### Analysis Agents

| Agent    | Domain            | Scope                                                                                                                                                 |
| -------- | ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| **P1-A** | Structure         | Solution topology, project dependency graph, namespace coherence, layer isolation, circular reference detection                                       |
| **P1-B** | Code Quality      | `.editorconfig` compliance, naming conventions, dead code, nullability gaps, access modifiers, async hygiene, exception handling, `var` usage         |
| **P1-C** | Tests             | Test framework setup, coverage configuration, test-to-code ratio, mocking patterns, integration vs. unit test split, missing categories, test naming  |
| **P1-D** | Security & Config | Secrets in source, CSP headers, dependency CVEs, input validation, auth/authz patterns, hardcoded URLs/keys, NuGet package audit                      |
| **P1-E** | CI/CD & Ops       | Pipeline correctness, build warnings, artifact publishing, `dotnet format` enforcement, test execution in CI, branch protection rules, Docker content |

### Validator Agents

| Validator | Validates | Checks                                                                                                                          |
| --------- | --------- | ------------------------------------------------------------------------------------------------------------------------------- |
| **V1-A**  | P1-A      | JSON schema conformance, every `files` entry resolves to an actual file + line, severity internal consistency, no duplicate IDs |
| **V1-B**  | P1-B      | Same as above                                                                                                                   |
| **V1-C**  | P1-C      | Same as above                                                                                                                   |
| **V1-D**  | P1-D      | Same as above                                                                                                                   |
| **V1-E**  | P1-E      | Same as above                                                                                                                   |

---

## Pass 2 — Deep Analysis

5 parallel agents + 5 parallel validators. Consumes Pass 1 output. Does not
execute if source domain was blocked.

### Analysis Agents

| Agent    | Domain                | Input                         | Task                                                                                                                               |
| -------- | --------------------- | ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| **P2-A** | Architecture          | P1-A + P1-B                   | Layer discipline, coupling, cohesion, SOLID adherence, DI registration audit, service lifetimes, `Program.cs` composition root     |
| **P2-B** | Algorithm Correctness | P1-B + Core project           | Topological sort (Kahn's), union-find, percentile ranking, linear spacing — edge cases, overflow, precision loss, graph invariants |
| **P2-C** | Blazor WASM           | P1-A + Web project            | Component lifecycle, state management, `Blazored.LocalStorage` usage, rendering performance, JS interop, accessibility             |
| **P2-D** | EF Core & Persistence | P1-B + Infrastructure project | SQLite schema, migration health, query patterns, N+1 detection, transaction usage, connection lifecycle                            |
| **P2-E** | Test Effectiveness    | P1-C + full test source       | Mutation coverage potential, assertion strength, test isolation, flaky test risk, untested critical paths                          |

### Validator Agents

| Validator | Validates | Checks                                                                                                                                |
| --------- | --------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| **V2-A**  | P2-A      | JSON schema conformance, file/line resolution, severity consistency, no duplicate IDs, cross-check against P1 findings for same files |
| **V2-B**  | P2-B      | Same as above                                                                                                                         |
| **V2-C**  | P2-C      | Same as above                                                                                                                         |
| **V2-D**  | P2-D      | Same as above                                                                                                                         |
| **V2-E**  | P2-E      | Same as above                                                                                                                         |

---

## Pass 3 — Synthesis & Verification

3 synthesis agents + 3 verifier agents. Consumes Pass 1 + Pass 2 output.

### Synthesis Agents

| Agent    | Task                                                                                                                                                                              |
| -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **P3-A** | Merge all P1 and P2 reports. Deduplicate. Cross-reference findings spanning multiple domains. Produce `findings-master.{md,json}`.                                                |
| **P3-B** | Sort all findings by risk/impact. Assign fix complexity (trivial → rewrite). Group into Critical / High / Medium / Low / Informational. Produce `findings-prioritized.{md,json}`. |
| **P3-C** | For each Critical and High finding, propose a concrete fix: files touched, effort estimate, verification strategy. Produce `remediation-plan.{md,json}`.                          |

### Verifier Agents

| Verifier          | Verifies                                                                                                                 |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------ |
| **V3-SpotCheck**  | Spot-checks 20% of all P1 + P2 findings against source code for hallucination/accuracy                                   |
| **V3-CrossCheck** | Confirms no contradictions between P2 and P1 findings for the same files; flags contested findings                       |
| **V3-Integrity**  | Validates the aggregator (P3-A) didn't drop or distort findings during dedup; confirms no ID collisions in master report |

---

## Pass 4 — Delivery

| Step | Action                                                         |
| ---- | -------------------------------------------------------------- |
| 4.1  | Present aggregated reports for review                          |
| 4.2  | Review and resolve all `status: contested` findings            |
| 4.3  | Review all `status: dead` and `status: escalated` findings     |
| 4.4  | Approve/refine the prioritized fix list                        |
| 4.5  | Commit `reports/audit/` to `fix/audit-codebase`                |
| 4.6  | Optionally create individual fix branches per critical finding |

---

## Agent Feedback & Retry Protocol

### Per-Finding Rejection

```
Source Agent
     │
     ▼
Validator Agent  →  validates each finding
     │
     ├─ PASS: finding enters the pipeline
     │
     └─ FAIL: rejected finding returned to source agent with:
           {
             "finding_id": "CQ-001",
             "reject_reason": "file_not_found|line_out_of_range|schema_violation|duplicate_id",
             "detail": "explanation...",
             "suggested_action": "..."
           }
              │
              ▼
         Source Agent retries (max 3 attempts per batch)
              │
              ├─ Fixes the finding → resubmits
              ├─ Removes the finding if hallucinated
              └─ 3 retries exhausted → finding marked `status: dead`, escalated to human
```

### Agent-Level Failure Modes

| Failure                                      | Detection                     | Action                                                                                         |
| -------------------------------------------- | ----------------------------- | ---------------------------------------------------------------------------------------------- |
| **Schema non-conformance**                   | JSON output invalid           | Full rejection. Retry from scratch (max 2 full retries).                                       |
| **Output truncated / incomplete**            | Missing required sections     | Same as above.                                                                                 |
| **Timeout / stall**                          | No output within pass timeout | Kill agent. Re-dispatch with sibling-agent context summary. Max 2 re-dispatches.               |
| **Verifier flags >50% findings invalid**     | Metrics check post-validation | Kill the domain. Findings marked `status: coverage_gap`. Escalate to human. Do not auto-retry. |
| **Cross-verifier detects >3 contradictions** | V3-CrossCheck detection       | All findings from that agent annotated `low_confidence`. Human resolves.                       |

### Escalation Contract

| Scenario                           | Orchestrator Action                                                                                                              |
| ---------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| All retries exhausted on a finding | Emit `finding_escalated.md` with full retry history. Present as decision item in Pass 4.                                         |
| Agent fails 2 full retries         | Kill the domain. Emit `domain_blocked.md`. Downstream agents receive `source_unavailable` marker and proceed without that input. |
| Verifier finds >50% invalid        | Kill both agent and domain. Flagged as `coverage_gap` in master report.                                                          |
| Timeout on 3+ agents in a pass     | Stall the pass. Present human with options: increase timeout, reduce scope, or continue with partial results.                    |

### Conflict Resolution

If V3-CrossCheck detects contradictory findings (e.g., Agent A says "no
nullability issues" but Agent B says "missing null check at line 47"):

- Both findings flagged in master report as `status: contested`
- Human resolves during Pass 4
- Contested findings are never auto-merged or dropped silently

---

## Orchestrator Role Boundaries

| Allowed                                                  | Forbidden                                            |
| -------------------------------------------------------- | ---------------------------------------------------- |
| Enumerate technology stack and verify docs (Pass 0.0)    | Read source code or form opinions about code quality |
| Inject tech constraints into agent prompts               | Edit any report output directly                      |
| Define schema, scope, domain boundaries                  | Override agent severity classifications              |
| Dispatch all agents in parallel batches                  | "Sanity check" findings — verifier agents do this    |
| Run validation gates between passes                      | Merge or rewrite findings manually                   |
| Present aggregated results                               | Propose architecture-specific remediations           |
| Handle retry/re-dispatch on validation failure           |                                                      |
| Run retroactive doc verification if Pass 0.0 was skipped |                                                      |

---

## Concurrency Summary

```
Pass 0.0: Technology stack enumeration + doc queries (sequential, 1 orchestrator step)
Pass 0.1–0.5: Preflight (sequential)
          ─── tech constraints injected into agent prompts ───
Pass 1:  5 agents + 5 validators  ████████████████  (parallel, zero dependency)
         ─── validation gate ───
Pass 2:  5 agents + 5 validators  ████████████████  (parallel, reads P1 output)
         ─── validation gate ───
Pass 3:  3 synthesis + 3 verifiers  ████████████      (parallel, reads P1+P2 output)
         ─── validation gate ───
Pass 3.R: 1 retroactive doc verification agent         (if Pass 0.0 was skipped)
Pass 4:  human review only
```

**Total: 27 agents (26 analysis + 1 retroactive doc verifier) across 3 parallel
passes + preflight/synthesis.**

---

## Traceability Guarantee

- Every finding carries `files: ["path:line"]` — verifier confirms line exists
- Master report cross-references findings by file for full traceability
- Finding IDs are globally unique (domain prefix + sequential) and stable across
  passes
- Deleted/hallucinated findings leave a tombstone entry in the master report for
  audit trail
