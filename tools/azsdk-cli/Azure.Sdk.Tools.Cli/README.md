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

## Release-plan status updates

`azsdk release-plan update-release-status` requires explicit correlation before it writes to Azure DevOps:

- `--release-plan-id`: the release-plan ID associated with this package release, not an interchangeable Azure DevOps work item ID.
- `--language` and `--package-name`: identify exactly one SDK entry within that plan. Language aliases are normalized; package names must match exactly.
- `--api-version`: one explicit spec API version from the package being released, matching the plan's saved API version. It is not the SDK package version.

Without a release-plan ID, the command reports a no-op without looking up plans. This is normal for an independent SDK-only release. An unresolved or duplicate ID, missing/ambiguous API version, or mismatched language/package/API produces an error and no writes. There is no first-result, merged-PR, or release-type fallback.

Package version and release pipeline URL remain optional result metadata. When an SDK release type or SDK PR URL is supplied, it must also match the identified plan/SDK entry. An already released SDK is not overwritten; matching retries are no-ops, including after the plan finishes, and a conflicting recorded version is rejected. Status and completion writes check the parent work item's revision; completion rereads all required language statuses and approved exclusions first. A revision conflict is reported rather than retried against changed data.

The shared publication script reads `ReleasePlanId` and a scalar `ApiVersion` from **each package's build-produced package-info JSON** and forwards them with the package name and pipeline language. Missing or invalid correlation skips the status update, not the successful publication. No pipeline-wide ID is applied to every package.

**Rollout prerequisite:** package-info producers must supply the API version using [#16868](https://github.com/Azure/azure-sdk-tools/issues/16868) and preserve the release-plan association for the exact SDK build/artifact. This change implements the receiving command and publication adapter, not that cross-repository producer work. Do not persist an ID as a permanent package setting or copy it into an unrelated bug-fix build. The existing auto-release progress caller also omits these inputs and therefore becomes a safe no-op until it is wired to verified per-package correlation. Publish the updated CLI before rolling out the shared script.

These guards do not prove artifact provenance or distinguish two generation attempts with identical inputs and a copied ID. Parent revision checks also do not make API Spec child reads and parent writes a cross-work-item transaction. Historical incorrect dashboard data is not repaired automatically.

## Retained customization repair attempts

`azsdk tsp client customized-update` / `azsdk_customized_code_update` accepts `--max-attempts` / `maxAttempts` (1..10, default 1). Multiple attempts currently require `CustomCode`; `All` and `SpecInputs` retain their single-pass behavior.

```text
azsdk tsp client customized-update --package-path <package-root> --edit-scope CustomCode --customization-request "Repair the custom-code build failures" --max-attempts 3 --output json
```

One command invocation keeps the same Copilot session and feeds actual validation failures back into its existing conversation. Earlier tool calls, edits, and feedback remain available. Repeated outer CLI invocations preserve files but start new conversations. A repair attempt is a patch proposal evaluated by host code, not an individual tool call or an Exit reminder; the existing per-language agent-turn allowance is retained.

After each proposal, the command awaits the existing .NET/Java preparation and regeneration, then builds. JavaScript/Python retain their existing build/check behavior. Preparation fallback policy, pinned `tsp-location.yaml` inputs, and optional local spec handling are unchanged. A green build does not skip a requested semantic customization, and a classifier no-op must still pass an SDK build in custom-code scope. The session stops on success, no additional patches, cancellation, or the attempt limit.

The existing response adds only `attemptsUsed` (default 0). Initial builds do not count; evaluated no-progress proposals do. Failures retain the final actual diagnostics in `buildResult` and `response_error`, the stopping reason/error code, known `appliedPatches`, and existing `specChangeRequired` / `next_steps` guidance for useful PR comments. `success` is not set by the agent's claim.

This feature adds no source receipt, artifact/checkpoint format, strict preparation policy, dependency bootstrap, or publication protocol. Existing repository generation prerequisites still apply. It retains existing tool/path restrictions and does not claim whole-repository change attestation or cross-process conversation resume.

## Telemetry Configuration
Telemetry collection is on by default.

To opt out, set the environment variable `AZSDKTOOLS_COLLECT_TELEMETRY` to false in your environment.

If you need to direct telemetry to an alternate Application Insights instance (for local testing or private collection), set one of the following environment variables in your environment or in your hosting configuration:

- `AZSDKTOOLS_APPLICATIONINSIGHTS_CONNECTION_STRING`: the full Application Insights connection string.

## Configure Azure Knowledge base service

The `TypeSpecAuthoringTool` uses the Azure Knowledge Base service and is configured with a default service by default.

If you want to use a different Azure Knowledge Base service instead of the default one, set the `AZURE_SDK_KB_ENDPOINT` environment variable to specify the endpoint.

If the service is deployed in Azure with built-in Microsoft authentication enabled, you must also set the `AZURE_SDK_KB_CLIENT_ID` and `AZURE_SDK_KB_SCOPE`environment variables. These variables should reference the application (client) ID of the service and the authentication scope. You can find both the endpoint and the client ID in the Azure SDK QA backend service configuration blob.
