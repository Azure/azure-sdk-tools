# Running Benchmarks in a Container

> **Temporary workaround.** The Copilot CLI does not yet sandbox agent execution. The agent can install packages, change runtime versions, or modify your system. Use a dev container to keep these changes off your host. This guide will be replaced once the Copilot CLI ships built-in sandboxing.

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) running
- [Dev Container CLI](https://github.com/devcontainers/cli): `npm install -g @devcontainers/cli`

## Quick Start

```sh
# Start a dev container from the target repo, mounting the benchmarks project in
devcontainer up \
  --workspace-folder <target-repo-path> \
  --config <path-to-devcontainer.json> \
  --mount "type=bind,source=<benchmarks-project-path>,target=/benchmarks"

# Install .NET if the target repo's devcontainer doesn't include it
devcontainer exec --workspace-folder <target-repo-path> \
  bash -c 'curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0'

# Run the benchmarks
devcontainer exec --workspace-folder <target-repo-path> \
  bash -c 'export PATH=$HOME/.dotnet:$PATH && dotnet run --project /benchmarks -- run <scenario-name> --cleanup never'
```

Everything the agent does — package installs, runtime changes, shell commands — stays in the container.
