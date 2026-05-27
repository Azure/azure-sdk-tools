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

Tracked in [#15124](https://github.com/Azure/azure-sdk-tools/issues/15124):

- [x] `check-public-repo` (reference scenario)
- [ ] `check-public-repo-then-validate`
- [ ] `validate-typespec`
- [ ] `typespec-generation-step02`
- [ ] `get-modified-typespec-projects`
- [ ] `add-arm-resource`
- [ ] `create-release-plan`
- [ ] `link-namespace-approval-issue`
- [ ] `get-pr-link-current-branch`
- [ ] `check-sdk-generation-status`
- [ ] Port `Evaluate_PromptToToolMatch` + `Evaluate_ToolDescriptionSimilarity`
      from `Azure.Sdk.Tools.Cli.Evaluations` (uses Copilot-SDK evaluator today).
