# Azure.Sdk.Tools.Mock

A mock MCP server that exposes the same tools as the real `Azure.Sdk.Tools.Cli` but returns canned responses instead of executing real logic. Useful for benchmarking, evaluation, and integration testing without requiring live Azure services or dependencies.

## How It Works

The mock server reuses the real CLI's tool definitions (`SharedOptions.ToolsList`) via a project reference. At startup it:

1. **Reflects** over all tool classes to find methods marked with `[McpServerTool]`
2. **Creates** real `McpServerTool` instances to capture full metadata (name, description, input schema)
3. **Wraps** each tool in a `MockMcpServerTool` that intercepts all calls and routes them through a `MockToolFactory`

When a tool is called:
- If a custom `IMockToolHandler` exists for that tool name → the handler produces the response
- Otherwise → a default success response is returned:
  ```json
  { "message": "Success" }
  ```

The MCP client sees the exact same tool list and schemas as the real CLI — only the responses differ.

## Running the Mock Server

### From the command line

```bash
dotnet run --project tools/azsdk-cli/Azure.Sdk.Tools.Mock
```

### As an MCP server in VS Code

Add to `.vscode/mcp.json`:

```json
{
  "servers": {
    "azure-sdk-mock": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "./tools/azsdk-cli/Azure.Sdk.Tools.Mock"]
    }
  }
}
```

## Adding a Custom Handler

To provide a tool-specific mock response instead of the default, create a class that implements `IMockToolHandler` in the `Handlers/` directory.

```csharp
// Handlers/MyToolHandler.cs
namespace Azure.Sdk.Tools.Mock.Handlers;

public class MyToolHandler : IMockToolHandler
{
    // Must match the [McpServerTool(Name = "...")] value from the real tool
    public string ToolName => "azsdk_my_tool";

    public object Handle(Dictionary<string, object?>? arguments)
    {
        return new
        {
            message = "Custom mock response",
            operation_status = "Succeeded"
        };
    }
}
```

## Argument-Based Switching

Handlers receive the arguments passed by the MCP client. You can switch on these arguments to return different responses, making the mock flexible enough to simulate success, failure, and edge cases from a single handler.

### Example: `HelloWorldHandler`

The `azsdk_hello_world` handler demonstrates this pattern:

```csharp
public class HelloWorldHandler : IMockToolHandler
{
    public string ToolName => "azsdk_hello_world";

    public object Handle(Dictionary<string, object?>? arguments)
    {
        var message = arguments?.GetValueOrDefault("message")?.ToString() ?? "world";

        return message.ToLowerInvariant() switch
        {
            "error" => new
            {
                message = "Simulated error for testing",
                operation_status = "Failed",
                error_code = "MOCK_ERROR"
            },
            "slow" => new
            {
                message = "Simulated slow response",
                operation_status = "Succeeded",
                duration = 30000
            },
            _ => new
            {
                message = $"Hello, {message}!",
                operation_status = "Succeeded",
                duration = 1
            }
        };
    }
}
```

This lets callers control the mock behavior through input:
- `{"message": "error"}` → simulates a failure
- `{"message": "slow"}` → simulates a slow operation
- `{"message": "Alice"}` → normal success response

Use this pattern in any handler to test how your integration handles different scenarios without changing the mock server code.

## Keeping the Mock in Sync with the Live MCP Server

The mock reuses the live CLI's tool definitions (`SharedOptions.ToolsList`), so the *set* of advertised tools is always identical. What can drift is which tools have a hand-written `IMockToolHandler`. Tools without a handler fall back to the generic `{"message":"Success"}` default — fine for routing tests but useless for scenarios that chain calls together (e.g. consume an ID returned by a previous tool).

When you add or rename an MCP tool in `Azure.Sdk.Tools.Cli`, add a matching handler under `Handlers/<Domain>/`:

