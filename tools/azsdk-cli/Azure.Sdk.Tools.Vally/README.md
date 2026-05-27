# Azure.Sdk.Tools.Vally

MCP-tool / end-to-end scenario evaluations for the `azsdk` MCP server, run via
[`@microsoft/vally-cli`](https://www.npmjs.com/package/@microsoft/vally-cli).

These evals are **distinct from the skill evals under `.github/skills/`**:

- **Skill evals** test that the agent picks and follows a specific skill
  (routing + skill capability).
- **Tool-scenario evals here** test that, given a user prompt, the agent
  invokes the right MCP tool(s) end-to-end — independent of any one skill.
  This includes single-tool checks (e.g. "agent invokes
  `azsdk_typespec_check_project_in_public_repo`") as well as multi-step
  scenarios that span several MCP calls (release-plan, SDK generation,
  release status, etc.).

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
- [ ] Wire a `vally eval` CI job (current `.github/workflows/skill-eval.yml`
      runs `vally lint` only). See [#15126](https://github.com/Azure/azure-sdk-tools/issues/15126)
      and [#15127](https://github.com/Azure/azure-sdk-tools/issues/15127).
