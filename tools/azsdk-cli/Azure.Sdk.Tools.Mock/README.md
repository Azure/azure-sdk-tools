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
  { "message": "Mock response for <tool_name>", "operation_status": "Succeeded" }
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

The mock reuses the live CLI's tool definitions, so the *set* of advertised tools is always identical. What can drift is which tools have a hand-written `IMockToolHandler`. Tools without a handler fall back to the generic default response — fine for noise, but it hides routing / arg regressions when a scenario actually depends on that tool returning a realistic shape.

Use the inventory script to audit live-vs-mock parity:

```powershell
pwsh eng/scripts/Get-McpToolInventory.ps1
```

It produces three buckets:

- **both** — live tool with a hand-written handler. No action.
- **live-only** — live tool that falls back to the default response. Add a handler.
- **mock-only** — handler for a tool that no longer exists on the live server. Rename or delete the stale handler.

CI runs the same script with `-CheckOnly`:

```powershell
pwsh eng/scripts/Get-McpToolInventory.ps1 -CheckOnly
```

`-CheckOnly` exits non-zero when either bucket is non-empty.

### Workflow when the script flags a gap

1. Look up the live tool's response type. Tool method signatures live under `tools/azsdk-cli/Azure.Sdk.Tools.Cli/Tools/`. The return type is usually a typed `CommandResponse` in `Azure.Sdk.Tools.Cli.Models.Responses.*`.
2. Add a new file under `Handlers/<Domain>/` (e.g., `Handlers/Pipeline/MyToolHandler.cs`).
3. Implement `IMockToolHandler`. Set `ToolName` to the exact `[McpServerTool(Name = "…")]` value from the real tool.
4. Return an instance of the same response type the real tool returns, populated with realistic sample data. For scenarios that need to exercise multiple branches, switch on `arguments` (see `HelloWorldHandler` above).
5. Re-run the inventory script to confirm the tool moved from **live-only** to **both**.
