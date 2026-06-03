# Vally Tool-Scenario Evaluation — Requirements


---

## 1. Context

PR [#15811](https://github.com/Azure/azure-sdk-tools/pull/15811) ported the
deleted `Azure.Sdk.Tools.Cli.Benchmarks` tool-scenarios into
[`tools/azsdk-cli/Azure.Sdk.Tools.Vally/evals/`](./evals/) as
`@microsoft/vally-cli` evals (11 scenarios, 10 fully-graded + 1 stub). They run
locally but are not yet wired into CI, have no shared environments, and cannot
yet assert *skill choice + tool-call shape + ordering* in a single scenario.


---

## 2. Goals

1. A single eval can express **what skill was picked, what tools were called, in
   what order, with what arguments, and whether the final answer is correct.**
2. Evals are **reproducible**: same SHA, same inputs ⇒ same trajectory.
3. Evals are **safe by default**: nothing destructive runs against live ADO /
   GitHub on a nightly schedule unless the author opted in.
4. Evals are **portable**: a new contributor can clone the repo and run any
   scenario without hand-editing paths.
5. Evals are **observable**: results are exportable (CSV / JUnit / markdown)
   and consumable by non-engineers.

---

## 3. Non-goals (for this round)

- Authoring new eval scenarios beyond the 11 already ported (tracked separately).
- Schema-parity tests between `Azure.Sdk.Tools.Cli` and `Azure.Sdk.Tools.Mock`
  responses — a separate concern, file against the mock project if needed.
- Replacing Vally as the eval runner.
- Building a UI on top of CSV exports.

---

## 4. Functional requirements

### 4.1 Unified scenario file (skill + tool + e2e in one place)

A single `.eval.yaml` must be able to declare:

- one or more `skill-invocation` graders (which `.github/skills/*` were picked),
- one or more `tool-calls` graders (which MCP tools fired, with arg matching),
- an optional `prompt` / `output-contains` / `output-matches` grader for the
  final answer,
- arbitrary tags (`tier`, `scenario`, `skills`, `tools`, `owner`).

Today these flows are split across two pipelines because Vally's per-scenario
grader set is limited. Removing that limitation is the #1 ask from the meeting.

**Upstream dependencies**:
- [microsoft/vally#453](https://github.com/microsoft/vally/issues/453) — `tool-calls` grader: support strict call ordering (`sequence:`).
- [microsoft/vally#454](https://github.com/microsoft/vally/issues/454) — `tool-calls` grader: open `ToolMatch` for generic argument matching.

### 4.2 Three levels of evaluation

Aligned with the 2026-06 design review. Three named levels;
the folder a scenario lives in (and an opt-in tag for level 2) determines
which one runs when.

| Level | Name | Agent | MCP | Trigger | Failure semantics |
|---|---|---|---|---|---|
| 0 | Routing evals (per-skill, prompt-to-skill matching) | live | none | per PR | required |
| 1 | **Workflow scenarios** (mock MCP — default) | live | mock | per PR | required |
| 2 | **Live scenarios** (live MCP — narrow opt-in) | live | live | nightly | advisory → required |

Plus a hermetic tool-shape layer that isn't agent-driven:

| Layer | Name | Agent | MCP | Trigger | Failure semantics |
|---|---|---|---|---|---|
| — | Unit evals (tool-shape + cross-skill triggers) | none | mock | per PR | required |

**Mock is the default; live is the exception.** Both modes drive the
same live agent, so **both incur LLM token cost** — the mock MCP server
itself is a deterministic stub with no LLM in it. The cost delta is
backend latency + the larger / chattier responses live tools produce,
which expand per-turn input and provoke more polling/retry turns.
Level 2 is therefore reserved for scenarios the mock can't deterministically
cover (e.g. TypeSpec ordering, real codegen output, real DevOps state).
Most multi-step work — including release plan and SDK generation —
stays at level 1.

Level 0 lives next to its skill and is out of scope for this project's
folder layout; this project owns the runner config it references.

### 4.3 Mock vs. live MCP — opt-in per eval

Tracked in [#15831](https://github.com/Azure/azure-sdk-tools/issues/15831).

- Both a mock MCP server and the real MCP server must be selectable as the
  scenario's MCP environment.
- **Default is mock.** Running against the live MCP server is per-scenario
  opt-in and must be justified — live MCP carries real token + wall-time
  cost (see [DESIGN.md §6](./DESIGN.md)), so it is reserved for behavior
  the mock can't faithfully reproduce.
- Scenarios touching **production** systems (e.g. shipping packages) must
  remain mock-only and must not be opt-in-able.

### 4.4 Workspace setup hooks (repo cloning for live scenarios)

Tracked in [#15831](https://github.com/Azure/azure-sdk-tools/issues/15831).

- The PR gate (unit + mock scenarios) must be fully hermetic: no clones,
  no outbound network.
- Live-tier scenarios that need external repos (e.g. `azure-rest-api-specs`)
  must declare those dependencies inside the scenario file. Adding a new
  repo dependency must be a YAML-only change.
- Repo provisioning runs once per CI job and is shared across scenarios.
- Pinning a repo to a specific ref is supported but optional in v1.

### 4.5 Configuration via environment variables, not hard-coded paths

- Scenario YAMLs must not hard-code absolute paths.
- Repo locations must be resolved through configuration that works the
  same way locally and in CI.

### 4.6 Skill + tool-call grading must be enforced together

For each prompt, the grader must verify both:

1. The agent picked the **right skill** (`skill-invocation` grader).
2. The agent fired the **right MCP tool calls**, in the right order, with the
   right arguments (`tool-calls` grader with the upstream extensions in §4.1).

A scenario that asserts only the final answer text is incomplete.

### 4.7 End-to-end multi-step scenarios

Vally must be able to grade chains such as:

- *validate TypeSpec project* → *create release plan* → *generate SDK*.

This requires:

- Ordering (vally#453).
- Argument matching (vally#454).
- Tier-appropriate environment (mock for destructive steps, live elsewhere).

Initial e2e targets: `release-planner-e2e`, `create-release-plan`,
`generate-sdk`. Each must include the full tool-call chain in its graders.

### 4.8 Result export

- Native: `results.jsonl`, `eval-results.md`, JUnit XML (already supported).
- **New**: CSV export for the cross-run projection / dashboard use case.
  Either a Vally feature request or a thin post-processor script that
  consumes `results.jsonl`.

### 4.9 Mock MCP tool coverage

- Inventory the tools `Azure.Sdk.Tools.Mock` currently implements.
- For every tool referenced by an eval that runs on `azsdk-mcp-mock`, the mock
  must have a handler (returning realistic shape, not necessarily real data).
- Track gaps in a checklist in the mock project's README.

---

## 5. CI / pipeline requirements

Tracked in [#15829](https://github.com/Azure/azure-sdk-tools/issues/15829).

- A PR-gate job runs unit + mock scenarios on every PR. Hermetic; required
  from day one.
- A nightly job additionally runs the live-tier scenarios against the real
  MCP server. Starts advisory (does not block); flipped to required once
  the baseline is stable.
- A manual trigger is available for ad-hoc runs.
- Results are published as build artifacts (markdown summary + JUnit XML
  at minimum).
- Model and ADO credentials must not leak into logs.

---

## 6. Authoring requirements (so new contributors can extend the suite)

- The authoring pattern (graders, tiers, mock-vs-live decision) is documented
  outside this file and linked from the Vally project's README.
- A new contributor can add a scenario without editing CI configuration or
  shared scripts.
