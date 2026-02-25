# Copilot SDK Migration Quick Start

Quick reference for migrating from Microagents to Copilot SDK.

## Key Changes

| Microagents | Copilot SDK |
|-------------|-------------|
| `IMicroagentHostService` | `ICopilotAgentRunner` |
| `Microagent<T>` | `CopilotAgent<T>` |
| `RunAgentToCompletion()` | `RunAsync()` |
| `MaxToolCalls` | `MaxIterations` |
| `AgentTool<TIn, TOut>` | `AIFunctionFactory.Create()` |
| Model: `"gpt-4.1"` | Model: `"claude-sonnet-4.5"` |

## Imports

```csharp
// Remove
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;

// Add
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Microsoft.Extensions.AI;
using System.ComponentModel;
```

## Constructor

```csharp
// Before
public class MyTool(IMicroagentHostService microagentHostService) : MCPTool

// After
public class MyTool(ICopilotAgentRunner copilotAgentRunner) : MCPTool
```

## Agent Definition

```csharp
// Before
var microagent = new Microagent<MyResult>()
{
    Instructions = prompt,
    MaxToolCalls = 50,
    Model = "gpt-4.1",
    Tools = [tool1, tool2],
    ValidateResult = async result => { /* ... */ }
};
var result = await microagentHostService.RunAgentToCompletion(microagent, ct);

// After
var agent = new CopilotAgent<MyResult>()
{
    Instructions = prompt,
    MaxIterations = 50,
    Model = "claude-sonnet-4.5",
    Tools = [tool1, tool2],
    ValidateResult = async result => { /* ... */ }
};
var result = await copilotAgentRunner.RunAsync(agent, ct);
```

## Tool Conversion

### Simple Tool

```csharp
// Before
var tool = AgentTool<MyInput, MyOutput>.FromFunc(
    "tool_name",
    "Tool description",
    async (input) => await DoWork(input));

// After
var tool = AIFunctionFactory.Create(
    async ([Description("Input description")] MyInput input) =>
        await DoWork(input),
    "tool_name",
    "Tool description");
```

### ReadFileTool

```csharp
// Before
new ReadFileTool(baseDir)

// After
FileTools.CreateReadFileTool(baseDir)
```

### WriteFileTool

```csharp
// Before
new WriteFileTool(baseDir)

// After
FileTools.CreateWriteFileTool(baseDir)
```

### ListFilesTool

```csharp
// Before
new ListFilesTool(baseDir)

// After
FileTools.CreateListFilesTool(baseDir)
```

## Test Updates

```csharp
// Before
private Mock<IMicroagentHostService> mockService;
mockService.Setup(m => m.RunAgentToCompletion(
    It.IsAny<Microagent<MyResult>>(), 
    It.IsAny<CancellationToken>()))
    .ReturnsAsync(expectedResult);

// After
private Mock<ICopilotAgentRunner> mockRunner;
mockRunner.Setup(m => m.RunAsync(
    It.IsAny<CopilotAgent<MyResult>>(), 
    It.IsAny<CancellationToken>()))
    .ReturnsAsync(expectedResult);
```

## Checklist

- [ ] Update imports
- [ ] Change `IMicroagentHostService` → `ICopilotAgentRunner`
- [ ] Change `Microagent<T>` → `CopilotAgent<T>`
- [ ] Change `MaxToolCalls` → `MaxIterations`
- [ ] Change `RunAgentToCompletion()` → `RunAsync()`
- [ ] Convert tools to `AIFunctionFactory.Create()`
- [ ] Update model name to `"claude-sonnet-4.5"`
- [ ] Update tests
- [ ] Build and verify
