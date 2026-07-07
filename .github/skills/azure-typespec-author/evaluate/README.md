# Azure TypeSpec Author Skill Evaluations

This directory contains [Vally](https://aka.ms/vally) evaluation cases for the `azure-typespec-author` skill.

## Prerequisites

- [Vally CLI](https://aka.ms/vally) installed globally: `npm install -g @microsoft/vally-cli@0.6.0`
- The `azsdk-cli` MCP server built: `dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Cli`
- An API key for the model configured (e.g., Anthropic or OpenAI key via environment variable)

## Environment Setup

Before running any evals, prime the fixtures from the live
[azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs) `main` branch:

```powershell
# PowerShell
node scripts/setup-environment.js | Invoke-Expression
```

```bash
# Bash / Zsh
eval $(node scripts/setup-environment.js)
```

`setup-environment.js` calls `setup-fixture-files.js` and then runs `npm ci`. Together they:

1. Download `package.json` / `package-lock.json` into `fixtures/Microsoft.Widget/Widget/` and run
   `npm ci` there.
2. Download `.github/copilot-instructions.md` into `fixtures/instructions-test/copilot-instructions.md`.
3. Print the shell command that exports `FIXTURE_NODE_MODULES`.

Why each piece matters:

- **`FIXTURE_NODE_MODULES`** lets the agent symlink a prebuilt `node_modules` instead of running
  `npm install` on every case. Without it evals still work, just slower.
- **`copilot-instructions.md`** is copied into each run's `.github/` by the `azsdk-mcp` environments
  in `.vally.yaml`, so evals exercise the *real* spec-repo authoring guidance. It is intentionally
  **not** checked in (it is git-ignored) and always refreshed from `main`, so the eval reflects what
  authors actually see today.

CI runs `setup-fixture-files.js` during setup, so these fixtures are always present in pipeline runs.

## Running Evaluations

All commands below should be run from this directory (`evaluate/`).

### Run all evaluations by mode

```bash
vally eval --suite forced --skill-dir .. --output-dir ./result --workspace ./debug --verbose
vally eval --suite trigger --skill-dir .. --output-dir ./result --workspace ./debug --verbose
vally eval --suite no-skill --skill-dir /tmp/no-skills --output-dir ./result --workspace ./debug --verbose
```

### Run a single evaluation file with a specific mode

Combine `--eval-spec` with `--tag` to run a specific mode for a single test case:

```bash
# Run only the forced mode for a single file
vally eval --eval-spec evals/001001.eval.yaml --tag mode=forced --skill-dir .. --output-dir ./result --workspace ./debug --verbose

# Run only the trigger mode for a single file
vally eval --eval-spec evals/001001.eval.yaml --tag mode=trigger --skill-dir .. --output-dir ./result --workspace ./debug --verbose

# Run only the no-skill mode for a single file
vally eval --eval-spec evals/001001.eval.yaml --tag mode=no-skill --skill-dir /tmp/no-skills --output-dir ./result --workspace ./debug --verbose
```

### Which file to use

Use different entry files depending on your goal:

| File                     | When to use                            | Example command                                                                                                   |
| ------------------------ | -------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `.vally.yaml`            | Default local entry; run by suite name | `vally eval --suite versioning-forced --skill-dir .. --output-dir ./result --workspace ./debug --verbose`                        |
| `evals/00xxxx.eval.yaml` | Debug one specific case file           | `vally eval --eval-spec evals/003001.eval.yaml --skill-dir .. --output-dir ./result-003001 --workspace ./debug-003001 --verbose` |

Notes:

- Prefer `--suite` for day-to-day runs because it keeps filtering and mode selection centralized in `.vally.yaml`.
- Use `--eval-spec` + `--tag` for single-file targeted mode runs.

### Run a named test suite

Test suites are defined in `.vally.yaml` under the `suites` key. Available suites:

| Suite                           | Description                                    |
| ------------------------------- | ---------------------------------------------- |
| `versioning-forced`             | Versioning cases (001xxx) — forced mode        |
| `armtemplate-forced`            | ARM template cases (002xxx) — forced mode      |
| `longrunningoperation-forced`   | Long-running operation cases (003xxx) — forced |
| `decorators-forced`             | Decorator cases (004xxx) — forced mode         |
| `warning-forced`                | Warning cases (005xxx) — forced mode           |
| `versioning-trigger`            | Versioning cases — trigger mode                |
| `armtemplate-trigger`           | ARM template cases — trigger mode              |
| `longrunningoperation-trigger`  | LRO cases — trigger mode                       |
| `decorators-trigger`            | Decorator cases — trigger mode                 |
| `warning-trigger`               | Warning cases — trigger mode                   |
| `versioning-no-skill`           | Versioning cases — no-skill baseline           |
| `armtemplate-no-skill`          | ARM template cases — no-skill baseline         |
| `longrunningoperation-no-skill` | LRO cases — no-skill baseline                  |
| `decorators-no-skill`           | Decorator cases — no-skill baseline            |
| `warning-no-skill`              | Warning cases — no-skill baseline              |
| `forced`                        | All cases — forced mode                        |
| `trigger`                       | All cases — trigger mode                       |
| `no-skill`                      | All cases — no-skill baseline                  |

Run a suite by name:

```bash
vally eval --suite forced --skill-dir .. --output-dir ./result --workspace ./debug --verbose
```

### Suite modes (forced / trigger / no-skill)

Each test case has three modes:

| Mode       | Description                                                                              |
| ---------- | ---------------------------------------------------------------------------------------- |
| `forced`   | Skill explicitly invoked via `@azure-typespec-author` prefix + full code quality graders |
| `trigger`  | Tests whether the skill is automatically triggered (skill invocation detection)          |
| `no-skill` | Baseline run without loading the skill (`--skill-dir /tmp/no-skills`)                    |

Suite names follow the pattern `<domain>-<mode>`:

```bash
# Forced mode for LRO cases
vally eval --suite longrunningoperation-forced --skill-dir .. --output-dir ./result --workspace ./debug --verbose

# Trigger mode for versioning cases
vally eval --suite versioning-trigger --skill-dir .. --output-dir ./result --workspace ./debug --verbose
```

### Vally 0.6.0 local runs

Vally 0.6.0 uses the `copilot-sdk` executor and records skill/tool calls differently from 0.5.0.
For local forced and trigger runs, pass `--skill-dir ..` explicitly so Vally discovers the
`azure-typespec-author` skill directory that contains `SKILL.md`. The prompts still include the
`@azure-typespec-author` prefix, and the evals still use `skill-invocation` graders to verify that
the skill was invoked.

Compared with 0.5.0, 0.6.0 result output includes additional run metadata such as `Turns`,
`Tool Calls`, `Executor: copilot-sdk`, and richer JSONL fields (`type`, `itemId`, `stimulus`,
`durationMs`, `workspacePath`). Because tool-call tracking is stricter, forced-mode failures often
mean the agent did not call a required tool such as `azure-sdk-mcp-azsdk_run_typespec_validation`.
When debugging local runs, keep `--verbose` enabled and inspect the `Tool Calls` column in
`eval-results.md` or the trial entries in `results.jsonl`.

Do not add `--skill-dir ..` to no-skill baseline runs. Those runs intentionally use an empty skill
directory, for example `--skill-dir /tmp/no-skills`, to measure behavior without loading the skill.

### Run with tag filtering (optional)

Tag filtering is useful for ad-hoc local runs on individual files:

```bash
# Run forced mode for versioning cases only
vally eval --suite versioning-forced --skill-dir ..

# Run no-skill baseline for armtemplate cases
vally eval --suite armtemplate-no-skill --skill-dir /tmp/no-skills

# Run trigger detection for all cases (no tag = all 29 cases)
vally eval --suite trigger --skill-dir ..
```

### Useful flags

| Flag                           | Purpose                                                        |
| ------------------------------ | -------------------------------------------------------------- |
| `--keep-executor-session-logs` | Preserve agent session logs under `--output-dir` for debugging |
| `--verbose`                    | Show full agent output during the run                          |
| `--workers <n>`                | Run multiple stimuli in parallel (default: 5)                  |

### Parallel run the environment

By default evals use the `azsdk-mcp` environment (local KB at `http://localhost:8088`) one by one.
To use the remote KB environment, you can parallel run after editing environment to `azsdk-mcp-remote-kb`, and set workers with:

```bash
vally eval --eval-spec evals/eval.yaml --workers 3
```

## Results

Results are written to the `results/` directory after each run. You can rerun graders with:

```bash
vally eval --eval-spec evals/eval.yaml --skip-grade --output jsonl | vally grade --eval-spec evals/eval.yaml
```

```bash
vally grade --eval-spec evals/eval.yaml < results/results.jsonl
```

## File Structure

```
evaluate/
├── .vally.yaml          # Vally configuration: environments, suites, paths
├── evals/               # Individual eval specs (one per test case, for local dev)
│   ├── 001001.eval.yaml
│   ├── ...
├── fixtures/            # TypeSpec project fixtures referenced by evals
│   ├── 001-share-version-new-feature/
│   ├── Microsoft.Widget/
│   └── ...
├── scripts/             # Setup and utility scripts
├── results/             # Eval run output
└── debug/               # Eval run workspace
```

## Pipeline Architecture

The CI runs in three mode groups (forced, trigger, no-skill), and each group is split into
five suite steps. Each step runs the corresponding `.vally.yaml` suite branch directly,
for example `--suite versioning-forced`, `--suite versioning-trigger`, or
`--suite versioning-no-skill`. Vally 0.6.0 supports parallel execution across multiple
eval files, so the pipelines no longer need consolidated suite eval files plus
`--tag suite=...` filtering.

| Pipeline           | Command/source              | Purpose                                                               |
| ------------------ | --------------------------- | --------------------------------------------------------------------- |
| benchmark          | `--suite *-forced`          | Forced skill invocation + code-quality graders (real MCP environment) |
| benchmark          | `--suite *-trigger`         | Skill trigger detection (mock MCP environment)                        |
| benchmark-no-skill | `--suite *-no-skill`        | Baseline run without loading the skill (`--skill-dir /tmp/no-skills`) |

Each stimulus has dual tags: `suite` and `mode`, for example
`{ suite: versioning, mode: forced }`.
