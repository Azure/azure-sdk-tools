# Azure TypeSpec Author Skill Evaluations

This directory contains [Vally](https://aka.ms/vally) evaluations for the
`azure-typespec-author` skill.

## Supported Local Environment

Use Vally 0.14.0.

Vally 0.14.0 can run on native Windows when its native dependencies install successfully. The
Windows run must use `--workers 1`, and eval fixtures must link `node_modules` through
`FIXTURE_NODE_MODULES` instead of copying pnpm symlinks.

Linux or WSL2 remains the fallback when Vally's native npm dependencies cannot be installed on
Windows. When using WSL, install the full toolchain inside WSL and keep the repository in its Linux
filesystem rather than `/mnt/c` or `/mnt/d`.

## Local Quick Start

### 1. Install prerequisites

#### Native Windows

From PowerShell:

```powershell
winget install --id Microsoft.DotNet.AspNetCore.8 --exact --source winget `
  --accept-package-agreements --accept-source-agreements
npm install --global @microsoft/vally-cli@0.14.0
```

Vally's `better-sqlite3` dependency may require the Visual Studio **Desktop development with C++**
workload when no prebuilt native binary is available.

Verify:

```powershell
node --version
dotnet --list-runtimes | Select-String "Microsoft.NETCore.App 8|Microsoft.AspNetCore.App 8"
vally --version
az account show --output table
```

#### Linux or WSL2 fallback

Install Linux build tools plus the .NET 8 SDK and ASP.NET Core 8 runtime:

```bash
sudo apt-get update
sudo apt-get install -y build-essential git python3 curl dotnet-sdk-8.0 aspnetcore-runtime-8.0
```

If Ubuntu cannot find the .NET packages, configure Microsoft's package repository for your Ubuntu
version first, then repeat the command.

Install a Linux-native Node.js 24.14.1 or newer. With `nvm` already installed:

```bash
nvm install 24.14.1
nvm use 24.14.1
```

Install Vally inside WSL:

```bash
npm install --global @microsoft/vally-cli@0.14.0
```

Vally contains native dependencies. `build-essential` and `python3` allow npm to build them when a
prebuilt Linux binary is unavailable.

Install and sign in to the Linux Azure CLI when using the live MCP:

```bash
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
az login
```

Under WSL, verify that every executable is Linux-native and that both required .NET 8 frameworks
exist:

```bash
command -v node npm dotnet vally az
node --version
dotnet --version
dotnet --list-runtimes | grep -E 'Microsoft\.NETCore\.App 8|Microsoft\.AspNetCore\.App 8'
vally --version
az account show --output table
```

Expected executable paths start with Linux locations such as `/usr/` or `/home/`, not `/mnt/c/`.
The runtime command must list both `Microsoft.NETCore.App 8.x` and
`Microsoft.AspNetCore.App 8.x`.

### 2. Prepare MCP binaries and fixtures

#### Native Windows

```powershell
node scripts/setup-environment.js --mcp-kind live | Invoke-Expression
$env:EVALUATE_USE_HOST_COPILOT_HOME = "1"
```

#### Linux or WSL2

From the `evaluate/` directory, prepare the live MCP:

```bash
eval "$(node scripts/setup-environment.js --mcp-kind live)"
```

The setup command:

1. Changes eval files to `environment: azsdk-mcp-local`.
2. Builds live and mock MCP binaries under `artifacts/mcp`.
3. Sparse-clones azure-rest-api-specs under `artifacts/azure-rest-api-specs`.
4. Installs the pnpm version pinned by azure-rest-api-specs.
5. Generates the Microsoft.Widget fixture package under `artifacts/` and installs it with pnpm.
6. Exports `AZSDK_EVAL_REPO_ROOT` and `FIXTURE_NODE_MODULES`.
7. Prepends the fixture `node_modules/.bin` to PATH so graders use its TypeSpec compiler.

On a managed device that blocks `registry.npmjs.org`, set the approved feed before setup:

```bash
export npm_config_registry="https://packagefeedproxy.microsoft.io/npm/"
eval "$(node scripts/setup-environment.js --mcp-kind live)"
```

Verify the prepared environment:

```bash
pnpm --version
tsp --version
dotnet "$AZSDK_EVAL_REPO_ROOT/artifacts/mcp/cli/azsdk.dll" --help

pushd fixtures/Microsoft.Widget/Widget
node ../../../scripts/check-node-dependencies.cjs packages
popd
```

`tsp --version` must match the generated fixture package under
`artifacts/typespec-author-eval/Microsoft.Widget/Widget`, not an older global installation.

Allow the executor to reuse the Copilot login from the same WSL environment:

```bash
export EVALUATE_USE_HOST_COPILOT_HOME=1
```

Keep this setting local. CI supplies its own authentication.

### 3. Run one smoke test

Use one worker for local live MCP runs. This avoids concurrent Copilot SDK session-filesystem
provider conflicts.

Native Windows:

```powershell
vally eval `
  --eval-spec evals/001001.eval.yaml `
  --tag mode=forced `
  --skill-dir .. `
  --workers 1 `
  --output-dir ./result-001001 `
  --workspace ./debug-001001 `
  --verbose
```

Linux or WSL2:

```bash
vally eval \
  --eval-spec evals/001001.eval.yaml \
  --tag mode=forced \
  --skill-dir .. \
  --workers 1 \
  --output-dir ./result-001001 \
  --workspace ./debug-001001 \
  --verbose
```

Do not start the full suite until this run produces assistant/tool events and passes the dependency
and TypeSpec compilation graders.

### 4. Run the forced suite

```bash
vally eval \
  --suite forced \
  --skill-dir .. \
  --workers 1 \
  --output-dir ./result-forced \
  --workspace ./debug-forced \
  --verbose
```

Useful suites:

| Suite | Coverage |
|---|---|
| `forced` | All forced-mode cases |
| `trigger` | All skill-trigger cases |
| `versioning-forced` | Versioning forced cases |
| `armtemplate-forced` | ARM forced cases |
| `longrunningoperation-forced` | LRO forced cases |
| `decorators-forced` | Decorator forced cases |
| `warning-forced` | Warning forced cases |
| `dataplane-forced` | Data-plane forced cases |

The same domain names are available with `-trigger` and `-no-skill` suffixes.

### 5. Run trigger mode with mock MCP

Prepare the mock environment:

```bash
eval "$(node scripts/setup-environment.js --mcp-kind mock)"
export EVALUATE_USE_HOST_COPILOT_HOME=1
```

Run the trigger suite:

```bash
vally eval \
  --suite trigger \
  --skill-dir .. \
  --workers 1 \
  --output-dir ./result-trigger \
  --workspace ./debug-trigger \
  --verbose
```

Setup changes eval files to `environment: azsdk-mcp-mock-local`. Do not commit that local override;
restore `environment: azsdk-mcp` before committing.

### 6. Run a no-skill baseline

```bash
mkdir -p ./no-skills

vally eval \
  --suite no-skill \
  --skill-dir ./no-skills \
  --workers 1 \
  --output-dir ./result-no-skill \
  --workspace ./debug-no-skill \
  --verbose
```

## Manual Fixture Setup

Normally `setup-environment.js` performs these steps. To debug pnpm separately:

```bash
node scripts/setup-fixture-files.js
node scripts/install-pnpm.js
pnpm --version
pnpm install --dir ../../../../artifacts/typespec-author-eval/Microsoft.Widget/Widget

export FIXTURE_NODE_MODULES="$PWD/../../../../artifacts/typespec-author-eval/Microsoft.Widget/Widget/node_modules"
export PATH="$FIXTURE_NODE_MODULES/.bin:$PATH"
```

## Results and Troubleshooting

Each run writes:

- `results.jsonl`: complete trial and grader data
- `eval-results.md`: readable pass/fail summary
- `azure-typespec-author-eval/**/events.jsonl`: executor events when session logs are kept

| Symptom | Resolution |
|---|---|
| Native Vally installation fails | Install the Visual Studio C++ workload or use WSL2 |
| WSL resolves Vally from `/mnt/c` | Install Vally and Node inside WSL; remove Windows paths from the WSL PATH |
| Missing `Microsoft.AspNetCore.App 8.x` | Install `aspnetcore-runtime-8.0` inside WSL |
| `sessionFs.setProvider` | Run with `--workers 1` |
| `require is not defined in ES module scope` | Ensure evals use `check-node-dependencies.cjs` |
| Compiler reports TypeSpec 1.8.0 | Rerun setup through `eval`; verify fixture `.bin` is first on PATH |
| pnpm symlink returns `EPERM` | Move the checkout from `/mnt/c` or `/mnt/d` to the WSL filesystem |
| MCP initialize connection closes | Run the MCP DLL directly and inspect its missing Linux runtime message |

## Environments

| Environment | Use |
|---|---|
| `azsdk-mcp` | CI pipeline; selects live/mock through `AZSDK_EVAL_MCP_KIND` |
| `azsdk-mcp-local` | Local live MCP under Linux/WSL |
| `azsdk-mcp-mock-local` | Local mock MCP under Linux/WSL |

Local setup rewrites only the root `environment:` entry in each eval file. Restore these entries to
`azsdk-mcp` before committing.

## Pipeline

The manual benchmark pipeline is `eng/pipelines/azure-typespec-author-benchmark.yml`. It builds the
selected MCP, generates area-based forced and trigger shard matrices, runs each area, publishes
result/debug artifacts, and produces separate summaries.

| Parameter | Purpose |
|---|---|
| `SkillBranch` | Skill source branch to overlay |
| `McpKind` | `live` or `mock` |
| `Runs` | Trials per stimulus |

Pipeline eval files must use `environment: azsdk-mcp`.
