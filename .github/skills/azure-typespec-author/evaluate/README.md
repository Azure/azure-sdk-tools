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
vally run --suite all
```

Or equivalently, run every `*.eval.yaml` file at once:

```bash
vally run evals/*.eval.yaml
```

### Run a single evaluation

Pass the path to a specific eval file:

```bash
vally run evals/001001.eval.yaml
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
vally run --suite versioning
```

### Override the environment

By default evals use the `azsdk-mcp` environment (local KB at `http://localhost:8088`).
To use the remote KB environment (no local KB required), specify the environment explicitly:

```bash
vally run --environment azsdk-mcp-remote-kb evals/001001.eval.yaml
```

Or for an entire suite:

```bash
vally run --suite versioning --environment azsdk-mcp-remote-kb
```

## Results

Results are written to the `results/` directory after each run. You can view them with:

```bash
vally report results/
```

## File Structure

```
evaluate/
├── .vally.yaml          # Vally configuration: environments, suites, paths
├── evals/               # Individual eval specs (one per test case)
│   ├── 001001.eval.yaml
│   ├── ...
│   └── all.eval.yaml    # Combined file with all cases (used for parallel runs)
├── fixtures/            # TypeSpec project fixtures referenced by evals
│   ├── 001-share-version-new-feature/
│   ├── Microsoft.Widget/
│   └── ...
└── results/             # Eval run output (git-ignored)
```
