# Agentic doc-refinement for `azure-typespec-author`

Design + implementation of an agentic loop that closes the gap between the skill's
reference documentation, the Vally code-quality evals, and the resulting gap analysis.

## Goal

Automate the manual loop we run today:

1. Update the skill's **reference documents** using a defined prompt.
2. Update the skill **markdown** (`SKILL.md` / `references/*`) if needed.
3. Run the **Vally code-quality evals**.
4. Analyze the results to find, per case, **why it failed** and whether the failure is
   caused by a **documentation-coverage gap**.
5. Produce an **analysis report** with two sections: per-case failure reasons and the
   identified documentation gaps.

## Which steps are agent vs. deterministic

| Step | Kind | Driven by |
| --- | --- | --- |
| 1 Update reference docs | Agent (reasoning) | Copilot SDK + `prompts/01-update-reference-docs.md` |
| 2 Update skill markdown | Agent (reasoning) | Copilot SDK + `prompts/02-update-skill.md` |
| 3 Run Vally evals | Deterministic (infra-heavy) | `vally` CLI (local) |
| 4 Analyze results | Agent (reasoning) | Copilot SDK + `prompts/04-analyze-results.md` |
| 5 Generate report | Agent (reasoning) | Copilot SDK + `prompts/05-generate-report.md` |

## Approach A (primary) — run it as code with the Copilot SDK

**Chosen approach.** A single Node program orchestrates the whole loop in one process, so
step 3's Vally run happens **in the same environment** as the agent steps.

Why this is the default: the *forced* (code-quality) eval is infra-heavy. Vally spawns the
`azsdk-cli` MCP server and needs the **QA-bot KB backend** reachable on `:8088`
(`AZURE_SDK_KB_ENDPOINT`), plus a model-backed executor. That backend is internal, so the
eval can only run where that infra exists — the ADO benchmark pool or a fully set-up dev
box. Running the loop *as code on that box* is the simplest way to keep all five steps
together without shipping results between systems.

- **Script**: `doc-refinement.mjs` (this folder). Run with `npm ci && npm run refine`.
- **Agent steps (1, 2, 4, 5)** use the **Copilot SDK** (`@github/copilot-sdk`):
  `new CopilotClient()` -> `createSession({ model, workingDirectory, skillDirectories,
  onPermissionRequest: approveAll, systemMessage })` -> `session.sendAndWait({ prompt },
  timeout)`. `approveAll` auto-approves file/shell tool permissions for autonomous runs.
- **Step 3** runs `vally eval --suite forced --skill-dir .. --output-dir ./result
  --workspace ./debug --verbose` directly from `evaluate/` (the consolidated `forced`
  suite = all forced-mode cases); a non-zero exit is logged but still leaves
  `results.jsonl` for analysis.
- **Prompts and output are identical to Approach B** - only the orchestration differs.

> **Steps 1 & 2 never commit.** The agent leaves the reference-doc and skill edits
> **unstaged in the working tree** so you can review them and decide whether to commit.
> The orchestrator's system message and the step 1/2 prompts forbid `git add`/`commit`/
> `push`, so an autonomous run cannot commit them on your behalf.

### Flow (Approach A)

```
npm run refine
  |
  |- 1. agent -> references/reference-document-links.md
  |- 2. agent -> SKILL.md / references/*   (only if needed)
  |- 3. vally eval --suite forced --skill-dir .. -> evaluate/result/**/results.jsonl
  |- 4. agent -> evaluate/result/analysis.md   (per-case failure + doc-gap attribution)
  |- 5. agent -> evaluate/result/document-gaps.md   (final report)
```

### CLI

```
npm run refine                                  # full loop, default forced suites
node doc-refinement.mjs --skip-docs             # steps 3-5 only
node doc-refinement.mjs --skip-eval \
     --results-dir ../evaluate/result           # steps 4-5 over existing jsonl
node doc-refinement.mjs --suite warning-forced  # Limit to one suite (e.g. a single domain's forced cases)
node doc-refinement.mjs --model claude-opus-4.6 --idle-timeout 1800
```

