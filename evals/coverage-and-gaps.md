# Eval coverage and gaps

A stakeholder-readable inventory of Azure SDK Agent eval coverage: what is
tested today, where the gaps are, and who is tracking each one. This is the
deliverable for [#16403](https://github.com/Azure/azure-sdk-tools/issues/16403)
(part of the eval-framework epic, [#16344](https://github.com/Azure/azure-sdk-tools/issues/16344)).

For the operational how-to (running evals locally, adding a new scenario,
suite/tag reference), see [`README.md`](README.md). This document is the
coverage map; the README is the user guide.

> Snapshot date: 2026-07-24, taken directly from `main` plus the open PRs
> noted inline. Tables here will drift as skills/tools/scenarios are added —
> see [Keeping this current](#keeping-this-current) below.

## 1. Eval types and tiers

| Type | Question it answers | Determinism | Where it lives | Runs today |
|---|---|---|---|---|
| Skill trigger/routing eval | Does the right skill load for a prompt? | Deterministic (`skill-invocation` grader) | `.github/skills/<skill>/evals/trigger.eval.yaml` (or `evals/eval.yaml` when trigger+capability are combined in one file) | `vally lint` (schema only) in [`.github/workflows/skill-eval.yml`](../.github/workflows/skill-eval.yml) — real execution not wired yet |
| Skill capability eval | Does the skill follow its instructions correctly end-to-end? | Model-graded — four-layer pattern (`skill-invocation` + `tool-calls` + `output-matches` + `prompt` LLM-judge), per [#15871](https://github.com/Azure/azure-sdk-tools/issues/15871) | `.github/skills/<skill>/evals/eval.yaml`; `azure-typespec-author` uses `evaluate/evals/*.eval.yaml` instead (38 cases) | Same as above |
| MCP tool-selection eval | Given a prompt, does the agent call the right tool(s)? | Deterministic (`tool-calls` grader) | [`evals/tools/*.eval.yaml`](tools/) (10 files, hermetic) | `unit` suite — PR-gate candidate in [`eng/common/pipelines/workflow-eval.yml`](../eng/common/pipelines/workflow-eval.yml) |
| Workflow scenario eval — mock | Multi-tool orchestration against a mocked MCP server | Mixed (tool-calls + output/file graders) | [`evals/workflows/mock/*.eval.yaml`](workflows/mock/) (7 files) | `scenarios-mock` suite — hermetic, PR-gate candidate |
| Workflow scenario eval — live | Same, against real DevOps/GitHub services | Mixed | [`evals/workflows/live/*.eval.yaml`](workflows/live/) (1 file today) | `scenarios-live` suite — nightly only |
| Multi-turn conversation eval | Cross-turn skill hand-off, clarification, deferred-action flows | Mixed | `turns:`-based stimuli inside [`multi-turn-release-workflows.eval.yaml`](workflows/mock/multi-turn-release-workflows.eval.yaml) today | Release-plan flows land today; pipeline diagnose→fix is pending in open PR [#16418](https://github.com/Azure/azure-sdk-tools/pull/16418) |
| ARM API reviewer suite | ARM OpenAPI-spec review quality (specs-repo only) | Model-graded | `azure-rest-api-specs` `.github/skills/evals/arm-api-reviewer/` (own `.vally.yaml`, own `run-evals.ps1` runner) | Own pipeline; not on the shared `vallyRoot`/`evalGlobs` model used elsewhere |
| Vally lint | Eval YAML is well-formed / schema-valid (not a behavioral check) | Deterministic | applies to every file above | Runs in every repo that has synced `.github/workflows/skill-eval.yml` |

## 2. Skills and scenario coverage

13 skill directories exist under `.github/skills/` today (`distribution:
shared` = cross-repo synced; `distribution: local` = azure-sdk-tools only;
some skills have no `distribution` field set at all, marked "unspecified"
below).

| Skill | Distribution | Trigger/routing | Capability | Notes |
|---|---|---|---|---|
| `azsdk-common-apiview-feedback-resolution` | shared | ✅ `evals/trigger.eval.yaml` | — | |
| `azsdk-common-generate-sdk-locally` | shared | ✅ `evals/eval.yaml` | ✅ (same file) | Newer unified naming — see [#15871](https://github.com/Azure/azure-sdk-tools/issues/15871) direction |
| `azsdk-common-generate-sdk-pipeline` | shared | ❌ | ❌ | **Gap** — no `evals/` folder at all despite `shared` distribution |
| `azsdk-common-pipeline-analysis` | shared | ✅ `evals/trigger.eval.yaml` | — | |
| `azsdk-common-pipeline-fixer` | shared | ✅ `evals/trigger.eval.yaml` | — | |
| `azsdk-common-prepare-release-plan` | shared | ✅ `evals/trigger.eval.yaml` | — | |
| `azsdk-common-sdk-release` | shared | ✅ `evals/trigger.eval.yaml` | — | |
| `azure-typespec-author` | unspecified | — | ✅ 38 cases under `evaluate/evals/` | No dedicated trigger file — flagged in [#15871](https://github.com/Azure/azure-sdk-tools/issues/15871) ("ships capability tests without a trigger file"); frontmatter has no `distribution:` field |
| `markdown-token-optimizer` | unspecified | ✅ `evals/trigger.eval.yaml` | — | No MCP tool invocations — `tool-calls` grader doesn't apply here; frontmatter has no `distribution:` field |
| `sensei` | unspecified | ✅ `evals/trigger.eval.yaml` | — | Flagged in [#15871](https://github.com/Azure/azure-sdk-tools/issues/15871): missing `scoring.threshold`, passes vacuously today; frontmatter has no `distribution:` field |
| `skill-authoring` | unspecified | ✅ `evals/trigger.eval.yaml` | — | frontmatter has no `distribution:` field |
| `sdk-ai-bot-eval-dataset` | local | ❌ | ❌ | **Gap** — lower priority; local-only QA-bot tooling, a different system from the MCP agent framework this doc otherwise covers |
| `sdk-ai-bot-run-evaluation` | local | ❌ | ❌ | **Gap** — same as above |
| `skill-eval-authoring` | shared (pending) | pending | pending | Open, duplicate PRs [#16412](https://github.com/Azure/azure-sdk-tools/pull/16412) / [#16476](https://github.com/Azure/azure-sdk-tools/pull/16476) — a skill to help authors write correct eval coverage; only one of the two PRs is expected to land |
| `tool-eval-authoring` | shared (pending) | pending | pending | Same PRs |
| `workflow-eval-authoring` | shared (pending) | pending | pending | Same PRs |

Note: an earlier audit in [#15871](https://github.com/Azure/azure-sdk-tools/issues/15871)
listed `azsdk-common-api-review` as having no eval file — that skill name no
longer exists in `.github/skills/`. Whoever owns that sub-issue should
confirm whether it was renamed/superseded (e.g. by
`azsdk-common-apiview-feedback-resolution`, which does have a trigger eval
today) or the audit line is simply stale.

## 3. MCP tool and mock-handler coverage

106 `[McpServerTool]`-attributed tools across 11 namespaces in
`tools/azsdk-cli/Azure.Sdk.Tools.Cli/Tools/`. Mock handlers in
[`Azure.Sdk.Tools.Mock/Handlers/`](../tools/azsdk-cli/Azure.Sdk.Tools.Mock/Handlers/)
mirror the same namespaces 1:1, except `Example` (sample/template tools,
intentionally excluded — see `ExemptTools` below).

| Namespace | Tools | Tool-scenario eval | Mock handler |
|---|---|---|---|
| APIView | 5 | [`prompt-to-tool-apiview.eval.yaml`](tools/prompt-to-tool-apiview.eval.yaml) | ✅ |
| Config | 13 | [`prompt-to-tool-config.eval.yaml`](tools/prompt-to-tool-config.eval.yaml) | ✅ |
| Core | 3 | — | ✅ |
| EngSys | 6 | [`prompt-to-tool-engsys.eval.yaml`](tools/prompt-to-tool-engsys.eval.yaml) | ✅ |
| Example | 12 | — | ❌ (intentionally — sample/template tools) |
| GitHub | 5 | [`prompt-to-tool-github.eval.yaml`](tools/prompt-to-tool-github.eval.yaml) | ✅ |
| Package | 19 | [`prompt-to-tool-package.eval.yaml`](tools/prompt-to-tool-package.eval.yaml) | ✅ |
| Pipeline | 8 | [`prompt-to-tool-pipeline.eval.yaml`](tools/prompt-to-tool-pipeline.eval.yaml) | ✅ |
| ReleasePlan | 18 | [`prompt-to-tool-releaseplan.eval.yaml`](tools/prompt-to-tool-releaseplan.eval.yaml) | ✅ |
| TypeSpec | 15 | [`prompt-to-tool-typespec.eval.yaml`](tools/prompt-to-tool-typespec.eval.yaml), [`add-arm-resource.eval.yaml`](tools/add-arm-resource.eval.yaml) | ✅ |
| Verify | 2 | [`prompt-to-tool-verify.eval.yaml`](tools/prompt-to-tool-verify.eval.yaml) | ✅ |

`Azure.Sdk.Tools.Cli.Tests`' `ToolPromptCoverageTests.AllToolsHaveTestPrompts`
enforces (deterministically, in PR CI) that every non-exempt tool has at
least one legacy test prompt; a handful of `Example`/debug-only tools and a
few `EngSys` codeowner tools are in its `ExemptTools` allowlist. A pending
draft PR ([#16461](https://github.com/Azure/azure-sdk-tools/pull/16461))
would repoint that same test at the `evals/tools/*.eval.yaml` corpus instead
of the legacy `TestPrompts.json`, closing the gap between "has a test
prompt" and "has a real Vally eval."

## 4. Cross-repository coverage

| Repo | `eng/common` eval templates synced | Repo-local pipeline entrypoint | Eval files today | Status |
|---|---|---|---|---|
| `azure-sdk-tools` (source) | n/a — native | `eng/common/pipelines/{skill-eval,workflow-eval,live-eval}.yml` used directly | 65 (10 tool-scenario + 7 mock-workflow + 1 live-workflow + 9 per-skill trigger + 38 `azure-typespec-author` capability) | Fully wired: `vally lint` on every skill PR, `unit`/`scenarios-mock` tiers via `workflow-eval.yml`, `scenarios-live` nightly |
| `azure-rest-api-specs` | ✅ | `eng/pipelines/skill-eval.yml` (extends `archetype-eval.yml`) | 25 (8 per-skill trigger + 17 `arm-api-reviewer`); **0 workflow-scenario evals** | **Piloted and live** — [#16347](https://github.com/Azure/azure-sdk-tools/issues/16347) closed all acceptance criteria; scheduled runs confirmed working via [Azure/azure-rest-api-specs#44696](https://github.com/Azure/azure-rest-api-specs/pull/44696). Fast-follow gaps (explicitly non-blocking): eval-coverage backfill for the `azure-api-review`, `openai-typespec-update`, `generate-sdk-locally`, `pipeline-analysis`, `pipeline-fixer` skills in that repo, and an `arm-api-reviewer` suite model-availability issue (`claude-opus-4.6-1m`) |
| `azure-sdk-for-python` | ✅ (confirmed directly — `eng/common/pipelines/skill-eval.yml` present) | ❌ none | 0 | Not started |
| `azure-sdk-for-net` / `-java` / `-js` / `-go` | ✅ (same sync mechanism; not individually re-verified) | ❌ none | 0 | Not started |

Language-repo rollout is tracked as a whole in
[#16348](https://github.com/Azure/azure-sdk-tools/issues/16348) — every
acceptance criterion there is still unchecked. The plan is sync-PR rollout
(reusing the same `eng/common` mirror already used for `azure-rest-api-specs`),
one pilot language repo first, scheduled/report-only before any PR-gating.

## 5. Known gaps and follow-up issues

| Gap | Tracking | Notes |
|---|---|---|
| 3 skills with zero eval coverage | [#16345](https://github.com/Azure/azure-sdk-tools/issues/16345) | `azsdk-common-generate-sdk-pipeline` (shared, higher priority), `sdk-ai-bot-eval-dataset` + `sdk-ai-bot-run-evaluation` (local-only) |
| Vacuous/low-quality capability grading (single `output-contains`, missing `scoring.threshold`) | [#15871](https://github.com/Azure/azure-sdk-tools/issues/15871) | `sensei` named explicitly; four-layer pattern is the target shape for every capability stimulus |
| Per-skill eval CI runs `vally lint` only — no real behavioral execution in CI yet | [#15126](https://github.com/Azure/azure-sdk-tools/issues/15126), [#15127](https://github.com/Azure/azure-sdk-tools/issues/15127) | cited as follow-ups in `evals/README.md` |
| `tool-calls` grader can't assert argument values, forbidden tools, or call order (only tool *names*) | tracked inline in `evals/README.md` → "Known gaps vs. the original benchmark" | needs upstream `@microsoft/vally-cli` support or a custom `.NET` grader under `evals/Graders/` |
| Multi-turn conversation coverage is thin — only release-plan flows are in `main`; pipeline diagnose→fix is pending; TypeSpec authoring, APIView feedback, SDK release readiness, and cross-skill handoff are still open | this doc + PR [#16418](https://github.com/Azure/azure-sdk-tools/pull/16418) (open) | |
| Workflow/multi-tool scenario evals (`evals/workflows/mock`, `evals/workflows/live`) exist only in `azure-sdk-tools` — zero in `azure-rest-api-specs` or any language repo | [#16347](https://github.com/Azure/azure-sdk-tools/issues/16347) | The specs-repo pilot only proved out the tool-selection tier; multi-tool workflow scenarios were never extended cross-repo |