1. Look up the live tool's response type under `tools/azsdk-cli/Azure.Sdk.Tools.Cli/Tools/`. The return type is usually a typed `CommandResponse` in `Azure.Sdk.Tools.Cli.Models.Responses.*`.
2. Create a new file under `Handlers/<Domain>/` (e.g., `Handlers/Pipeline/MyToolHandler.cs`).
3. Implement `IMockToolHandler`. Set `ToolName` to the exact `[McpServerTool(Name = "…")]` value from the real tool.
4. Return an instance of the same response type the real tool returns, populated with realistic sample data. For scenarios that need to exercise multiple branches, switch on `arguments` (see `HelloWorldHandler` above).

## Breaking-Change Detector Fixtures

`PackageDetectBreakingChangeHandler` supplies typed common responses for
`azsdk_package_detect_breaking_change`. Its input schema still comes from the
real CLI; the handler never runs a native SDK detector, reads a catalog, or calls
an LLM classifier.

| Package directory at the end of `packagePath` | Fixture |
| --- | --- |
| `Azure.ResourceManager.Contoso` | .NET management, classified manual mitigation |
| `Azure.Contoso.Widget` | .NET data plane, classified manual mitigation |
| `azure-resourcemanager-contoso` | Java management, legacy classification without mitigation enum |

Default responses retain package metadata, removal and addition Markdown,
classified breaks with `mitigationStrategy`, and .NET-native `details` for .NET
packages. The common details contract is language-neutral, with
`DotnetSdkChangeDetails` providing the .NET view. Java does not fabricate
.NET-native details or mitigation strategies. `changesOnly: true` returns
the same raw evidence without classification. These are synthetic fixtures,
not comparisons against files at the supplied path.

Place one directory segment below before the package directory to select a
scenario, for example `C:\mock-catalog-error\Azure.Contoso.Widget`:

| Segment | `breaking_change_status` | Response |
| --- | --- | --- |
| None | `classified` | Classified breaks; `detected` when `changesOnly: true` |
| `mock-no-breaks` | `clean` | Successful additive-only comparison |
| `mock-no-baseline` | `inconclusive` | Compatibility not evaluated, with an explicit no-GA limitation |
| `mock-missing-config` | `blocked` | Required detector configuration is absent |
| `mock-missing-artifacts` / `mock-stale-artifacts` | `failed` | Explicit failure, not a clean comparison |
| `mock-classifier-error` / `mock-catalog-error` | `failed` | Classification failure retaining raw changes/details; `detected` when `changesOnly: true` |

The additive `breaking_change_status` is independent of `operation_status`.
Inconclusive results preserve a successful operation status but must not be
treated as compatibility passes. Blocked/failed results contain errors.

Unknown packages and invalid/conflicting inputs fail explicitly. Classified
`localSdkChangeJsonFilePath` replay reports that this mock capability is not
implemented; the argument is ignored for `changesOnly`, as in production.
`tspConfigPath` is accepted but not read by the fixtures.

Other generation/build/check/test handlers remain canned successes.
`azsdk_customized_code_update` is not a stateful or scope-aware SDK editor.
These are mock coverage limits, not missing production detector or mitigation
behavior.

### Focused Offline Tests and Build

From the repository root:

```powershell
dotnet test tools\azsdk-cli\Azure.Sdk.Tools.Mock\Tests\Azure.Sdk.Tools.Mock.Tests.csproj --nologo -p:CopilotSkipCliDownload=true
dotnet build tools\azsdk-cli\Azure.Sdk.Tools.Mock -c Debug -o artifacts\mcp\mock --nologo -p:CopilotSkipCliDownload=true
```

The nested NUnit project uses the existing CLI test package versions and is
excluded from the server's compile/content items. It verifies fixture contracts,
raw/error evidence, metadata, JSON inputs, state isolation, and equality with the
real reflected MCP schema. It is included in `Azure.Sdk.Tools.Cli.sln`, and the
existing azsdk-cli CI test jobs run that solution, including these mock tests.

`CopilotSkipCliDownload` is the SDK's supported build option for avoiding an
unused native Copilot CLI download: this mock server does not execute the real
CLI's agent.

The shared breaking-change skill and its evals are deferred to
[PR #16634](https://github.com/Azure/azure-sdk-tools/pull/16634). The fixtures
above remain available to consumers of the common detector tool; the focused
unit tests do not require a model credential or live Azure SDK environment.
