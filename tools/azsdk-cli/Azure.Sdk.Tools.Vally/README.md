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
| **Path** | [`tools/azsdk-cli/Azure.Sdk.Tools.Vally/evals/`](evals/) (`tools/` + `workflow-scenarios/`) | [`.github/skills/<skill-name>/evals/*.eval.yaml`](../../../.github/skills/) (and `evaluate/evals/` for capability suites) |
| **Loaded subject** | Production MCP server (`Azure.Sdk.Tools.Cli`) over stdio — real tools, real network calls | Skill's `SKILL.md` + frontmatter; the agent picks tools itself |
| **Primary grader** | `tool-calls` — checks the recorded trajectory for required tool names | Trigger / routing graders + per-skill rubric |
| **Run command** | `vally eval --eval-spec evals/tools/<name>.eval.yaml` *from this directory* | `vally eval --skill-dir .github/skills/<skill-name>` *from repo root* |
| **CI status** | Phase 1 mock vertical in [`eng/pipelines/vally-eval.yml`](../../../eng/pipelines/vally-eval.yml) (hermetic `unit` + mock tiers, detect→shard→summarize); live tier deferred | `vally lint` runs in [.github/workflows/skill-eval.yml](../../../.github/workflows/skill-eval.yml); full `eval` job pending |
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

#### `evals/tools/` — hermetic single-tool evals (10 files)

