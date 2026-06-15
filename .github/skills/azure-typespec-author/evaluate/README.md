# Azure TypeSpec Author Skill Evaluations

This directory contains [Vally](https://aka.ms/vally) evaluation cases for the `azure-typespec-author` skill.

## Prerequisites

- [Vally CLI](https://aka.ms/vally) installed globally: `npm install -g @microsoft/vally-cli`
- The `azsdk-cli` MCP server built: `dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Cli`
- An API key for the model configured (e.g., Anthropic or OpenAI key via environment variable)

## Environment Setup

Run the setup script to download spec repo package files, run `npm ci`, and configure `FIXTURE_NODE_MODULES`:

```powershell
# PowerShell
node scripts/setup-environment.js | Invoke-Expression
```

```bash
# Bash / Zsh
eval $(node scripts/setup-environment.js)
```

This script:
1. Clones `package.json` and `package-lock.json` from [azure-rest-api-specs](https://github.com/Azure/azure-rest-api-specs) into `fixtures/Microsoft.Widget/Widget/`.
2. Runs `npm ci` in that directory.
3. Outputs the shell command to set `FIXTURE_NODE_MODULES` for symlink usage.

Without `FIXTURE_NODE_MODULES`, the agent will run `npm install` each time (slow but functional).

## Running Evaluations

All commands below should be run from this directory (`evaluate/`).

### Run all evaluations by mode

```bash
vally eval --suite forced --output-dir ./result --workspace ./debug --verbose
vally eval --suite trigger --output-dir ./result --workspace ./debug --verbose
vally eval --suite no-skill --output-dir ./result --workspace ./debug --verbose
```

### Run a single evaluation file with a specific mode

Combine `--eval-spec` with `--tag` to run a specific mode for a single test case:

```bash
# Run only the forced mode for a single file
vally eval --eval-spec evals/001001.eval.yaml --tag mode=forced --output-dir ./result --workspace ./debug --verbose

# Run only the trigger mode for a single file
vally eval --eval-spec evals/001001.eval.yaml --tag mode=trigger --output-dir ./result --workspace ./debug --verbose

# Run only the no-skill mode for a single file
vally eval --eval-spec evals/001001.eval.yaml --tag mode=no-skill --output-dir ./result --workspace ./debug --verbose
```

### Which file to use

Use different entry files depending on your goal:

| File | When to use | Example command |
| --- | --- | --- |
| `.vally.yaml` | Default local entry; run by suite name | `vally eval --suite versioning-forced --output-dir ./result --workspace ./debug --verbose` |
| `evals/00xxxx.eval.yaml` | Debug one specific case file | `vally eval --eval-spec evals/003001.eval.yaml --output-dir ./result-003001 --workspace ./debug-003001 --verbose` |

Notes:

- Prefer `--suite` for day-to-day runs because it keeps filtering and mode selection centralized in `.vally.yaml`.
- Use `--eval-spec` + `--tag` for single-file targeted mode runs.

### Run a named test suite

Test suites are defined in `.vally.yaml` under the `suites` key. Available suites:

| Suite                           | Description                                      |
| ------------------------------- | ------------------------------------------------ |
| `versioning-forced`             | Versioning cases (001xxx) — forced mode          |
| `armtemplate-forced`            | ARM template cases (002xxx) — forced mode        |
| `longrunningoperation-forced`   | Long-running operation cases (003xxx) — forced   |
| `decorators-forced`             | Decorator cases (004xxx) — forced mode           |
| `warning-forced`                | Warning cases (005xxx) — forced mode             |
| `versioning-trigger`            | Versioning cases — trigger mode                  |
| `armtemplate-trigger`           | ARM template cases — trigger mode                |
| `longrunningoperation-trigger`  | LRO cases — trigger mode                         |
| `decorators-trigger`            | Decorator cases — trigger mode                   |
| `warning-trigger`               | Warning cases — trigger mode                     |
| `versioning-no-skill`           | Versioning cases — no-skill baseline             |
| `armtemplate-no-skill`          | ARM template cases — no-skill baseline           |
| `longrunningoperation-no-skill` | LRO cases — no-skill baseline                    |
| `decorators-no-skill`           | Decorator cases — no-skill baseline              |
| `warning-no-skill`              | Warning cases — no-skill baseline                |
| `forced`                        | All cases — forced mode                          |
| `trigger`                       | All cases — trigger mode                         |
| `no-skill`                      | All cases — no-skill baseline                    |

Run a suite by name:

```bash
vally eval --suite forced --output-dir ./result --workspace ./debug --verbose
```

### Suite modes (forced / trigger / no-skill)

Each test case has three modes:

| Mode | Description |
| --- | --- |
| `forced` | Skill explicitly invoked via `@azure-typespec-author` prefix + full code quality graders |
| `trigger` | Tests whether the skill is automatically triggered (skill invocation detection) |
| `no-skill` | Baseline run without loading the skill (`--skill-dir /tmp/no-skills`) |

Suite names follow the pattern `<domain>-<mode>`:

```bash
# Forced mode for LRO cases
vally eval --suite longrunningoperation-forced --output-dir ./result --workspace ./debug --verbose

# Trigger mode for versioning cases
vally eval --suite versioning-trigger --output-dir ./result --workspace ./debug --verbose
```

### Run with tag filtering (optional)

Tag filtering is useful for ad-hoc local runs on individual files:

```bash
# Run forced mode for versioning cases only
vally eval --eval-spec suites/forced.eval.yaml --tag suite=versioning

# Run no-skill baseline for armtemplate cases
vally eval --eval-spec suites/no-skill.eval.yaml --tag suite=armtemplate --skill-dir /tmp/no-skills

# Run trigger detection for all cases (no tag = all 29 cases)
vally eval --eval-spec suites/trigger.eval.yaml
```

### Useful flags

| Flag | Purpose |
|---|---|
| `--keep-executor-session-logs` | Preserve agent session logs under `--output-dir` for debugging |
| `--verbose` | Show full agent output during the run |
| `--workers <n>` | Run multiple stimuli in parallel (default: 5) |

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
│   └── forced.eval.yaml # Consolidated forced-mode file with all cases
├── suites/              # Consolidated eval files used by pipelines
│   ├── forced.eval.yaml    # Forced skill invocation + code quality graders
│   ├── trigger.eval.yaml   # Skill trigger detection (mock MCP)
│   └── no-skill.eval.yaml  # Pure agent baseline (no environment)
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
five suite steps. Splitting by suite makes error logs easier to query and helps isolate failures
to a small case set.

The pipelines use `--eval-spec` with the consolidated suite files under `suites/`, and use `--tag suite=...` to split runs by domain:

| Pipeline | Eval file | Purpose |
| -------- | --------- | ------- |
| benchmark | `suites/forced.eval.yaml` | Forced skill invocation + code-quality graders (real MCP environment) |
| benchmark | `suites/trigger.eval.yaml` | Skill trigger detection (mock MCP environment) |
| benchmark-no-skill | `suites/no-skill.eval.yaml` | Baseline run without loading the skill (`--skill-dir /tmp/no-skills`) |

Each stimulus has dual tags: `suite` and `mode`, for example
`{ suite: versioning, mode: forced }`.