### Prerequisites (Approach A)

- `npm ci` in this folder (installs `@github/copilot-sdk` + `@microsoft/vally-cli`).
- GitHub Copilot CLI installed & authenticated - the SDK spawns it (`copilot login`), or
  set `COPILOT_GITHUB_TOKEN`.
- For step 3 only: eval fixtures set up (`../evaluate/scripts/setup-environment.js`), the
  `azsdk-cli` MCP server buildable, and the KB backend reachable
  (`AZURE_SDK_KB_ENDPOINT` / `localhost:8088`). Skip with `--skip-eval` + `--results-dir`
  to (re)generate the report from an existing run.

## Approach B (optional) — GitHub Actions workflow

`.github/workflows/typespec-author-doc-refinement.yml` (`workflow_dispatch`) is a thin
wrapper that runs the **same orchestrator** (`doc-refinement.mjs`) on a runner — so
**step 3 runs `vally eval` directly on the runner; it never triggers any pipeline.** The
workflow checks out the branch, `npm ci`s this folder, runs `node doc-refinement.mjs` with
flags derived from its inputs, then force-adds the report and opens a PR.

**Inputs**: `skill_branch`, `update_docs` (steps 1-2), `run_eval` (step 3), `suite`
(default `forced`).
**Secrets**: `COPILOT_CLI_TOKEN` (passed to the orchestrator as `COPILOT_GITHUB_TOKEN`).
**Permissions**: `contents: write`, `pull-requests: write`.

### Requirement / limitation of Approach B

- **Step 3 needs the eval infra on the runner.** Because Vally runs directly (no pipeline),
  the runner must have the `azsdk-cli` MCP server buildable and the QA-bot KB backend
  reachable on `:8088` (`AZURE_SDK_KB_ENDPOINT`) plus the fixtures set up. That infra is
  internal, so run with `run_eval=true` only on a **self-hosted runner** provisioned with
  it. On a stock GitHub-hosted runner, dispatch with **`run_eval=false`** to perform only
  the agent doc/report steps against an already-present
  `evaluate/result/**/results.jsonl`.

Prefer Approach A for local/dev-box iteration; use Approach B when you want a hands-off,
PR-producing trigger from GitHub on a suitably provisioned runner.

## Prompt -> file mapping (both approaches)

| Prompt | Reads | Writes |
| --- | --- | --- |
| `prompts/01-update-reference-docs.md` | typespec-azure + typespec.io docs | `references/reference-document-links.md` |
| `prompts/02-update-skill.md` | updated references | `SKILL.md`, `references/*` (only if needed) |
| `prompts/04-analyze-results.md` | `**/results.jsonl` (`gradeResult.stimulusName/passed/details[]`) | `evaluate/result/analysis.md` |
| `prompts/05-generate-report.md` | step-4 analysis | `evaluate/result/document-gaps.md` |

## Layout

```
agentic-doc-refinement/
|- README.md              # this design + usage
|- doc-refinement.mjs     # Approach A orchestrator (Copilot SDK)
|- package.json           # pins @github/copilot-sdk + @microsoft/vally-cli
|- prompts/
   |- 01-update-reference-docs.md
   |- 02-update-skill.md
   |- 04-analyze-results.md
   |- 05-generate-report.md
```

(`evaluate/result/` is gitignored; the generated report lives there.
Approach B force-adds it when opening its PR.)

## Open assumptions

- "Defined prompts" = the four `prompts/*.md` files (seeded from the existing
  reference-doc sync prompt and the `evaluate/result/document-gaps.md`
  report format). Edit those files to change agent behaviour - the orchestrator is stable.
- Default agent model is `claude-opus-4.6` (matches the eval executor); override with
  `--model` / `AGENT_MODEL`.
