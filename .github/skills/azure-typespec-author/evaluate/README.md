# Azure TypeSpec Author Skill Evaluations

This directory contains [Vally](https://aka.ms/vally) evaluation cases for the
`azure-typespec-author` skill.

## Supported Local Environment

Use Vally 0.19.0.

Vally 0.19.0 can run on native Windows when its native dependencies install successfully. The
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
npm install --global @microsoft/vally-cli@0.19.0
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
npm install --global @microsoft/vally-cli@0.19.0
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

### 2. Prepare MCP binaries, fixtures, and skills

Run from the `evaluate` directory.

#### Native Windows

```powershell
node scripts\setup-environment.ts --mcp-kind live | Invoke-Expression
$env:EVALUATE_USE_HOST_COPILOT_HOME = "1"
```

#### Linux or WSL2

Prepare the live MCP:

```bash
eval "$(node scripts/setup-environment.ts --mcp-kind live)"
```

The setup command:

1. Changes eval files to `agent_environment: azsdk-mcp-local`.
2. Builds live and mock MCP binaries from this tools checkout under `artifacts/mcp`.
3. Sparse-clones azure-rest-api-specs under `artifacts/azure-rest-api-specs`.
4. Installs the pnpm version pinned by azure-rest-api-specs.
5. Generates the untracked Microsoft.Widget fixture package and installs it with pnpm.
6. Installs the repository-pinned Vally toolchain and exports `AZSDK_EVAL_REPO_ROOT`,
   `AZSDK_EVAL_MCP_ROOT`, and `FIXTURE_NODE_MODULES`.
7. Prepends the local Vally and fixture `node_modules/.bin` directories to PATH.

Use `--mcp-root <path>` to reuse prebuilt MCP artifacts instead of building them.
Use `--spec-root <path>` to generate fixtures from an existing specs checkout without
cloning or updating it. Dependency versions and Copilot instructions come from that
separate specs checkout; only the generated Widget package is installed, not the
tools repository or the full specs workspace.

`.vally.yaml` declares the authoring and shared naming skills, plus the
assessment/generation competitors used by routing-boundary cases. Vally mounts
these inside each trial so reference reads use accessible workspace paths.
Environment setup removes evaluator content from the mounted copies before the
agent starts; only `SKILL.md` and references remain.

CI uses the common shard runner and the pinned Vally version in
`eng/common/scripts/eval/package.json`. Forced, trigger and no-skill selection
is passed through a Vally argument array (`--tag mode=...`, `--runs N`).
The local pre-eval hook prepares the disposable CI checkout's `.vally.yaml` before
Vally reloads it from each selected eval's directory. It mounts absolute paths to
staged definitions and references, without evaluator content or installed fixture
dependencies, and clears explicit skill mounts for no-skill runs.
It also resolves `AZSDK_EVAL_MCP_KIND` to the selected prebuilt MCP DLL and writes
an absolute `dotnet` command into the pipeline environment. Vally starts that
binary directly, without the Node launcher used by local environments. An invalid
MCP kind or missing binary fails preparation before Vally starts. The local
environments and KB backend settings are unchanged.
This setup modifies the CI checkout; do not invoke `--prepare` on a developer's
working configuration unless those generated changes are intended.
When `SkillBranch` predates shared naming, setup omits the absent naming dependency
only if the selected authoring skill does not require it. A missing required
dependency remains an error.

All three modes use common authentication, standard verdicts, unmodified JUnit,
and result collection with latest-attempt/completeness checks. `Runs` means repeated
trials, not conversation turns. Common Summary already shows successful and total
runs for failing scenarios, such as `1/2 runs passed`; at least one successful
trial does not override the standard verdict. No supplementary report or local
post-eval hook is needed.
The pre-eval hook calls `pipeline/typespec-author-prepare.ts --prepare` for setup.
Pipeline globs select only files containing the requested mode. Vally retains
zero-match explicit eval files in its summary, and the common verdict requires
executed stimuli. A regression checks that each mode's globs cover exactly its
matching files; update those local lists when adding cases. Trigger-only naming
cases and turns-only cases are not included in forced/no-skill shards.

Naming cases follow the existing flat, numbered `evals/NNNNNN.eval.yaml` layout.
Cases `007001` through `007008` compare the same task and output/file assertions
across trigger, forced, and no-skill modes. No-skill graders omit skill invocation
and bundled-reference reads. Trigger continuations may dispatch directly to naming;
forced cases still require the authoring handoff. Opt-out and documentation-only
continuations must not invoke naming. Output and file assertions are unchanged
across modes.
Cases `007009` through `007014` are trigger-only routing checks; they do not
measure baseline task quality.

On a managed device that blocks `registry.npmjs.org`, set the approved feed before setup:

```bash
export npm_config_registry="https://packagefeedproxy.microsoft.io/npm/"
eval "$(node scripts/setup-environment.ts --mcp-kind live)"
```

Verify the prepared environment:

```bash
pnpm --version
tsp --version
dotnet "$AZSDK_EVAL_REPO_ROOT/artifacts/mcp/cli/azsdk.dll" --help

pushd fixtures/Microsoft.Widget/Widget
node ../../../scripts/check-node-dependencies.ts packages
popd
```

`tsp --version` must match the generated package under
`fixtures/Microsoft.Widget/Widget`, not an older global installation.

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
  --eval-spec evals\001001.eval.yaml `
  --tag mode=forced `
  --workers 1 `
  --output-dir .\result-001001 `
  --workspace .\debug-001001 `
  --verbose
```

Linux or WSL2:

```bash
vally eval \
  --eval-spec evals/001001.eval.yaml \
  --tag mode=forced \
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
  --workers 1 \
  --output-dir ./result-forced \
  --workspace ./debug-forced \
  --verbose
```

Useful suites:

| Suite                         | Coverage                                                           |
| ----------------------------- | ------------------------------------------------------------------ |
| `forced`                      | All forced-mode cases                                              |
| `trigger`                     | All skill-trigger cases                                            |
| `naming-forced`               | Optional client configuration planning and customization ownership |
| `naming-trigger`              | Authoring and naming-skill routing boundaries                      |
| `naming-no-skill`             | Naming task quality without installed skills                       |
| `versioning-forced`           | Versioning forced cases                                            |
| `armtemplate-forced`          | ARM forced cases                                                   |
| `longrunningoperation-forced` | LRO forced cases                                                   |
| `decorators-forced`           | Decorator forced cases                                             |
| `warning-forced`              | Warning forced cases                                               |
| `dataplane-forced`            | Data-plane forced cases                                            |

The same domain names are available with `-trigger` and `-no-skill` suffixes.

### 5. Run trigger mode with live MCP

Use the live environment and configured skills prepared above; do not switch
trigger runs to the mock MCP:

```bash
export EVALUATE_USE_HOST_COPILOT_HOME=1
```

Run the trigger suite:

```bash
vally eval \
  --suite trigger \
  --workers 1 \
  --output-dir ./result-trigger \
  --workspace ./debug-trigger \
  --verbose
```

Setup changes eval files to `agent_environment: azsdk-mcp-local`. Do not commit that local override;
restore `agent_environment: azsdk-mcp` before committing.

Use `--mcp-kind mock` only for isolated mock-server development. The benchmark's forced and trigger
tracks default to the live MCP so they exercise the production tool surface.

### 6. Run a no-skill baseline

```bash
(
  set -e
  repo_root="$(git rev-parse --show-toplevel)"
  backup="$(mktemp -d)"
  cp .vally.yaml "$backup/.vally.yaml"
  trap 'cp "$backup/.vally.yaml" .vally.yaml; rm "$backup/.vally.yaml"; rmdir "$backup"' EXIT
  node --experimental-strip-types pipeline/typespec-author-prepare.ts \
    --prepare --eval-root "$PWD" --mode no-skill --skill-root "$backup/skills"
  node "$repo_root/eng/common/scripts/eval/node_modules/@microsoft/vally-cli/dist/index.js" eval \
    --tag mode=no-skill --workers 1 --junit --threshold 0.8 --output-dir ./result-no-skill
)
```

Prepare the configuration for baselines: Vally's explicit `environment.skills` takes
precedence over `--skill-dir`, so pointing `--skill-dir` at an empty directory alone
does not remove configured skills. The subshell restores the original `.vally.yaml`
on exit. This uses the pinned Vally installation from `eng/common/scripts/eval`;
run `npm ci` there first.

## Manual Fixture Setup

Normally `setup-environment.ts` performs these steps. To debug pnpm separately:

```bash
node scripts/setup-fixture-files.ts
node scripts/install-pnpm.ts
pnpm --version
pnpm install --dir fixtures/Microsoft.Widget/Widget

export FIXTURE_NODE_MODULES="$PWD/fixtures/Microsoft.Widget/Widget/node_modules"
export PATH="$FIXTURE_NODE_MODULES/.bin:$PATH"
```

`pipeline/typespec-author-prepare.test.ts` generates fixture inputs in a temporary
directory from a synthetic specs catalog. It needs the repository-pinned Vally
dependencies, but no specs clone, fixture dependency installation, MCP build, or login.

## Results and Troubleshooting

Each run writes:

- `results.jsonl`: complete trial and grader data
- `eval-results.md`: readable pass/fail summary
- `azure-typespec-author-eval/**/events.jsonl`: executor events when session logs are kept

Forced and trigger cases primarily use declarative file, compile, prompt, and skill-invocation
graders. Only cases 001005 through 001008 retain tool-call assertions in those modes for
validation and knowledge-route behavior. The bounded `007xxx` naming cases also
check reference reads and the no-MCP/direct-edit boundary.
No-skill cases retain their `tsp compile .` graders for edit scenarios; planning-only and read-only
naming cases do not require compilation.

| Symptom                                                  | Resolution                                                                           |
| -------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| Native Vally installation fails                          | Install the Visual Studio C++ workload or use WSL2                                   |
| WSL resolves Vally from `/mnt/c`                         | Install Vally and Node inside WSL; remove Windows paths from the WSL PATH            |
| Missing `Microsoft.AspNetCore.App 8.x`                   | Install `aspnetcore-runtime-8.0` inside WSL                                          |
| `sessionFs.setProvider`                                  | Run with `--workers 1`                                                               |
| `Session filesystem path escapes root` on native Windows | Use Linux or WSL2 for Vally execution; lint and fixture tests can still run natively |
| `require is not defined in ES module scope`              | Ensure evals use `check-node-dependencies.ts`                                        |
| Compiler reports TypeSpec 1.8.0                          | Rerun setup through `eval`; verify fixture `.bin` is first on PATH                   |
| pnpm symlink returns `EPERM`                             | Move the checkout from `/mnt/c` or `/mnt/d` to the WSL filesystem                    |
| MCP initialize connection closes                         | Run the MCP DLL directly and inspect its missing Linux runtime message               |

## Environments

| Environment            | Use                                                          |
| ---------------------- | ------------------------------------------------------------ |
| `azsdk-mcp`            | CI pipeline; selects live/mock through `AZSDK_EVAL_MCP_KIND` |
| `azsdk-mcp-local`      | Local live MCP under Linux/WSL                               |
| `azsdk-mcp-mock-local` | Local mock MCP under Linux/WSL                               |

Local setup rewrites only the root `agent_environment:` entry in each eval file. Restore these entries to
`azsdk-mcp` before committing.

## Pipeline

The manual benchmark pipeline is `eng/pipelines/azure-typespec-author-benchmark.yml`.
Each build selects forced or trigger mode and uses the common Prepare/Eval/Summary stage template.
It builds the selected MCP, prepares skills and fixtures for that mode, generates an
area-based matrix, and produces one summary and dashboard bundle. Run separate builds
to compare modes; this pipeline no longer runs forced and trigger tracks together.

| Parameter                 | Purpose                                                                                    |
| ------------------------- | ------------------------------------------------------------------------------------------ |
| `Mode`                    | `forced` (default) or `trigger`                                                            |
| `SkillBranch`             | Skill source branch to overlay                                                             |
| `McpKind`                 | `live` or `mock`                                                                           |
| `Runs`                    | Trials per stimulus                                                                        |
| `publishDashboardResults` | Publish trusted main results; disabling it retains the bundle without added storage egress |

Publication uses the existing common trust policy: only approved main-branch runs
can upload, and the pipeline owner must configure `EvalStorageAccountName`,
`EvalDashboardRefreshUrl` and `EvalDashboardResource`. Eval authentication and dashboard
publication use separate service connections. Feature-branch runs retain the bundle
as a pipeline artifact but do not upload. Dashboard grouping/comparison by mode still
requires verification in the reader.

The no-skill baseline runs separately through
`eng/pipelines/azure-typespec-author-benchmark-no-skill.yml`; it is not a mode of the
main benchmark pipeline.

Both this pipeline and `eng/pipelines/azure-typespec-author-benchmark-no-skill.yml`
pass the fixed value `azureSubscription: azuresdkqabot-dev` to the common stage
template, which forwards it through the job template to the invoke step.
Other deployments update this value in their repo-owned pipeline YAML to an
authorized service connection. No service-connection run parameter or Azure DevOps
UI variable is required. Common templates default to an empty connection and retain no
deployment-specific connection fallback. An invalid connection fails Azure task
validation for live runs; an empty or whitespace-only connection produces an explicit
configuration error. Mock runs do not use a service connection.
Authentication and Vally invocation remain in common; the KB backend configuration
in `.vally.yaml` is unchanged.

Pipeline eval files must use `environment: azsdk-mcp`.
