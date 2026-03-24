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

# Run all scenarios and generate a report
dotnet run -- run --all --report

# Generate a report from existing log files
dotnet run -- report /path/to/logs --output report.md
```

### CLI Options

| Command | Description |
|---------|-------------|
| `list` | List all available scenarios with name, description, and tags |
| `run <name>` | Run a specific scenario by name |
| `run --all` | Run all discovered scenarios |
| `report <path>` | Generate a markdown report from existing benchmark log files |

| Option | Description | Default |
|--------|-------------|---------|
| `--model <model>` | Model to use for agent execution | `claude-opus-4.5` |
| `--cleanup <policy>` | Cleanup policy: `always`, `never`, `on-success` | `on-success` |
| `--repo <repo>` | Filter by repository (when used with `run`, requires `--all`; also supported with `list`). Append `:ref` to override the branch (see below) | — |
| `--tag <tag>` | Filter scenarios by tag (repeatable; when used with `run`, requires `--all`; also supported with `list`) | — |
| `--report` | Generate a markdown report after the run completes | `false` |
| `--output <path>` | Output file path for the report (report command only) | `report.md` in log dir |
| `--parallel <n>` | Max concurrent scenarios | `1` |
| `--verbose` | Show agent activity during execution | `false` |

### Branch Overrides for CI / PR Testing

By default, every scenario clones its repository at `main`. When running benchmarks for a pull request, use `--repo Owner/Name:branch` to override the ref:

```sh
# Run all scenarios targeting azure-rest-api-specs, but use the PR branch instead of main
dotnet run -- run --all --repo Azure/azure-rest-api-specs:feature/my-pr

# Filter only — no branch override (existing behavior)
dotnet run -- run --all --repo Azure/azure-rest-api-specs
```

The override applies to every matching `RepoConfig` in the selected scenarios (including `TargetRepos`).

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
├── Models/                   # Data models
│   ├── BenchmarkResult.cs    # Result of a benchmark run
│   └── ...
├── Reporting/                # Report generation
│   ├── ReportGenerator.cs    # LLM-based report generation using Copilot SDK
│   └── report-template.md    # Markdown report template
├── Scenarios/                # Benchmark scenario definitions
│   ├── BenchmarkScenario.cs  # Base class for all scenarios
│   └── ...
└── Program.cs                # Entry point
```

## Running in a Container

To prevent the agent from modifying your host environment (installing packages, changing runtime versions, etc.), you can run benchmarks inside a dev container using the target repo's dev container config. See [docs/running-in-containers.md](docs/running-in-containers.md) for details.

> **Note:** This is a temporary workaround while benchmarks have no built in sandboxing. Once proper sandboxing is available, container-based isolation may no longer be necessary.

## Creating Scenarios

See [docs/creating-scenarios.md](docs/creating-scenarios.md) for guidance on defining new benchmark scenarios.
