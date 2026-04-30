# Azure TypeSpec Author Skill Evaluations

This directory contains [Vally](https://vally.dev) evaluation cases for the `azure-typespec-author` skill.

## Prerequisites

- [Vally CLI](https://vally.dev/docs/getting-started) installed globally: `npm install -g vally`
- The `azsdk-cli` MCP server built: `dotnet build tools/azsdk-cli/Azure.Sdk.Tools.Cli`
- An API key for the model configured (e.g., Anthropic or OpenAI key via environment variable)

## Running Evaluations

All commands below should be run from this directory (`evaluate/`).

### Run all evaluations

```bash
vally eval --suite all --output-dir ./result --workspace ./debug --verbose
```

Or equivalently, run every `*.eval.yaml` file at once:

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

| Suite | Description |
|---|---|
| `versioning` | All versioning cases (001xxx) |
| `version-evolution` | Version evolution subset |
| `armtemplate` | ARM template cases (002xxx) |
| `longrunningoperation` | Long-running operation cases (003xxx) |
| `decorators` | Decorator cases (004xxx) |
| `warning` | Warning cases (005xxx) |
| `all` | Every eval case |

Run a suite by name:

```bash
vally eval --suite versioning --output-dir versioning
```

### Parallel run the environment

By default evals use the `azsdk-mcp` environment (local KB at `http://localhost:8088`) one by one.
To use the remote KB environment, you can parallel run after editing environment to `azsdk-mcp-remote-kb`, and set workers with:

```bash
vally eval --eval-spec eval.yaml --workers 3
```

## Results

Results are written to the `results/` directory after each run. You can rerun graders with:

```bash
vally eval --eval-spec eval.yaml --skip-grade --output jsonl | vally grade --eval-spec eval.yaml
```

```bash
vally grade --eval-spec eval.yaml < results/results.jsonl
```

## File Structure

```
evaluate/
├── .vally.yaml          # Vally configuration: environments, suites, paths
├── evals/               # Individual eval specs (one per test case)
│   ├── 001001.eval.yaml
│   ├── ...
│   └── eval.yaml        # Combined file with all cases (used for parallel runs)
├── fixtures/            # TypeSpec project fixtures referenced by evals
│   ├── 001-share-version-new-feature/
│   ├── Microsoft.Widget/
│   └── ...
└── results/             # Eval run output
└── debug/               # Eval run workspace
```
