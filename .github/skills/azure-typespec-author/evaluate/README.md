# Azure TypeSpec Author Skill Evaluations

This directory contains [Vally](https://aka.ms/vally) evaluation cases for the `azure-typespec-author` skill.

## Prerequisites

- [Vally CLI](https://aka.ms/vally) installed globally: `npm install -g @microsoft/vally-cli`
- The `azsdk-cli` MCP server built: `dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Cli`
- An API key for the model configured (e.g., Anthropic or OpenAI key via environment variable)

## Running Evaluations

All commands below should be run from this directory (`evaluate/`).

### Run all evaluations (local dev)

```bash
vally eval --suite all --output-dir ./result --workspace ./debug --verbose
```

Or using the combined eval file:

```bash
vally eval --eval-spec evals/eval.yaml --output-dir ./result --workspace ./debug
```

### Run a single evaluation

Pass the path to a specific eval file:

```bash
vally eval --eval-spec evals/001001.eval.yaml --output-dir ./result-001001 --workspace ./debug-001001
```

### Run a named test suite

Test suites are defined in `.vally.yaml` under the `suites` key. Available suites:

| Suite                  | Description                           |
| ---------------------- | ------------------------------------- |
| `versioning`           | All versioning cases (001xxx)         |
| `version-evolution`    | Version evolution subset              |
| `armtemplate`          | ARM template cases (002xxx)           |
| `longrunningoperation` | Long-running operation cases (003xxx) |
| `decorators`           | Decorator cases (004xxx)              |
| `warning`              | Warning cases (005xxx)                |
| `all`                  | Every eval case                       |

Run a suite by name:

```bash
vally eval --suite versioning --output-dir versioning
```

### Run with tag filtering (same as pipeline)

The pipeline uses consolidated eval files with `--tag` to select subsets of cases.
You can replicate this locally:

```bash
# Run forced mode (with MCP + skill) for versioning cases only
vally eval --eval-spec suites/forced.eval.yaml --tag suite=versioning

# Run no-skill baseline for armtemplate cases
vally eval --eval-spec suites/no-skill.eval.yaml --tag suite=armtemplate --skill-dir /tmp/no-skills

# Run trigger detection for all cases (no tag = all 29 cases)
vally eval --eval-spec suites/trigger.eval.yaml
```

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
│   └── eval.yaml        # Combined file with all 29 cases (environment: azsdk-mcp)
├── suites/              # Consolidated eval files used by pipelines
│   ├── forced.eval.yaml    # Forced skill invocation + code quality graders
│   ├── trigger.eval.yaml   # Skill trigger detection (mock MCP)
│   └── no-skill.eval.yaml  # Pure agent baseline (no environment)
├── fixtures/            # TypeSpec project fixtures referenced by evals
│   ├── 001-share-version-new-feature/
│   ├── Microsoft.Widget/
│   └── ...
├── scripts/             # Setup and utility scripts
└── results/             # Eval run output
```

## Pipeline Architecture

The CI uses 3 consolidated eval files (one per mode) with `--tag suite=<name>` to run
each suite as a separate step with its own timeout:

| Pipeline | Eval file | Mode |
| -------- | --------- | ---- |
| benchmark | `suites/forced.eval.yaml` | Forced skill + MCP + code quality |
| benchmark | `suites/trigger.eval.yaml` | Skill invocation detection |
| benchmark-no-skill | `suites/no-skill.eval.yaml` | Pure agent baseline |

Each stimulus has a `tags: { suite: versioning|armtemplate|lro|decorators|warning }` field
that enables per-suite filtering via `--tag suite=<name>`.
