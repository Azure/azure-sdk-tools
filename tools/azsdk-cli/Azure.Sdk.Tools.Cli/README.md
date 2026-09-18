# `Azure SDK CLI`

This is the SDK developer experience CLI and MCP server. It is intended to:
  - Provide hooks into language automation tasks for LLMs and command-line users
  - Encapsulate manual work in the `azure sdk` release process.
  - Improve developer efficiency

## Table of Contents

- [`Azure SDK CLI`](#azure-sdk-cli)
  - [Table of Contents](#table-of-contents)
  - [Prerequisites](#prerequisites)
  - [Quick Start](#quick-start)
  - [Usage Modes](#usage-modes)
    - [1. MCP Server Mode](#1-mcp-server-mode)
    - [2. Standalone CLI Mode](#2-standalone-cli-mode)
  - [Telemetry Configuration](#telemetry-configuration)
  - [Configure Azure Knowledge base service](#configure-azure-knowledge-base-service)

## Prerequisites

- .NET 8.0 (`winget install Microsoft.DotNet.SDK.8`)
- Visual Studio Code (`winget install Microsoft.VisualStudioCode`)
  - [Copilot Extension](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot)
  - [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) (optional)

## Quick Start

1. **Open VS Code** in the `azure-sdk-tools` directory

2. **Start the MCP server via settings** (optional - Copilot will auto-start if needed):
   - In `.vscode/mcp.json`, click the Start button below "servers"

   ![Screenshot showing the MCP Start button in VS Code's mcp.json file](/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Images/MCP-Start.png)

3. **Alternatively, start the MCP server via command palette**:
   - Enter ctrl-shift-p (or cmd-shift-p for mac)
   - Type `MCP: List Servers`
   - Select `azure-sdk-mcp` and press enter
   - Select `Start Server` and press enter

4. **Test the connection** by prompting Copilot (`Ctrl + Shift + I`) with any of our recommended prompts from the [documentation](https://aka.ms/azsdk/agent#agentic-workflow-scenarios)

## Usage Modes

### 1. MCP Server Mode

The `<repo root>/.vscode/mcp.json` config file can be updated to change which version of the MCP server is used (local or release).

**Using dotnet tool**

This config should already be checked into azure-sdk-tools main. Using dotnet tool mode means the mcp server will run with any changes
made to the local branch.

```jsonc
{
  "servers": {
    "azure-sdk-mcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/tools/azsdk-cli/Azure.Sdk.Tools.Cli",
        "--configuration",
        "Debug",
        "--",
        "start"
      ]
    }
  }
}
```

**Using standalone executable from github releases**

Run the below command to update the `.vscode/mcp.json` file with a reference to the upstream release. Do not check this change in.

```
<tools repo root>/eng/common/mcp/azure-sdk-mcp.ps1 -UpdateVsCodeConfig
```

### 2. Standalone CLI Mode

Run directly as a command-line tool:

```bash
dotnet run --project Azure.Sdk.Tools.Cli -- --help
dotnet run --project Azure.Sdk.Tools.Cli -- example hello-world foobar
dotnet run --project Azure.Sdk.Tools.Cli -- release-plan get --work-item-id YOUR_WORK_ITEM_ID
```

In either case, the _same_ code will be invoked to get both results.

This server is intended to run in **local mcp mode only** and will utilize your environment cached settings to communicate where authentication is necessary.

## Bounded custom-code updates

`azsdk tsp client customized-update` and MCP tool `azsdk_customized_code_update` share the same customization engine. For `CustomCode` scope, `--max-attempts` / `maxAttempts` controls how many patch attempts the engine may make in one conversation (1..10, default 1):

```text
azsdk tsp client customized-update --package-path <absolute-package-path> --edit-scope CustomCode --customization-request "Repair the custom-code build failures" --max-attempts 3 --output json
```

The package path is the SDK package root, not its `src` directory. A local TypeSpec checkout is optional: omitting `--tsp-project-path` regenerates from the unchanged inputs in `tsp-location.yaml`. An explicit local project must belong to a separate Git repository and remain unchanged throughout the update.

The engine validates the initial source, classifies the requested customization, and retains hypotheses, actual diffs, and diagnostics across patch attempts. A green build does not skip a requested customization such as an API rename. Generation and build run in code, not at the agent's discretion. .NET and Java prepare/regenerate before building; .NET plugin preparation must succeed. JavaScript and Python keep their custom-code build/check behavior without adding regeneration.

If the repository-local `tsp-client` executable is missing, generation first runs `npm ci --prefix <repo>/eng/common/tsp-client` using the existing package manifest and lockfile. Installation must produce the local executable; generation never infers an npm package named `tsp-client`. Node.js/npm and network access to the manifest's package registries are required. An already-installed local executable is reused.

Baseline validation does not consume an attempt. Each patch turn does, even when it makes no effective change. The session stops on success, no progress, a return to a previously failed state, an out-of-scope change, a required-stage failure, cancellation, or the attempt limit. A fixed 30-minute total deadline applies across the session. `All` and `SpecInputs` preserve their existing single-attempt behavior; multiple attempts currently require `CustomCode`.

The response preserves existing properties and adds `buildValidated` plus a `repair` object for `CustomCode`:

| Field | Meaning |
| --- | --- |
| `repair.schemaVersion` | Contract version, currently `1`. |
| `repair.terminalReason` | `already_green`, `repaired`, or a specific failure such as `no_progress`, `scope_violation`, `preparation_failed`, `generation_failed`, `attempt_limit`, `timed_out`, or `cancelled`. |
| `repair.repairKind` | `none`, `generation_only`, or `custom_code`. Successful baseline generation changes are a repair even with zero patch attempts. |
| `repair.input` / `repair.finalState` | Initial/final HEAD, full Git-publishable source trees, package identity, pinned-input hashes, and actual changed files. Added/untracked nonignored outputs and deletions are included. |
| `repair.validation` | Actual deterministic validation result, its source tree, and the required stage IDs, including the build. |
| `repair.stages` / `repair.attempts` | Stage outcomes, retained hypotheses, source states, diagnostics, and paths to actual patch/validation diffs. |
| `repair.artifactsPath` | External directory containing atomic checkpoints, logs, and diffs. |

Artifacts are written to `<OS temporary directory>/azsdk-repair/<sessionId>`, never into the source tree. They may contain proprietary source and diagnostics; retain them according to your organization's policy and remove the specific session directory when no longer needed. They survive ordinary failure/cancellation, subject to OS cleanup; resuming another process from them is not supported. A forced termination may leave only a partial checkpoint.

The new repair contract's JSON field names and non-null values match across CLI and MCP. The MCP SDK omits null object properties, while CLI JSON may include them; consumers must accept either for optional fields. Legacy `appliedPatches` items retain their existing CLI PascalCase versus MCP camelCase property names; use `repair.finalState.changedFiles` for complete source changes. Failures include a string `response_error`; actionable spec/manual guidance is in the string arrays `specChangeRequired` and `next_steps`, respectively.

**Publication must not trust an agent's summary or a mutable JSON file alone.** A successful response requires the final build's source tree to equal the final source tree. A publishing workflow must independently check the complete changed-file allowlist, expected PR HEAD, pinned inputs, and candidate tree. If invocation/result capture is not controlled by a trusted runner, independently prepare, regenerate, and build the exact candidate before publishing. Keep publication credentials outside the repair agent. These receipts are not signed attestations or a sandbox against arbitrary repository build scripts.

## Telemetry Configuration
Telemetry collection is on by default.

To opt out, set the environment variable `AZSDKTOOLS_COLLECT_TELEMETRY` to false in your environment.

If you need to direct telemetry to an alternate Application Insights instance (for local testing or private collection), set one of the following environment variables in your environment or in your hosting configuration:

- `AZSDKTOOLS_APPLICATIONINSIGHTS_CONNECTION_STRING`: the full Application Insights connection string.

## Configure Azure Knowledge base service

The `TypeSpecAuthoringTool` uses the Azure Knowledge Base service and is configured with a default service by default.

If you want to use a different Azure Knowledge Base service instead of the default one, set the `AZURE_SDK_KB_ENDPOINT` environment variable to specify the endpoint.

If the service is deployed in Azure with built-in Microsoft authentication enabled, you must also set the `AZURE_SDK_KB_CLIENT_ID` and `AZURE_SDK_KB_SCOPE`environment variables. These variables should reference the application (client) ID of the service and the authentication scope. You can find both the endpoint and the client ID in the Azure SDK QA backend service configuration blob.
