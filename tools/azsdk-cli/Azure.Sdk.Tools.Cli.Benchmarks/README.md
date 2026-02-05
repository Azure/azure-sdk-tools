# Azure SDK Benchmarks

A benchmarking framework for testing AI agent capabilities on Azure SDK tasks using real end-to-end execution.

## Purpose

This project tests that AI agents can correctly complete real Azure SDK tasks. Unlike the Evaluations project, Benchmarks:

- Uses GitHub Copilot SDK to run actual agent sessions
- Executes real tools against cloned repositories
- Validates actual outcomes (e.g., correct TypeSpec file edits, proper code changes)
- Measures end-to-end task completion, not just tool selection

## Benchmarks vs Evaluations

| Aspect | Evaluations | Benchmarks |
|--------|-------------|------------|
| Tool execution | Mocked responses | Real execution |
| Validation | Tool selection correctness | Task outcome correctness |
| Speed | Fast (no real I/O) | Slower (full agent loop) |
| Use case | Unit testing tool dispatch | Integration testing agent behavior |

## How to Use

### Prerequisites

- [.NET 8.0 SDK or later](https://dotnet.microsoft.com/download)
- Copilot CLI installed and authenticated

### Build

```sh
dotnet build
```

### Run

```sh
# List all available scenarios
dotnet run -- list

# Run a specific scenario by name
dotnet run -- run rename-client-property

# Run all scenarios
dotnet run -- run --all

# Run with options
dotnet run -- run rename-client-property --model gpt-4o --cleanup never
```

### CLI Options

| Command | Description |
|---------|-------------|
| `list` | List all available scenarios with name, description, and tags |
| `run <name>` | Run a specific scenario by name |
| `run --all` | Run all discovered scenarios |

| Option | Description | Default |
|--------|-------------|---------|
| `--model <model>` | Model to use for agent execution | `claude-opus-4.5` |
| `--cleanup <policy>` | Cleanup policy: `always`, `never`, `on-success` | `on-success` |

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `AZSDK_MCP_PATH` | Path to azsdk MCP server executable | Uses repo config or system PATH |
| `AZSDK_BENCHMARKS_REPO_CACHE` | Directory for cached bare repository clones | `~/.cache/azsdk-benchmarks/repos` |
| `AZSDK_BENCHMARKS_WORKSPACE_DIR` | Directory for isolated worktree workspaces | `/tmp/azsdk-benchmarks/workspaces` |

## Project Structure

```
Azure.Sdk.Tools.Cli.Benchmarks/
├── Infrastructure/           # Core framework
│   ├── BenchmarkRunner.cs    # Orchestrates scenario execution
│   ├── SessionExecutor.cs    # Runs Copilot agent sessions
│   ├── WorkspaceManager.cs   # Manages repo cloning and worktrees
│   └── ...
├── Scenarios/                # Benchmark scenario definitions
│   ├── BenchmarkScenario.cs  # Base class for all scenarios
│   └── ...
└── Program.cs                # Entry point
```

## Creating Scenarios

See [docs/creating-scenarios.md](docs/creating-scenarios.md) for guidance on defining new benchmark scenarios.