One prompt → one expected MCP tool. No `environment.git`, no fixtures. Fast; safe to run on every PR. The per-namespace `prompt-to-tool-*.eval.yaml` files group every prompt→tool check by tool namespace (trigger coverage ported from [#15183](https://github.com/Azure/azure-sdk-tools/pull/15183)); `add-arm-resource` is the one file-producing exception.

| Scenario | Area | Shape |
|---|---|---|
| [`add-arm-resource`](evals/tools/add-arm-resource.eval.yaml) | typespec | File-producing scenario — calls `azsdk_typespec_generate_authoring_plan` + `edit` for an ARM resource |
| [`prompt-to-tool-apiview`](evals/tools/prompt-to-tool-apiview.eval.yaml) | apiview | `azsdk_apiview_*` |
| [`prompt-to-tool-config`](evals/tools/prompt-to-tool-config.eval.yaml) | engsys | `azsdk_check_service_label`, `azsdk_create_service_label` |
| [`prompt-to-tool-engsys`](evals/tools/prompt-to-tool-engsys.eval.yaml) | engsys | `azsdk_analyze_log_file`, failed-test tools, codeowner-cache |
| [`prompt-to-tool-github`](evals/tools/prompt-to-tool-github.eval.yaml) | github | `azsdk_create_pull_request`, `azsdk_get_pull_request*`, `azsdk_get_github_user_details`, `azsdk_get_pull_request_link_for_current_branch` |
| [`prompt-to-tool-package`](evals/tools/prompt-to-tool-package.eval.yaml) | package | `azsdk_package_*`, `azsdk_release_sdk` |
| [`prompt-to-tool-pipeline`](evals/tools/prompt-to-tool-pipeline.eval.yaml) | pipeline | `azsdk_analyze_pipeline`, `azsdk_get_pipeline_*` |
| [`prompt-to-tool-releaseplan`](evals/tools/prompt-to-tool-releaseplan.eval.yaml) | release-plan | `azsdk_*_release_plan*`, `azsdk_run_generate_sdk`, `azsdk_link_*` |
| [`prompt-to-tool-typespec`](evals/tools/prompt-to-tool-typespec.eval.yaml) | typespec | `azsdk_typespec_*`, `azsdk_convert_swagger_to_typespec`, `azsdk_customized_code_update`, `azsdk_run_typespec_validation` |
| [`prompt-to-tool-verify`](evals/tools/prompt-to-tool-verify.eval.yaml) | engsys | `azsdk_verify_setup` |

#### `evals/workflow-scenarios/` — multi-tool scenarios (5)

Multi-step prompts that exercise 2+ MCP tools end-to-end. Split into
`mock/` (hermetic, runs on PR gate) and `live/` (real DevOps / GitHub /
pipelines, runs nightly).

| Scenario | Area | Mode | Shape |
|---|---|---|---|
| [`check-public-repo-then-validate`](evals/workflow-scenarios/mock/check-public-repo-then-validate.eval.yaml) | typespec | mock | Validate, then check public-repo presence |
| [`typespec-generation-step02`](evals/workflow-scenarios/mock/typespec-generation-step02.eval.yaml) | typespec | mock | Step in the spec-PR generation flow |
| [`rename-client-property`](evals/workflow-scenarios/mock/rename-client-property.eval.yaml) | typespec | mock | Stub — needs `expected-diff` grader + sparse clone |
| [`release-planner-workflows`](evals/workflow-scenarios/mock/release-planner-workflows.eval.yaml) | release-plan | mock | Create / re-fetch / link / update release-plan flows (5 stimuli) |
| [`release-planner`](evals/workflow-scenarios/live/release-planner.eval.yaml) | release-plan | **live** | Create + re-fetch a release plan, kick off SDK gen, link PR back — real DevOps test-area writes |

Live scenarios need a primed `azure-rest-api-specs` clone — run
[`sync-eval-git-repo.js`](../../../eng/common/scripts/eval/sync-eval-git-repo.js)
(`node ../../../eng/common/scripts/eval/sync-eval-git-repo.js`; local-only
helper, auto-refreshes every 24h) before invoking the
`scenarios-live` / `nightly` suite.

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
│   ├── tools/                  # tool-shape + per-skill trigger evals, hermetic
│   ├── workflow-scenarios/
│   │   ├── mock/              # multi-tool scenarios, hermetic (PR gate)
│   │   └── live/              # multi-tool scenarios, live MCP (nightly)
│   └── fixtures/              # (future) pinned SHAs + per-eval mocks
├── fixtures/                  # Per-scenario static input files (env.files)
│   └── <scenario-name>/...
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

## Quickstart — run one scenario

The fastest path from a fresh clone to a green eval. Swap the path after
`-e` for any other `.eval.yaml` to try a different scenario.

### 1. One-time setup

```powershell
# From repo root
cd eng/skill-eval; npm ci; cd ../..
dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Cli  -c Debug -o artifacts/mcp/cli
dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Mock -c Debug -o artifacts/mcp/mock
```

Rebuild the MCP servers any time you edit tool source. Vally does **not**
rebuild them — it just spawns the existing DLL. `-o artifacts/mcp/{cli,mock}`
pins the output path so `.vally.yaml` doesn't need to know the target
framework moniker (avoids `Debug/net8.0/...` drift).

### 2. Move into this project and stash the paths

All commands below run from here:

```powershell
cd tools/azsdk-cli/Azure.Sdk.Tools.Vally
$vally  = '../../../eng/skill-eval/node_modules/.bin/vally.cmd'
$skills = '../../../.github/skills'
```

### 3. Run a scenario

**One namespace's trigger evals** (hermetic; a handful of prompt→tool checks):

```powershell
& $vally eval -e evals/tools/prompt-to-tool-releaseplan.eval.yaml --skill-dir $skills
```

**The release-planner mock workflow** (~4 min, 5 stimuli, hermetic):

```powershell
& $vally eval -e evals/workflow-scenarios/mock/release-planner-workflows.eval.yaml --skill-dir $skills
```

**The release-planner live workflow** (~15 min, real DevOps writes to the
test area; prime the spec clone once):

```powershell
node ../../../eng/common/scripts/eval/sync-eval-git-repo.js
& $vally eval -e evals/workflow-scenarios/live/release-planner.eval.yaml --skill-dir $skills --workers 1
```

### 4. Pick a different scenario

```powershell
# List everything you can pass to -e
Get-ChildItem evals -Recurse -Filter *.eval.yaml | ForEach-Object FullName
```

Common swaps:

| What you want | Replace `-e` value with |
|---|---|
| A different tool namespace | `evals/tools/prompt-to-tool-apiview.eval.yaml` |
| A TypeSpec workflow | `evals/workflow-scenarios/mock/check-public-repo-then-validate.eval.yaml` |
| All triggers for one feature | drop `-e` and use `--suite typespec` (or `release-plan`, `github`, …) |
| Everything hermetic | drop `-e` and use `--suite pr-gate` |

### 5. Read the results

- Live PASS/FAIL table prints to the terminal.
- Each run writes a timestamped folder under the output dir (default
  `./vally-results/<timestamp>/`) containing `eval-results.md`, a
  human-readable summary. Add `--output jsonl` to also emit
  `results.jsonl` — one line per stimulus run, with the prompt, every
  tool call, and the final agent message.
- Add `--output-dir vally-results/<your-tag>` to nest runs under a stable
  parent you can re-open later.

## Running locally (advanced)

Prereqs are the same as the [Quickstart](#quickstart--run-one-scenario)
plus Node 22+ and a .NET SDK matching `global.json`.

Run a suite (recommended):

```powershell
cd tools/azsdk-cli/Azure.Sdk.Tools.Vally
$vally = '../../../eng/skill-eval/node_modules/.bin/vally.cmd'
$skills = '../../../.github/skills'

# Fast tiers only — PR-gate candidate
& $vally eval --suite pr-gate --skill-dir $skills

# A single tier
& $vally eval --suite unit --skill-dir $skills
& $vally eval --suite scenarios-mock --skill-dir $skills

# By feature area (cross-cuts tiers via tag filter)
& $vally eval --suite release-plan --skill-dir $skills
& $vally eval --suite typespec --skill-dir $skills
```

> `--skill-dir` is **required** for workflow-scenario evals — without it,
> the agent never loads the project skills and the `skill-invocation`
> grader fails even when the tool calls are correct.
>
> Each agent still boots its own MCP child process, but `.vally.yaml`
> launches the **pre-built** `azsdk-mock.dll` / `azsdk.dll` via
> `dotnet <dll>` (read-only memory-map, no MSBuild on the hot path), so
> `--workers 6+` is safe for `scenarios-mock`. The old MSBuild boot race
> is gone; the only remaining concurrency limit is rate limits on the
> Copilot CLI subprocesses.

Run a single eval:

```powershell
& $vally eval --eval-spec evals/workflow-scenarios/mock/check-public-repo-then-validate.eval.yaml --skill-dir $skills
```

Run the live scenarios tier (first, prime a per-user clone of
`azure-rest-api-specs`; the helper refreshes it every 24h):

```powershell
node ../../../eng/common/scripts/eval/sync-eval-git-repo.js
& $vally eval --suite scenarios-live --skill-dir $skills --workers 1
```

## Command cookbook

All recipes assume the two path variables are set first:

```powershell
$vally  = (Resolve-Path '../../../eng/skill-eval/node_modules/.bin/vally.cmd').Path
$skills = '../../../.github/skills'
```

Run several eval files at once (repeat `-e`):

```powershell
& $vally eval `
  -e evals/tools/prompt-to-tool-apiview.eval.yaml `
  -e evals/tools/prompt-to-tool-package.eval.yaml `
  --skill-dir $skills
```

Hunt for flaky stimuli by repeating each one (`--runs`) and writing
machine-readable output:

```powershell
& $vally eval -e evals/tools/prompt-to-tool-typespec.eval.yaml `
  --runs 5 --workers 2 --model gpt-5.4 `
  --output jsonl --output-dir vally-results
```

> `--workers 2` is the safe ceiling when `--runs` is high: the Copilot
> CLI subprocesses share an auth/session pool, and pushing more parallel
> agents tends to surface `status: error` infra noise (auth/session
> exhaustion) rather than real grade failures. Bump workers only for the
> pre-built `scenarios-mock` suite, which has no MSBuild boot race.

Run by tag or suite instead of listing files:

```powershell
& $vally eval --suite pr-gate     --skill-dir $skills   # tools/* + workflow-scenarios/mock/*
& $vally eval --suite scenarios-mock --skill-dir $skills --workers 6
```

Summarize a `results.jsonl` into a PASS / FLAKY / HARD-FAIL table
(filtering out `status: error` infra noise):

```powershell
$run  = Get-ChildItem vally-results -Recurse -Filter results.jsonl |
    Sort-Object LastWriteTime | Select-Object -Last 1   # newest run
$rows = Get-Content $run.FullName | ForEach-Object { $_ | ConvertFrom-Json } |
    Where-Object { $_.status -ne 'error' }
$rows | Group-Object { $_.gradeResult.stimulusName } | ForEach-Object {
    $passed = ($_.Group | Where-Object { $_.gradeResult.passed }).Count
    $total  = $_.Count
    [pscustomobject]@{
        Stimulus = $_.Name
        Passed   = "$passed/$total"
        Verdict  = if ($passed -eq $total) { 'PASS' }
                   elseif ($passed -eq 0)  { 'HARD-FAIL' }
                   else                    { 'FLAKY' }
    }
} | Sort-Object Verdict, Stimulus | Format-Table -AutoSize
```

> Exit code `1` is expected whenever any stimulus fails — it does not
> mean the run itself errored. While iterating on prompts, read the
> per-stimulus verdicts (or the JSONL) rather than the process exit code.

## Adding a new scenario

1. **Pick a tier** — the folder you drop the YAML into:
   - `evals/tools/` — one prompt, one MCP tool, no environment hooks.
   - `evals/workflow-scenarios/mock/` — multi-tool flow against
     `azsdk-mcp-mock`. Hermetic; runs on PR gate.
   - `evals/workflow-scenarios/live/` — needs real DevOps / GitHub /
     pipelines; bind `environment: azsdk-mcp-live`. Nightly only.
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
