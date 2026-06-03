# Azure.Sdk.Tools.Vally

MCP-tool / end-to-end scenario evaluations for the `azsdk` MCP server, run via
[`@microsoft/vally-cli`](https://www.npmjs.com/package/@microsoft/vally-cli).

## Tool-scenario evals vs. skill evals

The repo runs **two complementary eval surfaces**, both via the same
`@microsoft/vally-cli` binary. They answer different questions and live in
different folders. A full end-to-end gate runs *both*.

| | **Tool-scenario evals** (this project) | **Skill evals** |
|---|---|---|
| **Question** | Given a user prompt, does the agent invoke the right MCP tool(s) with the right shape? | Given a user prompt, does the agent route to the right skill and follow its instructions? |
| **Catches** | Tool name / description / parameter regressions; multi-tool ordering; tool-catalog conflicts | Skill frontmatter / `description` / instruction regressions; skill-routing collisions |
| **Path** | [`tools/azsdk-cli/Azure.Sdk.Tools.Vally/evals/`](evals/) (`scenarios/` + `triggers/`) | [`.github/skills/<skill-name>/evals/*.eval.yaml`](../../../.github/skills/) (and `evaluate/evals/` for capability suites) |
| **Loaded subject** | Production MCP server (`Azure.Sdk.Tools.Cli`) over stdio — real tools, real network calls | Skill's `SKILL.md` + frontmatter; the agent picks tools itself |
| **Primary grader** | `tool-calls` — checks the recorded trajectory for required tool names | Trigger / routing graders + per-skill rubric |
| **Run command** | `vally eval --eval-spec evals/unit/<name>.eval.yaml` *from this directory* | `vally eval --skill-dir .github/skills/<skill-name>` *from repo root* |
| **CI status** | Not wired yet (see follow-ups) | `vally lint` runs in [.github/workflows/skill-eval.yml](../../../.github/workflows/skill-eval.yml); full `eval` job pending |
| **Cost profile** | Higher — each run spins up the MCP server, real LLM turns (~5–15), real tool calls | Variable — trigger evals are cheap; capability evals (e.g. `azure-typespec-author`) are expensive |

### Why both?

A skill *uses* tools, but a tool can be invoked **without** any skill
(Copilot picks it directly from the catalog when the user prompt doesn't
trigger a skill — which is most prompts in practice). Concretely:

- Drop tool-scenario evals → you stop catching regressions when someone
  renames a tool, edits its description, or adds an overlapping tool that
  the model now prefers.
- Drop skill evals → you stop catching regressions when someone edits a
  skill's `description`, frontmatter, or instruction body and the router
  stops invoking it for the right prompts.

For workflows where a skill is a thin wrapper around one tool, the two
evals have meaningful overlap and you may keep just one. For workflows
where the skill does real orchestration (multi-tool sequencing,
conditional branches, recovery), both matter independently.

### Scenarios checked in today

**Tool-scenario evals (this project)** — organised by the standard test pyramid under [`evals/`](evals/). The folder is the **cost tier** (and CI cadence); the feature **area** is a tag inside each YAML so cross-cuts work via `.vally.yaml` suite filters.

#### `evals/unit/` — hermetic single-tool evals (18)

One prompt → one expected MCP tool. No `environment.git`, no fixtures. Fast; safe to run on every PR. Includes the per-tool **trigger** coverage ported from [#15183](https://github.com/Azure/azure-sdk-tools/pull/15183) (`triggers-*.eval.yaml`).

| Scenario | Area | Shape |
|---|---|---|
| [`check-public-repo`](evals/unit/check-public-repo.eval.yaml) | typespec | Is a TypeSpec project published in `azure-rest-api-specs`? |
| [`validate-typespec`](evals/unit/validate-typespec.eval.yaml) | typespec | Run `tsp` linter/validation |
| [`get-modified-typespec-projects`](evals/unit/get-modified-typespec-projects.eval.yaml) | typespec | Git-aware tool against current branch |
| [`add-arm-resource`](evals/unit/add-arm-resource.eval.yaml) | typespec | Calls `azsdk_typespec_generate_authoring_plan` for an ARM resource |
| [`create-release-plan`](evals/unit/create-release-plan.eval.yaml) | release-plan | Create a release-plan work item |
| [`link-namespace-approval-issue`](evals/unit/link-namespace-approval-issue.eval.yaml) | release-plan | Link an existing approval issue to a release plan |
| [`get-pr-link-current-branch`](evals/unit/get-pr-link-current-branch.eval.yaml) | github | Resolve the PR for the active git branch |
| [`check-sdk-generation-status`](evals/unit/check-sdk-generation-status.eval.yaml) | pipeline | Pipeline status lookup |
| [`triggers-apiview`](evals/unit/triggers-apiview.eval.yaml) | apiview | `azsdk_apiview_*` |
| [`triggers-config`](evals/unit/triggers-config.eval.yaml) | engsys | `azsdk_check_service_label`, `azsdk_create_service_label` |
| [`triggers-engsys`](evals/unit/triggers-engsys.eval.yaml) | engsys | `azsdk_analyze_log_file`, failed-test tools, codeowner-cache |
| [`triggers-github`](evals/unit/triggers-github.eval.yaml) | github | `azsdk_create_pull_request`, `azsdk_get_pull_request*`, `azsdk_get_github_user_details` |
| [`triggers-package`](evals/unit/triggers-package.eval.yaml) | package | `azsdk_package_*`, `azsdk_release_sdk` |
| [`triggers-pipeline`](evals/unit/triggers-pipeline.eval.yaml) | pipeline | `azsdk_analyze_pipeline`, `azsdk_get_pipeline_*` |
| [`triggers-releaseplan`](evals/unit/triggers-releaseplan.eval.yaml) | release-plan | `azsdk_*_release_plan*`, `azsdk_run_generate_sdk`, `azsdk_link_*` |
| [`triggers-typespec`](evals/unit/triggers-typespec.eval.yaml) | typespec | `azsdk_typespec_*`, `azsdk_convert_swagger_to_typespec`, `azsdk_customized_code_update`, `azsdk_run_typespec_validation` |
| [`triggers-verify`](evals/unit/triggers-verify.eval.yaml) | engsys | `azsdk_verify_setup` |

The companion [`scripts/Validate-EvalTools.ps1`](scripts/Validate-EvalTools.ps1) cross-checks that every tool referenced in `evals/unit/triggers-*.eval.yaml` exists on the running MCP server, and every server tool has at least one trigger.

#### `evals/scenarios/` — multi-tool scenarios (4)

Multi-step prompts that exercise 2+ MCP tools end-to-end. Split into
`mock/` (hermetic, runs on PR gate) and `live/` (real DevOps / GitHub /
pipelines, runs nightly).

| Scenario | Area | Mode | Shape |
|---|---|---|---|
| [`check-public-repo-then-validate`](evals/scenarios/mock/check-public-repo-then-validate.eval.yaml) | typespec | mock | Validate, then check public-repo presence |
| [`typespec-generation-step02`](evals/scenarios/mock/typespec-generation-step02.eval.yaml) | typespec | mock | Step in the spec-PR generation flow |
| [`rename-client-property`](evals/scenarios/mock/rename-client-property.eval.yaml) | typespec | mock | Stub — needs `expected-diff` grader + sparse clone |
| [`release-planner`](evals/scenarios/live/release-planner.eval.yaml) | release-plan | **live** | Create + re-fetch a release plan, kick off SDK gen, link PR back — real DevOps test-area writes |

Live scenarios need a primed `azure-rest-api-specs` clone — run
[`evals/setup/ensure-specs-clone.ps1`](evals/setup/ensure-specs-clone.ps1)
(auto-refreshes every 24h) before invoking the `scenarios-live` / `nightly` suite.

**Skill evals (already in repo, *not* part of this PR)** — for reference:

- **Trigger evals** (one per skill, verify routing): see e.g.
  [`.github/skills/azsdk-common-prepare-release-plan/evals/trigger.eval.yaml`](../../../.github/skills/azsdk-common-prepare-release-plan/evals/trigger.eval.yaml),
  plus `azsdk-common-sdk-release`, `azsdk-common-pipeline-troubleshooting`,
  `azsdk-common-apiview-feedback-resolution`, `sensei`,
  `skill-authoring`, `markdown-token-optimizer`.
- **Capability suite** for [`azure-typespec-author`](../../../.github/skills/azure-typespec-author/) —
  29 numbered cases under
  [`.github/skills/azure-typespec-author/evaluate/evals/`](../../../.github/skills/azure-typespec-author/evaluate/evals/)
  (`001001.eval.yaml` … `005001.eval.yaml`). These are the data-driven
  TypeSpec authoring scenarios that *would* have been our follow-up #1
  here — they're already covered as skill evals, so this project doesn't
  re-port them.

This project supersedes the deleted `Azure.Sdk.Tools.Cli.Benchmarks` project
(removed in [#15697](https://github.com/Azure/azure-sdk-tools/pull/15697)) and
tracks the migration in
[#15124](https://github.com/Azure/azure-sdk-tools/issues/15124).

## Layout

```
Azure.Sdk.Tools.Vally/
├── .vally.yaml                # Vally config (environments + suites)
├── evals/
│   ├── unit/                  # tool-shape + per-skill trigger evals, hermetic
│   ├── scenarios/
│   │   ├── mock/              # multi-tool scenarios, hermetic (PR gate)
│   │   └── live/              # multi-tool scenarios, live MCP (nightly)
│   ├── setup/                 # helper scripts (e.g. ensure-specs-clone.ps1)
│   └── fixtures/              # (future) pinned SHAs + per-eval mocks
├── fixtures/                  # Per-scenario static input files (env.files)
│   └── <scenario-name>/...
├── scripts/                   # Repo-side helpers (Validate-EvalTools.ps1, …)
└── Graders/                   # (future) Custom .NET graders
    └── Azure.Sdk.Tools.Vally.csproj  # added when first custom grader lands
```

Folder = tier (cost / CI cadence): `unit/` is hermetic + fast,
`scenarios/mock/` is multi-tool hermetic, `scenarios/live/` is multi-tool
against real services. Vally's suite filter is positive-match only, so the
mock-vs-live split lives on disk rather than in tags. Feature **area** still
lives as a `tags:` entry inside each YAML so cross-cuts (e.g. "all
release-plan evals") select via [`.vally.yaml`](.vally.yaml) suite filters
or `vally eval --tag`.

## Running locally

Prereqs:

- Node 22+
- .NET SDK matching the rest of the repo (see `global.json`)
- `@microsoft/vally-cli` installed via the repo's pinned lockfile:

  ```powershell
  cd eng/skill-eval
  npm ci
  ```

Run a suite (recommended):

```powershell
cd tools/azsdk-cli/Azure.Sdk.Tools.Vally
$vally = '../../../eng/skill-eval/node_modules/.bin/vally.cmd'

# Fast tiers only — PR-gate candidate
& $vally eval --suite pr-gate

# A single tier
& $vally eval --suite unit
& $vally eval --suite scenarios-mock

# By feature area (cross-cuts tiers via tag filter)
& $vally eval --suite release-plan
& $vally eval --suite typespec
```

Run a single eval:

```powershell
& $vally eval --eval-spec evals/unit/check-public-repo.eval.yaml
```

Run the live scenarios tier (first, prime a per-user clone of
`azure-rest-api-specs`; the helper refreshes it every 24h):

```powershell
./evals/setup/ensure-specs-clone.ps1
& $vally eval --suite scenarios-live
```

## Adding a new scenario

1. **Pick a tier** — the folder you drop the YAML into:
   - `evals/unit/` — one prompt, one MCP tool, no environment hooks.
   - `evals/scenarios/mock/` — multi-tool flow against `azsdk-mcp-mock`.
     Hermetic; runs on PR gate.
   - `evals/scenarios/live/` — needs real DevOps / GitHub / pipelines;
     bind `environment: azsdk-mcp-live`. Nightly only.
2. Pick a short, kebab-case name (e.g. `create-release-plan`).
3. Create `evals/<tier>/<name>.eval.yaml`. Start from a sibling in the same
   tier as a template.
4. **Tag it** so suite filters pick it up:
   ```yaml
   tags:
     area: release-plan   # or typespec / pipeline / github / engsys / apiview / package
   ```
5. If the scenario needs input files, add them under
   `fixtures/<name>/...` and reference them via `environment.files` in the
   eval (relative paths from the eval file).
6. Pick graders — they’re a **list**, stack as many as you need:
   - `tool-calls` — verify the agent invoked the expected MCP tool(s).
   - `skill-invocation` — verify the right skill routed (e2e only).
   - `tool-call-count` / `token-budget` / `turn-count` — chattiness / budget guards.
   - `output-matches` / `output-contains` — assert final-message shape.
   - `file-matches` / `file-exists` — verify produced/modified files.
   - `prompt` — LLM-as-judge for free-form quality checks.
   - Custom (`Graders/`) — add a .NET grader when no built-in fits.
7. The suite picks it up automatically (folders are globbed). Add a new
   tag-filtered suite to [`.vally.yaml`](.vally.yaml) only if you’re
   introducing a brand-new feature area.
8. Run locally to confirm it passes, then open a PR.

## Recovery checklist (from deleted benchmark)

Tracked in [#15124](https://github.com/Azure/azure-sdk-tools/issues/15124).
All 9 deleted scenarios have been ported as Vally `tool-calls` evals (presence
checks). Items marked with **(stub)** have known gaps documented inline in the
eval file:

- [x] `check-public-repo`
- [x] `check-public-repo-then-validate`
- [x] `validate-typespec`
- [x] `typespec-generation-step02`
- [x] `get-modified-typespec-projects` **(stub — needs git-repo fixture / setup hook)**
- [x] `add-arm-resource` **(stub — needs fixtures + `npx tsp compile` post-check)**
- [x] `create-release-plan`
- [x] `link-namespace-approval-issue`
- [x] `get-pr-link-current-branch`
- [x] `check-sdk-generation-status`
- [x] `rename-client-property` **(stub — needs `expected-diff` grader + sparse-clone of `azure-rest-api-specs`)**

### Known gaps vs. the original benchmark

The current `tool-calls` grader only checks tool *names*. The deleted
benchmark's `ToolCallValidator` additionally asserted:

1. **Argument values** (e.g. `serviceTreeId`, `buildId`, `typeSpecProjectPath`).
2. **Forbidden tools** (e.g. "must NOT call `azsdk_verify_setup`").
3. **Call order** (e.g. validate before check-public-repo).
4. **Optional tools** (calls that are allowed but not required).

Recovering 1–4 requires either upstream grader support in
`@microsoft/vally-cli` or a custom .NET grader under `Graders/`. Until then
those constraints are captured in prompt text and inline `TODO:` comments.

### Follow-ups

- [ ] Port `Evaluate_PromptToToolMatch` + `Evaluate_ToolDescriptionSimilarity`
      from `Azure.Sdk.Tools.Cli.Evaluations` (still uses Copilot-SDK evaluator).
- [ ] File upstream issue against `@microsoft/vally-cli` to add `forbidden`,
      `optional`, argument-matching, and ordering to the built-in `tool-calls`
      grader (or accept that those gaps need custom graders).
- [ ] Wire a `vally eval` CI job for this project (current
      [`.github/workflows/skill-eval.yml`](../../../.github/workflows/skill-eval.yml)
      runs `vally lint` only and is scoped to skills). See
      [#15126](https://github.com/Azure/azure-sdk-tools/issues/15126) and
      [#15127](https://github.com/Azure/azure-sdk-tools/issues/15127).
- [ ] Decide on `AuthoringScenario` parity: the 29 TypeSpec authoring cases
      are already covered as **skill evals** under
      [`.github/skills/azure-typespec-author/evaluate/evals/`](../../../.github/skills/azure-typespec-author/evaluate/evals/).
      Tracked as [#15767](https://github.com/Azure/azure-sdk-tools/issues/15767) —
      likely close as duplicate unless we also want tool-level coverage of the
      same prompts (catches catalog regressions even when the skill isn't
      triggered).
