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
| **Path** | [`tools/azsdk-cli/Azure.Sdk.Tools.Vally/evals/*.eval.yaml`](evals/) | [`.github/skills/<skill-name>/evals/*.eval.yaml`](../../../.github/skills/) (and `evaluate/evals/` for capability suites) |
| **Loaded subject** | Production MCP server (`Azure.Sdk.Tools.Cli`) over stdio — real tools, real network calls | Skill's `SKILL.md` + frontmatter; the agent picks tools itself |
| **Primary grader** | `tool-calls` — checks the recorded trajectory for required tool names | Trigger / routing graders + per-skill rubric |
| **Run command** | `vally eval --eval-spec evals/<name>.eval.yaml` *from this directory* | `vally eval --skill-dir .github/skills/<skill-name>` *from repo root* |
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

**Tool-scenario evals (this project)** — 11 scenarios under [`evals/`](evals/):

| Scenario | Shape |
|---|---|
| [`check-public-repo`](evals/check-public-repo.eval.yaml) | Single-tool: is a TypeSpec project published in `azure-rest-api-specs`? |
| [`check-public-repo-then-validate`](evals/check-public-repo-then-validate.eval.yaml) | Multi-tool, ordered: validate then check |
| [`validate-typespec`](evals/validate-typespec.eval.yaml) | Single-tool: run `tsp` linter/validation |
| [`typespec-generation-step02`](evals/typespec-generation-step02.eval.yaml) | Step in the spec-PR generation flow |
| [`get-modified-typespec-projects`](evals/get-modified-typespec-projects.eval.yaml) | Git-aware tool against current branch |
| [`add-arm-resource`](evals/add-arm-resource.eval.yaml) | Calls `azsdk_typespec_generate_authoring_plan` for an ARM resource |
| [`create-release-plan`](evals/create-release-plan.eval.yaml) | Single-tool: create a release-plan work item |
| [`link-namespace-approval-issue`](evals/link-namespace-approval-issue.eval.yaml) | Link an existing approval issue to a release plan |
| [`get-pr-link-current-branch`](evals/get-pr-link-current-branch.eval.yaml) | Resolve the PR for the active git branch |
| [`check-sdk-generation-status`](evals/check-sdk-generation-status.eval.yaml) | Pipeline status lookup |
| [`rename-client-property`](evals/rename-client-property.eval.yaml) | Stub — needs `expected-diff` grader |

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
├── evals/                     # Scenario eval YAML files
│   └── *.eval.yaml
├── fixtures/                  # Per-scenario file fixtures
│   └── <scenario-name>/...
└── Graders/                   # (future) Custom .NET graders
    └── Azure.Sdk.Tools.Vally.csproj  # added when first custom grader lands
```

## Running locally

Prereqs:

- Node 22+
- .NET SDK matching the rest of the repo (see `global.json`)
- `@microsoft/vally-cli` installed via the repo's pinned lockfile:

  ```powershell
  cd eng/skill-eval
  npm ci
  ```

Run all tool-scenario evals from this directory:

```powershell
cd tools/azsdk-cli/Azure.Sdk.Tools.Vally
../../../eng/skill-eval/node_modules/.bin/vally run .
```

Run a single eval:

```powershell
../../../eng/skill-eval/node_modules/.bin/vally run evals/check-public-repo.eval.yaml
```

## Adding a new scenario

1. Pick a short, kebab-case name (e.g. `create-release-plan`).
2. Create `evals/<name>.eval.yaml`. Start from
   [`evals/check-public-repo.eval.yaml`](evals/check-public-repo.eval.yaml) as
   a template.
3. If the scenario needs input files, add them under
   `fixtures/<name>/...` and reference them via `environment.files` in the
   eval (relative paths from the eval file).
4. Pick graders:
   - `tool-calls` — verify the agent invoked the expected MCP tool(s).
   - `file-matches` — verify the agent produced/modified files correctly.
   - `prompt` — LLM-as-judge for free-form quality checks.
   - Custom (`Graders/`) — add a .NET grader when none of the built-ins fit
     (and add the `Azure.Sdk.Tools.Vally.csproj` when the first one lands).
5. Add the new eval path to the relevant `suites:` entry in
   [`.vally.yaml`](.vally.yaml).
6. Run locally to confirm it passes, then open a PR.

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
