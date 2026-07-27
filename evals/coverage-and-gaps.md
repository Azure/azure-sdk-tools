# Eval coverage and gaps

A stakeholder-readable inventory of Azure SDK Agent eval coverage: what is
tested today, where the gaps are, and the issue or PR that tracks each one.
This is the deliverable for [#16403](https://github.com/Azure/azure-sdk-tools/issues/16403)
(part of the eval-framework epic, [#16344](https://github.com/Azure/azure-sdk-tools/issues/16344)).

For the operational how-to (running evals locally, adding a new scenario, and
suite/tag reference), see [`README.md`](README.md). This document is the
coverage map; the README is the user guide.

> Snapshot date: 2026-07-27. Counts were verified from current source and the
> downstream repositories noted inline. This is an inventory, not a coverage
> gate; update it as described in [Keeping this current](#6-keeping-this-current).

## 1. Eval types and tiers

The agent execution in every behavioral tier is model-driven. “Deterministic”
below means the grader evaluates a recorded trajectory or static input without
asking another model to judge it.

| Type | Question it answers | Grading | Where it lives | Runs today |
| --- | --- | --- | --- | --- |
| Skill trigger/routing eval | Does the right skill load for a prompt? | Deterministic `skill-invocation` trajectory grade | `.github/skills/<skill>/evals/trigger.eval.yaml` (or `evals/eval.yaml` when combined) | PR: [`skill-eval.yml`](../.github/workflows/skill-eval.yml) runs `vally lint`. Post-main: [`eng/pipelines/skill-eval.yml`](../eng/pipelines/skill-eval.yml) runs behavioral evals report-only; no PR behavioral gate. |
| Skill behavioral/capability eval | Does the skill follow its instructions end-to-end? | Deterministic trajectory/output graders; optional LLM judge. The four-layer pattern is the target, not the current minimum. | `.github/skills/<skill>/evals/eval.yaml`; `azure-typespec-author` uses `evaluate/evals/*.eval.yaml` (38 cases). | Same as above. |
| MCP tool-selection eval | Given a prompt, does the agent call the right tool(s)? | Deterministic `tool-calls` trajectory grade | [`evals/tools/*.eval.yaml`](tools/) (10 hermetic files) | `unit` is main-triggered/report-only in [`workflow-eval.yml`](../eng/common/pipelines/workflow-eval.yml), not a PR gate. |
| Workflow scenario eval — mock | Does a multi-tool flow reach the expected result against mock MCP responses? | Deterministic tool/output/file graders, with optional model judge | [`evals/workflows/mock/*.eval.yaml`](workflows/mock/) (7 files) | `scenarios-mock` is main-triggered/report-only, not a PR gate. |
| Workflow scenario eval — live | Does a flow work against real DevOps/GitHub services? | Mixed | [`evals/workflows/live/*.eval.yaml`](workflows/live/) (1 file) | `scenarios-live` runs nightly and gates on its threshold in [`live-eval.yml`](../eng/common/pipelines/live-eval.yml). |
| Multi-turn conversation eval | Can a later turn reuse prior context and hand off correctly? | Mixed | `turns:` stimuli in [`multi-turn-release-workflows.eval.yaml`](workflows/mock/multi-turn-release-workflows.eval.yaml) | Release-plan flows are present; pipeline diagnose→fix is pending in [#16418](https://github.com/Azure/azure-sdk-tools/pull/16418). |
| ARM API reviewer suite | Does an ARM OpenAPI spec meet review expectations? | Model-graded | `azure-rest-api-specs` `.github/skills/evals/arm-api-reviewer/` with its own configuration and runner | Own specs-repo pipeline; not on the root `evals/` suite model. |
| Vally lint | Are skill definitions and their eval configuration statically valid? | Deterministic static check | Skill directories | Runs in [`.github/workflows/skill-eval.yml`](../.github/workflows/skill-eval.yml). It is not behavioral execution and does not validate root `evals/` scenario coverage. |

Every current root workflow scenario in `azure-sdk-tools`:

| File | Area | Tier | What it covers |
| --- | --- | --- | --- |
| [`analyze-failed-pipeline.eval.yaml`](workflows/mock/analyze-failed-pipeline.eval.yaml) | pipeline | mock | Pull pipeline status, then analyze the run to surface the failing test. |
| [`check-public-repo-then-validate.eval.yaml`](workflows/mock/check-public-repo-then-validate.eval.yaml) | typespec | mock | Validate, then check public-repo presence. |
| [`fix-pipeline.eval.yaml`](workflows/mock/fix-pipeline.eval.yaml) | pipeline | mock | Apply a fix to overlaid source and verify with package build/check/test tools. |
| [`multi-turn-release-workflows.eval.yaml`](workflows/mock/multi-turn-release-workflows.eval.yaml) | release-plan | mock | Multi-turn (`turns:`) release-plan conversation coverage. |
| [`release-planner-workflows.eval.yaml`](workflows/mock/release-planner-workflows.eval.yaml) | release-plan | mock | Create, re-fetch, link, and update release-plan flows. |
| [`rename-client-property.eval.yaml`](workflows/mock/rename-client-property.eval.yaml) | typespec | mock | Stub; still needs an `expected-diff` grader and sparse clone. |
| [`typespec-generation-step02.eval.yaml`](workflows/mock/typespec-generation-step02.eval.yaml) | typespec | mock | Step in the spec-PR generation flow. |
| [`release-planner.eval.yaml`](workflows/live/release-planner.eval.yaml) | release-plan | **live** | Create and re-fetch a plan, kick off SDK generation, and link its PR using real DevOps test-area writes. |

## 2. Skills and scenario coverage

Thirteen skill directories exist under `.github/skills/` today. In this table,
**routing** means at least one file contains a `skill-invocation` grader;
**behavioral** means the file has a tool-call or outcome contract. File
existence alone does not establish strong four-layer coverage.

`distribution: shared` means cross-repository sync; `local` means Azure SDK
Tools only; `unspecified` means the frontmatter has no `distribution` field.

| Skill | Distribution | Routing coverage | Behavioral/capability coverage | Notes |
| --- | --- | --- | --- | --- |
| `azsdk-common-apiview-feedback-resolution` | shared | ✅ `evals/trigger.eval.yaml` | ✅ `evals/eval.yaml` (9 stimuli, tool calls) | Behavioral file has legacy mixed graders. |
| `azsdk-common-generate-sdk-locally` | shared | — | ✅ `evals/eval.yaml` (33 stimuli, tool calls) | No `skill-invocation` routing grader yet. |
| `azsdk-common-generate-sdk-pipeline` | shared | ❌ | ❌ | **Gap** — no `evals/` folder despite shared distribution. |
| `azsdk-common-pipeline-analysis` | shared | ✅ `evals/trigger.eval.yaml` | — | |
| `azsdk-common-pipeline-fixer` | shared | ✅ `evals/trigger.eval.yaml` | — | |
| `azsdk-common-prepare-release-plan` | shared | ✅ `evals/trigger.eval.yaml` | ✅ `evals/eval.yaml` (9 stimuli, tool calls) | |
| `azsdk-common-sdk-release` | shared | ✅ `evals/trigger.eval.yaml` | ✅ `evals/eval.yaml` (9 stimuli, tool calls) | |
| `azure-typespec-author` | unspecified | Partial — 9 data-plane cases include `skill-invocation` | ✅ 38 cases in `evaluate/evals/` | No dedicated trigger suite; frontmatter has no `distribution` field. |
| `markdown-token-optimizer` | unspecified | ✅ `evals/trigger.eval.yaml` | — | `evals/eval.yaml` contains only two trigger-shaped outcome checks; no MCP tool contract applies. |
| `sensei` | unspecified | ✅ `evals/trigger.eval.yaml` | — | `evals/eval.yaml` exists but is two trigger-shaped checks and lacks `scoring.threshold`. |
| `skill-authoring` | unspecified | ✅ `evals/trigger.eval.yaml` | ⚠️ `evals/eval.yaml` (3 output-only stimuli) | Needs stronger behavioral contracts. |
| `sdk-ai-bot-eval-dataset` | local | ❌ | ❌ | **Gap** — local QA-bot tooling, outside the MCP-agent focus of most rows here. |
| `sdk-ai-bot-run-evaluation` | local | ❌ | ❌ | **Gap** — same as above. |

Not counted above: [#16412](https://github.com/Azure/azure-sdk-tools/pull/16412)
is closed. Open [#16476](https://github.com/Azure/azure-sdk-tools/pull/16476)
proposes three repository-local meta-skills — `eval-authoring-skill`,
`eval-authoring-tool`, and `eval-authoring-workflow` — which are pending and
not yet part of this inventory.

## 3. MCP tool and mock-handler coverage

Current source contains 74 `[McpServerTool]` method attributes across 11
namespaces under `tools/azsdk-cli/Azure.Sdk.Tools.Cli/Tools/`. The mock server
reflects every such tool in
[`MockToolRegistrations.cs`](../tools/azsdk-cli/Azure.Sdk.Tools.Mock/MockToolRegistrations.cs).
Custom handlers are discovered separately by
[`MockToolFactory.cs`](../tools/azsdk-cli/Azure.Sdk.Tools.Mock/Handlers/MockToolFactory.cs),
and its default response covers a tool with no custom handler. Therefore,
handler folder/counts are not a one-to-one proof of production-namespace
coverage.

| Production source namespace | Tools | Tool-selection eval | Custom mock handlers |
| --- | ---: | --- | ---: |
| APIView | 4 | [`prompt-to-tool-apiview.eval.yaml`](tools/prompt-to-tool-apiview.eval.yaml) | 4 |
| Config | 11 | [`prompt-to-tool-config.eval.yaml`](tools/prompt-to-tool-config.eval.yaml) | 11 |
| Core | 1 | — | 1 |
| EngSys | 4 | [`prompt-to-tool-engsys.eval.yaml`](tools/prompt-to-tool-engsys.eval.yaml) | 5 |
| Example | 10 | — | 10 |
| GitHub | 4 | [`prompt-to-tool-github.eval.yaml`](tools/prompt-to-tool-github.eval.yaml) | 4 |
| Package | 12 | [`prompt-to-tool-package.eval.yaml`](tools/prompt-to-tool-package.eval.yaml) | 11 |
| Pipeline | 4 | [`prompt-to-tool-pipeline.eval.yaml`](tools/prompt-to-tool-pipeline.eval.yaml) | 3 |
| ReleasePlan | 15 | [`prompt-to-tool-releaseplan.eval.yaml`](tools/prompt-to-tool-releaseplan.eval.yaml) | 16 |
| TypeSpec | 8 | [`prompt-to-tool-typespec.eval.yaml`](tools/prompt-to-tool-typespec.eval.yaml), [`add-arm-resource.eval.yaml`](tools/add-arm-resource.eval.yaml) | 8 |
| Verify | 1 | [`prompt-to-tool-verify.eval.yaml`](tools/prompt-to-tool-verify.eval.yaml) | 1 |

`ToolPromptCoverageTests.AllToolsHaveTestPrompts` deterministically requires
each non-exempt tool to have at least one legacy test prompt in PR CI. Draft
[#16461](https://github.com/Azure/azure-sdk-tools/pull/16461) would instead
validate the `evals/tools/*.eval.yaml` corpus, closing the gap between “has a
prompt” and “has a Vally tool-selection eval.”

## 4. Cross-repository coverage

| Repo | Eval wiring | Eval files today | Status |
| --- | --- | --- | --- |
| `azure-sdk-tools` (source) | PR lint: [`.github/workflows/skill-eval.yml`](../.github/workflows/skill-eval.yml). Behavioral runs: post-main/report-only skill and mock workflow pipelines; live tier nightly. | 71 YAML files: 53 skill files (15 across standard skill directories + 38 TypeSpec-author cases), 10 tool-selection, 7 mock workflow, 1 live workflow. | Native implementation. Behavioral coverage is not PR-gated. |
| `azure-rest-api-specs` | `eng/pipelines/skill-eval.yml` and `.github/skills/.vally.yaml` are present. | 32 YAML files: 15 skill eval files + 17 ARM API reviewer evals; no root `evals/` workflow-scenario layout. | Pilot is live; [#16347](https://github.com/Azure/azure-sdk-tools/issues/16347) is closed. |
| `azure-sdk-for-python` | Shared `eng/common` template present; no repo-local entrypoint or `.github/skills/.vally.yaml`. | 9 skill eval YAML files. | Language-repo rollout not started. |
| `azure-sdk-for-net` | Shared `eng/common` template present; no repo-local entrypoint or `.github/skills/.vally.yaml`. | 11 skill eval YAML files. | Language-repo rollout not started. |
| `azure-sdk-for-java` | Shared `eng/common` template present; no repo-local entrypoint or `.github/skills/.vally.yaml`. | 9 skill eval YAML files. | Language-repo rollout not started. |
| `azure-sdk-for-js` | Shared `eng/common` template present; no repo-local entrypoint or `.github/skills/.vally.yaml`. | 9 skill eval YAML files. | Language-repo rollout not started. |
| `azure-sdk-for-go` | Shared `eng/common` template present; no repo-local entrypoint or `.github/skills/.vally.yaml`. | 0 skill eval YAML files. | Language-repo rollout not started. |

Language-repo rollout is tracked in
[#16348](https://github.com/Azure/azure-sdk-tools/issues/16348). The shared
template and individual eval files alone do not make a language-repository
suite executable; each repository still needs its own configuration and
pipeline entrypoint.

## 5. Known gaps and status

| Gap | Status / tracking | Notes |
| --- | --- | --- |
| Three skill directories have zero eval coverage. | [#16345](https://github.com/Azure/azure-sdk-tools/issues/16345) — open | `azsdk-common-generate-sdk-pipeline` is shared and higher priority; the two SDK AI bot skills are local-only. |
| Existing skill files vary in behavioral quality. | Historical [#15871](https://github.com/Azure/azure-sdk-tools/issues/15871) — closed | Several legacy files rely only on output matching; `sensei/evals/eval.yaml` also lacks a threshold. The table above identifies the current cases. |
| Behavioral skill and mock-workflow runs are not PR-gated. | [#15126](https://github.com/Azure/azure-sdk-tools/issues/15126) and [#15127](https://github.com/Azure/azure-sdk-tools/issues/15127) — closed implementation history | Runs exist after main, but current post-main/report-only execution does not protect a PR before merge. |
| Tool-selection corpus is not mechanically enforced against every tool. | Draft [#16461](https://github.com/Azure/azure-sdk-tools/pull/16461) | Current static test checks legacy prompt presence, not Vally scenario coverage. |
| Generic tool arguments and tool-call ordering cannot be asserted. | [microsoft/vally#453](https://github.com/microsoft/vally/issues/453), [microsoft/vally#454](https://github.com/microsoft/vally/issues/454) | `tool-calls` already supports required and disallowed tool names; generic arguments and sequence remain gaps. |
| Multi-turn coverage is thin. | [#16418](https://github.com/Azure/azure-sdk-tools/pull/16418) — open | Only release-plan flows are on `main`; pipeline diagnose→fix, TypeSpec authoring, APIView feedback, SDK release readiness, and cross-skill handoff need coverage. |
| Root workflow-scenario evals are only in `azure-sdk-tools`. | [#16348](https://github.com/Azure/azure-sdk-tools/issues/16348) — open | The specs-repo pilot added skills and ARM review coverage, not root `evals/workflows/{mock,live}` scenarios. |

## 6. Keeping this current

The Azure SDK Tools eval owner maintains this inventory. A PR that adds,
removes, or materially changes an eval surface must update the affected table
and snapshot date in the same change:

1. Skill changes: update the file, routing, and behavioral columns in table 2.
2. MCP tool or mock changes: recount `[McpServerTool]` method attributes and custom `IMockToolHandler` implementations for table 3.
3. Scenario or pipeline changes: update table 1, the itemized workflow list, and the source-repo row in table 4.
4. Downstream rollout: recheck the target repository’s pipeline entrypoint, `.vally.yaml`, and eval-file count before changing table 4.
5. New gaps or resolved gaps: update table 5 with the active issue, PR, or a clearly labelled closed historical item.
