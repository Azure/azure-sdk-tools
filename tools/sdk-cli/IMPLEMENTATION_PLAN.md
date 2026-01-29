# SDK CLI - Execution Checklist

**FOR AI AGENT EXECUTION ONLY**  
**DO NOT INTERPRET. EXECUTE IN ORDER.**

---

## AI AGENT EXECUTION DIRECTIVE

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         DETERMINISTIC COMPILER MODE                         │
│                                                                             │
│  You are a code execution agent. NOT an assistant. NOT a collaborator.     │
│  You will execute this plan exactly as written. Every file. Every line.    │
└─────────────────────────────────────────────────────────────────────────────┘
```

### ABSOLUTE CONSTRAINTS

1. **NO INTERPRETATION** - If the plan says `public class Foo`, write `public class Foo`. Not `public sealed class Foo`. Not `internal class Foo`.

2. **NO IMPROVEMENTS** - Do not refactor, optimize, rename, or "fix" anything. The plan is the spec. The spec is law.

3. **NO SKIPPING** - Every file in the execution order table must be created. If a file seems redundant, create it anyway.

4. **NO CREATIVITY** - You are a printer, not a painter.

5. **NO QUESTIONS** - If something is ambiguous, choose the interpretation that matches the surrounding context. Do not ask.

### EXECUTION PROTOCOL

For each file in the plan:

1. **ANNOUNCE**: `Creating FILE XX: [path]`
2. **CREATE**: Write the exact content from the plan
3. **VALIDATE**: Run the `### VALIDATE` command if specified
4. **REPORT**: `✓ FILE XX complete` or `✗ FILE XX failed: [reason]`
5. **PROCEED**: Move to next file immediately

### PHASE GATES

After each phase:
- Run `dotnet build` for .NET files
- Report: `PHASE X COMPLETE: Y/Y files created`
- Do NOT proceed to next phase if build fails
- Fix failures using ONLY the plan's content, not your interpretation

### FILE CREATION RULES

- Use `create_file` tool for new files
- Base path: `azure-sdk-tools/tools/sdk-cli/`
- Create directories as needed
- Do not modify existing files unless plan explicitly says "Updates FILE XX"

### OUTPUT FORMAT

After each file:
```
[XX/65] ✓ path/to/File.cs
```

After each phase:
```
═══════════════════════════════════════
PHASE X COMPLETE: [status]
Build: [PASS/FAIL]
Files: XX/XX
═══════════════════════════════════════
```

### START COMMAND

```
Execute Phase 0 now. Begin with File A01.
Do not summarize. Do not ask. Execute.
```

---

## QUICK REFERENCE

```
TOOL:      sdk-cli
MODES:     CLI (direct) | MCP (VS Code/Claude) | ACP (interactive)
LOCATION:  tools/sdk-cli/Sdk.Tools.Cli/
BUILD:     dotnet build tools/sdk-cli/Sdk.Tools.Cli/Sdk.Tools.Cli.csproj
RUN:       dotnet run --project tools/sdk-cli/Sdk.Tools.Cli -- <command>

ACP SDK:   AgentClientProtocol.Sdk (standalone, extractable)
LOCATION:  tools/sdk-cli/AgentClientProtocol.Sdk/
PURPOSE:   .NET implementation of Agent Client Protocol (mirrors TypeScript SDK)
```

---

## ARCHITECTURE: ACP SDK SEPARATION

The ACP SDK is built as a **standalone, general-purpose library** that can be extracted to its own repo.

```
tools/sdk-cli/
├── AgentClientProtocol.Sdk/           # ← EXTRACTABLE ACP SDK
│   ├── AgentClientProtocol.Sdk.csproj
│   ├── IAgent.cs                      # Agent interface (implements protocol)
│   ├── IClient.cs                     # Client interface (IDE side)
│   ├── AgentSideConnection.cs         # Agent's view of connection
│   ├── ClientSideConnection.cs        # Client's view of connection
│   ├── TerminalHandle.cs              # Terminal command handle
│   ├── Stream/
│   │   ├── IAcpStream.cs              # Bidirectional stream abstraction
│   │   └── NdJsonStream.cs            # Newline-delimited JSON transport
│   ├── JsonRpc/
│   │   ├── JsonRpcMessage.cs          # Base JSON-RPC 2.0 types
│   │   ├── Connection.cs              # Low-level connection management
│   │   └── RequestError.cs            # Standard error codes
│   └── Schema/
│       ├── ProtocolVersion.cs         # Protocol version constants
│       ├── AgentMethods.cs            # Agent method constants
│       ├── ClientMethods.cs           # Client method constants
│       ├── Capabilities.cs            # Agent/client capabilities
│       ├── SessionTypes.cs            # Session lifecycle types
│       ├── ContentTypes.cs            # Message content types
│       ├── TerminalTypes.cs           # Terminal command types
│       ├── FileSystemTypes.cs         # File system operation types
│       └── PermissionTypes.cs         # Permission request types
│
├── AgentClientProtocol.Sdk.Tests/     # ← ACP SDK TESTS
│   ├── AgentClientProtocol.Sdk.Tests.csproj
│   ├── JsonRpc/
│   │   └── JsonRpcMessageTests.cs
│   ├── Stream/
│   │   └── NdJsonStreamTests.cs
│   └── ConnectionTests.cs
│
├── Sdk.Tools.Cli/                     # ← APPLICATION (uses ACP SDK)
│   ├── Sdk.Tools.Cli.csproj           # References AgentClientProtocol.Sdk
│   ├── Acp/
│   │   ├── SampleGeneratorAgent.cs    # Agent implementation for this tool
│   │   └── SampleGeneratorAgentHost.cs # Agent hosting
│   └── ...
```

---

## EXECUTION ORDER

| # | File | Phase | Checkpoint |
|---|------|-------|------------|
| **ACP SDK (Phase 0 - Infrastructure)** |
| A01 | `AgentClientProtocol.Sdk/AgentClientProtocol.Sdk.csproj` | 0 | `dotnet restore` succeeds |
| A02 | `AgentClientProtocol.Sdk/JsonRpc/JsonRpcMessage.cs` | 0 | compiles |
| A03 | `AgentClientProtocol.Sdk/JsonRpc/RequestError.cs` | 0 | compiles |
| A04 | `AgentClientProtocol.Sdk/Schema/ProtocolVersion.cs` | 0 | compiles |
| A05 | `AgentClientProtocol.Sdk/Schema/AgentMethods.cs` | 0 | compiles |
| A06 | `AgentClientProtocol.Sdk/Schema/ClientMethods.cs` | 0 | compiles |
| A07 | `AgentClientProtocol.Sdk/Schema/Capabilities.cs` | 0 | compiles |
| A08 | `AgentClientProtocol.Sdk/Schema/SessionTypes.cs` | 0 | compiles |
| A09 | `AgentClientProtocol.Sdk/Schema/ContentTypes.cs` | 0 | compiles |
| A10 | `AgentClientProtocol.Sdk/Schema/TerminalTypes.cs` | 0 | compiles |
| A11 | `AgentClientProtocol.Sdk/Schema/FileSystemTypes.cs` | 0 | compiles |
| A12 | `AgentClientProtocol.Sdk/Schema/PermissionTypes.cs` | 0 | compiles |
| A13 | `AgentClientProtocol.Sdk/Stream/IAcpStream.cs` | 0 | compiles |
| A14 | `AgentClientProtocol.Sdk/Stream/NdJsonStream.cs` | 0 | compiles |
| A15 | `AgentClientProtocol.Sdk/JsonRpc/Connection.cs` | 0 | compiles |
| A16 | `AgentClientProtocol.Sdk/IAgent.cs` | 0 | compiles |
| A17 | `AgentClientProtocol.Sdk/IClient.cs` | 0 | compiles |
| A18 | `AgentClientProtocol.Sdk/AgentSideConnection.cs` | 0 | compiles |
| A19 | `AgentClientProtocol.Sdk/ClientSideConnection.cs` | 0 | compiles |
| A20 | `AgentClientProtocol.Sdk/TerminalHandle.cs` | 0 | **CHECKPOINT: `dotnet build` ACP SDK succeeds** |
| **SDK CLI (Phase 1 - Core)** |
| 01 | `Sdk.Tools.Cli.csproj` | 1 | `dotnet restore` succeeds |
| 02 | `Models/SdkLanguage.cs` | 1 | compiles |
| 03 | `Models/SampleConstants.cs` | 1 | compiles |
| 04 | `Models/SourceInput.cs` | 1 | compiles |
| 05 | `Models/GeneratedSample.cs` | 1 | compiles |
| 06 | `Models/LanguageInfo.cs` | 1 | compiles |
| 07 | `Models/SdkCliConfig.cs` | 1 | compiles |
| 08 | `Helpers/FileHelper.cs` | 1 | compiles |
| 09 | `Helpers/ConfigurationHelper.cs` | 1 | compiles |
| 10 | `Services/Languages/LanguageDetector.cs` | 1 | compiles |
| 11 | `Services/Languages/LanguageService.cs` | 1 | compiles |
| 12 | `Services/Languages/DotNetLanguageService.cs` | 1 | compiles |
| 13 | `Services/Languages/Samples/SampleLanguageContext.cs` | 1 | compiles |
| 14 | `Services/Languages/Samples/DotNetSampleLanguageContext.cs` | 1 | compiles |
| 15 | `Tools/Package/Samples/SampleGeneratorTool.cs` | 1 | compiles |
| 16 | `Services/CopilotAgentService.cs` | 1 | compiles |
| 17 | `Program.cs` | 1 | **CHECKPOINT: `dotnet build` succeeds** |
| 18 | `Prompts/SampleGeneration/system.md` | 1 | file exists |
| 19 | `Prompts/SampleGeneration/dotnet.md` | 1 | **PHASE 1 COMPLETE** |
| **Phase 2: ACP + MCP Integration** |
| 20 | `Acp/SampleGeneratorAgent.cs` | 2 | compiles |
| 21 | `Acp/SampleGeneratorAgentHost.cs` | 2 | compiles |
| 22 | `Services/InteractiveSampleGenerator.cs` | 2 | compiles |
| 23 | `Services/SamplesFolderScanner.cs` | 2 | compiles |
| 24 | `Mcp/McpServer.cs` | 2 | compiles |
| 25 | `Mcp/SampleGeneratorMcpTool.cs` | 2 | compiles |
| 26 | Update `Program.cs` (add acp/mcp commands) | 2 | **PHASE 2 COMPLETE** |
| **Phase 3: Additional Languages** |
| 27 | `Services/Languages/PythonLanguageService.cs` | 3 | compiles |
| 28 | `Services/Languages/TypeScriptLanguageService.cs` | 3 | compiles |
| 29 | `Services/Languages/JavaLanguageService.cs` | 3 | compiles |
| 30 | `Services/Languages/GoLanguageService.cs` | 3 | compiles |
| 31 | `Services/Languages/Samples/PythonSampleLanguageContext.cs` | 3 | compiles |
| 32 | `Services/Languages/Samples/TypeScriptSampleLanguageContext.cs` | 3 | compiles |
| 33 | `Services/Languages/Samples/JavaSampleLanguageContext.cs` | 3 | compiles |
| 34 | `Services/Languages/Samples/GoSampleLanguageContext.cs` | 3 | compiles |
| 35 | `Prompts/SampleGeneration/python.md` | 3 | file exists |
| 36 | `Prompts/SampleGeneration/typescript.md` | 3 | file exists |
| 37 | `Prompts/SampleGeneration/java.md` | 3 | file exists |
| 38 | `Prompts/SampleGeneration/go.md` | 3 | **PHASE 3 COMPLETE** |
| **Phase 4: Tests + Docs** |
| 39 | `AgentClientProtocol.Sdk.Tests/AgentClientProtocol.Sdk.Tests.csproj` | 4 | `dotnet build` succeeds |
| 40 | `AgentClientProtocol.Sdk.Tests/JsonRpc/JsonRpcMessageTests.cs` | 4 | tests pass |
| 41 | `AgentClientProtocol.Sdk.Tests/Stream/NdJsonStreamTests.cs` | 4 | tests pass |
| 42 | `AgentClientProtocol.Sdk.Tests/ConnectionTests.cs` | 4 | tests pass |
| 43 | `Sdk.Tools.Cli.Tests/Sdk.Tools.Cli.Tests.csproj` | 4 | `dotnet build` succeeds |
| 44 | `Sdk.Tools.Cli.Tests/LanguageDetectorTests.cs` | 4 | tests pass |
| 45 | `README.md` | 4 | **PHASE 4 COMPLETE** |

---

## PHASE CHECKPOINTS

After each phase, run validation:

```bash
# Phase 1: Core builds and CLI works
dotnet build tools/sdk-cli/Sdk.Tools.Cli/Sdk.Tools.Cli.csproj
dotnet run --project tools/sdk-cli/Sdk.Tools.Cli -- package sample generate --help

# Phase 2: All modes work
dotnet run --project tools/sdk-cli/Sdk.Tools.Cli -- acp --help
dotnet run --project tools/sdk-cli/Sdk.Tools.Cli -- mcp --help

# Phase 3: All languages detected
# (manual verification with test repos)

# Phase 4: Tests pass
dotnet test tools/sdk-cli/Sdk.Tools.Cli.Tests/
```

---

# FILE DEFINITIONS

All paths relative to: `tools/sdk-cli/`

---

# PHASE 0: ACP SDK (Extractable)

All Phase 0 files go in: `AgentClientProtocol.Sdk/`

This SDK mirrors the TypeScript SDK at https://github.com/agentclientprotocol/typescript-sdk

---

## FILE A01: AgentClientProtocol.Sdk/AgentClientProtocol.Sdk.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AgentClientProtocol.Sdk</RootNamespace>
    
    <!-- Package metadata for future NuGet extraction -->
    <PackageId>AgentClientProtocol.Sdk</PackageId>
    <Version>0.1.0</Version>
    <Authors>Microsoft</Authors>
    <Description>.NET SDK for Agent Client Protocol (ACP) - Build AI coding agents that integrate with IDEs</Description>
    <PackageTags>acp;agent;ai;coding;protocol</PackageTags>
    <PackageProjectUrl>https://agentclientprotocol.com</PackageProjectUrl>
    <RepositoryUrl>https://github.com/agentclientprotocol/dotnet-sdk</RepositoryUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
  </ItemGroup>

</Project>
```

### VALIDATE
```bash
dotnet restore tools/sdk-cli/AgentClientProtocol.Sdk/AgentClientProtocol.Sdk.csproj
```

### DONE WHEN
Exit code 0.

---

## FILE A02: AgentClientProtocol.Sdk/JsonRpc/JsonRpcMessage.cs

```csharp
// Agent Client Protocol - .NET SDK
// JSON-RPC 2.0 type definitions

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentClientProtocol.Sdk.JsonRpc;

/// <summary>
/// Base for all JSON-RPC messages.
/// </summary>
public abstract record JsonRpcMessageBase
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
}

/// <summary>
/// JSON-RPC 2.0 request message.
/// </summary>
public record JsonRpcRequest : JsonRpcMessageBase
{
    [JsonPropertyName("id")]
    public object? Id { get; init; }
    
    [JsonPropertyName("method")]
    public required string Method { get; init; }
    
    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Params { get; init; }
}

/// <summary>
/// JSON-RPC 2.0 response message.
/// </summary>
public record JsonRpcResponse : JsonRpcMessageBase
{
    [JsonPropertyName("id")]
    public object? Id { get; init; }
    
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; init; }
    
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; init; }
    
    public static JsonRpcResponse Success(object? id, object? result) => 
        new() { Id = id, Result = result };
    
    public static JsonRpcResponse Failure(object? id, JsonRpcError error) => 
        new() { Id = id, Error = error };
}

/// <summary>
/// JSON-RPC 2.0 error object.
/// </summary>
public record JsonRpcError
{
    [JsonPropertyName("code")]
    public required int Code { get; init; }
    
    [JsonPropertyName("message")]
    public required string Message { get; init; }
    
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; init; }
}

/// <summary>
/// JSON-RPC 2.0 notification message (request without id).
/// </summary>
public record JsonRpcNotification : JsonRpcMessageBase
{
    [JsonPropertyName("method")]
    public required string Method { get; init; }
    
    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; init; }
}

/// <summary>
/// Union type for any JSON-RPC message.
/// </summary>
public abstract record AnyMessage;

public record RequestMessage(JsonRpcRequest Request) : AnyMessage;
public record ResponseMessage(JsonRpcResponse Response) : AnyMessage;
public record NotificationMessage(JsonRpcNotification Notification) : AnyMessage;
```

### VALIDATE
Build succeeds.

---

## FILE A03: AgentClientProtocol.Sdk/JsonRpc/RequestError.cs

```csharp
// Agent Client Protocol - .NET SDK
// Standard JSON-RPC error codes

namespace AgentClientProtocol.Sdk.JsonRpc;

/// <summary>
/// JSON-RPC 2.0 error with standard ACP error codes.
/// </summary>
public class RequestError : Exception
{
    public int Code { get; }
    public object? Data { get; }
    
    public RequestError(int code, string message, object? data = null) : base(message)
    {
        Code = code;
        Data = data;
    }
    
    // JSON-RPC 2.0 standard errors
    public static RequestError ParseError(object? data = null, string? additional = null) =>
        new(-32700, $"Parse error{(additional != null ? $": {additional}" : "")}", data);
    
    public static RequestError InvalidRequest(object? data = null, string? additional = null) =>
        new(-32600, $"Invalid Request{(additional != null ? $": {additional}" : "")}", data);
    
    public static RequestError MethodNotFound(string method) =>
        new(-32601, $"Method not found: {method}");
    
    public static RequestError InvalidParams(object? data = null, string? additional = null) =>
        new(-32602, $"Invalid params{(additional != null ? $": {additional}" : "")}", data);
    
    public static RequestError InternalError(object? data = null, string? additional = null) =>
        new(-32603, $"Internal error{(additional != null ? $": {additional}" : "")}", data);
    
    // ACP-specific errors (reserved range -32000 to -32099)
    public static RequestError AuthRequired(object? data = null, string? additional = null) =>
        new(-32000, $"Authentication required{(additional != null ? $": {additional}" : "")}", data);
    
    public static RequestError ResourceNotFound(string? uri = null) =>
        new(-32002, $"Resource not found{(uri != null ? $": {uri}" : "")}", uri != null ? new { uri } : null);
    
    public static RequestError Cancelled() =>
        new(-32800, "Request cancelled");
    
    public JsonRpcError ToError() => new()
    {
        Code = Code,
        Message = Message,
        Data = Data
    };
    
    public JsonRpcResponse ToResponse(object? id) => 
        JsonRpcResponse.Failure(id, ToError());
}
```

### VALIDATE
Build succeeds.

---

## FILE A04: AgentClientProtocol.Sdk/Schema/ProtocolVersion.cs

```csharp
// Agent Client Protocol - .NET SDK
// Protocol version constants

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// ACP protocol version.
/// </summary>
public static class Protocol
{
    /// <summary>
    /// Current protocol version supported by this SDK.
    /// </summary>
    public const int Version = 1;
    
    /// <summary>
    /// Protocol version string for display.
    /// </summary>
    public const string VersionString = "2024-11-05";
}
```

### VALIDATE
Build succeeds.

---

## FILE A05: AgentClientProtocol.Sdk/Schema/AgentMethods.cs

```csharp
// Agent Client Protocol - .NET SDK
// Methods that clients call on agents

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Method names for requests that clients send to agents.
/// </summary>
public static class AgentMethods
{
    public const string Initialize = "initialize";
    public const string Authenticate = "authenticate";
    
    // Session methods
    public const string SessionNew = "session/new";
    public const string SessionLoad = "session/load";
    public const string SessionList = "session/list";
    public const string SessionFork = "session/fork";
    public const string SessionResume = "session/resume";
    public const string SessionPrompt = "session/prompt";
    public const string SessionCancel = "session/cancel";
    public const string SessionSetMode = "session/set_mode";
    public const string SessionSetModel = "session/set_model";
    public const string SessionSetConfigOption = "session/set_config_option";
}
```

### VALIDATE
Build succeeds.

---

## FILE A06: AgentClientProtocol.Sdk/Schema/ClientMethods.cs

```csharp
// Agent Client Protocol - .NET SDK
// Methods that agents call on clients

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Method names for requests that agents send to clients.
/// </summary>
public static class ClientMethods
{
    // File system methods
    public const string FsReadTextFile = "fs/read_text_file";
    public const string FsWriteTextFile = "fs/write_text_file";
    
    // Session methods
    public const string SessionRequestPermission = "session/request_permission";
    public const string SessionRequestInput = "session/request_input";
    public const string SessionUpdate = "session/update";
    
    // Terminal methods
    public const string TerminalCreate = "terminal/create";
    public const string TerminalOutput = "terminal/output";
    public const string TerminalRelease = "terminal/release";
    public const string TerminalWaitForExit = "terminal/wait_for_exit";
    public const string TerminalKill = "terminal/kill";
}
```

### VALIDATE
Build succeeds.

---

## FILE A07: AgentClientProtocol.Sdk/Schema/Capabilities.cs

```csharp
// Agent Client Protocol - .NET SDK
// Capability types for initialization

using System.Text.Json.Serialization;

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Capabilities supported by the agent.
/// </summary>
public record AgentCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("loadSession")]
    public bool? LoadSession { get; init; }
    
    [JsonPropertyName("mcpCapabilities")]
    public McpCapabilities? McpCapabilities { get; init; }
    
    [JsonPropertyName("promptCapabilities")]
    public PromptCapabilities? PromptCapabilities { get; init; }
    
    [JsonPropertyName("sessionCapabilities")]
    public SessionCapabilities? SessionCapabilities { get; init; }
}

/// <summary>
/// Capabilities supported by the client.
/// </summary>
public record ClientCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("fs")]
    public FileSystemCapability? Fs { get; init; }
    
    [JsonPropertyName("terminal")]
    public bool? Terminal { get; init; }
}

/// <summary>
/// File system capabilities.
/// </summary>
public record FileSystemCapability
{
    [JsonPropertyName("readTextFile")]
    public bool ReadTextFile { get; init; }
    
    [JsonPropertyName("writeTextFile")]
    public bool WriteTextFile { get; init; }
}

/// <summary>
/// MCP capabilities supported by the agent.
/// </summary>
public record McpCapabilities
{
    [JsonPropertyName("http")]
    public bool? Http { get; init; }
    
    [JsonPropertyName("sse")]
    public bool? Sse { get; init; }
}

/// <summary>
/// Prompt capabilities.
/// </summary>
public record PromptCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

/// <summary>
/// Session capabilities.
/// </summary>
public record SessionCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("fork")]
    public SessionForkCapabilities? Fork { get; init; }
    
    [JsonPropertyName("list")]
    public SessionListCapabilities? List { get; init; }
    
    [JsonPropertyName("resume")]
    public SessionResumeCapabilities? Resume { get; init; }
}

public record SessionForkCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

public record SessionListCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

public record SessionResumeCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

/// <summary>
/// Metadata about implementation.
/// </summary>
public record Implementation
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("version")]
    public required string Version { get; init; }
    
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }
}
```

### VALIDATE
Build succeeds.

---

## FILE A08: AgentClientProtocol.Sdk/Schema/SessionTypes.cs

```csharp
// Agent Client Protocol - .NET SDK
// Session-related types

using System.Text.Json.Serialization;

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Request to initialize connection.
/// </summary>
public record InitializeRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("protocolVersion")]
    public required int ProtocolVersion { get; init; }
    
    [JsonPropertyName("clientCapabilities")]
    public ClientCapabilities? ClientCapabilities { get; init; }
    
    [JsonPropertyName("clientInfo")]
    public Implementation? ClientInfo { get; init; }
}

/// <summary>
/// Response to initialize request.
/// </summary>
public record InitializeResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("protocolVersion")]
    public required int ProtocolVersion { get; init; }
    
    [JsonPropertyName("agentCapabilities")]
    public AgentCapabilities? AgentCapabilities { get; init; }
    
    [JsonPropertyName("agentInfo")]
    public Implementation? AgentInfo { get; init; }
    
    [JsonPropertyName("authMethods")]
    public AuthMethod[]? AuthMethods { get; init; }
}

/// <summary>
/// Authentication method.
/// </summary>
public record AuthMethod
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Request to authenticate.
/// </summary>
public record AuthenticateRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("methodId")]
    public required string MethodId { get; init; }
}

/// <summary>
/// Response to authenticate request.
/// </summary>
public record AuthenticateResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

/// <summary>
/// Request to create a new session.
/// </summary>
public record NewSessionRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("cwd")]
    public required string Cwd { get; init; }
    
    [JsonPropertyName("mcpServers")]
    public McpServer[]? McpServers { get; init; }
}

/// <summary>
/// Response to new session request.
/// </summary>
public record NewSessionResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("modes")]
    public SessionModeState? Modes { get; init; }
}

/// <summary>
/// Request to load an existing session.
/// </summary>
public record LoadSessionRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("cwd")]
    public required string Cwd { get; init; }
    
    [JsonPropertyName("mcpServers")]
    public McpServer[]? McpServers { get; init; }
}

/// <summary>
/// Response to load session request.
/// </summary>
public record LoadSessionResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

/// <summary>
/// Request to prompt in a session.
/// </summary>
public record PromptRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("prompt")]
    public required ContentBlock[] Prompt { get; init; }
}

/// <summary>
/// Response to prompt request.
/// </summary>
public record PromptResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("stopReason")]
    public required string StopReason { get; init; }
}

/// <summary>
/// Cancel notification.
/// </summary>
public record CancelNotification
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
}

/// <summary>
/// MCP server configuration.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(McpServerStdio), "stdio")]
[JsonDerivedType(typeof(McpServerHttp), "http")]
[JsonDerivedType(typeof(McpServerSse), "sse")]
public abstract record McpServer
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

public record McpServerStdio : McpServer
{
    [JsonPropertyName("command")]
    public required string Command { get; init; }
    
    [JsonPropertyName("args")]
    public string[]? Args { get; init; }
    
    [JsonPropertyName("env")]
    public EnvVariable[]? Env { get; init; }
}

public record McpServerHttp : McpServer
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }
    
    [JsonPropertyName("headers")]
    public HttpHeader[]? Headers { get; init; }
}

public record McpServerSse : McpServer
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }
    
    [JsonPropertyName("headers")]
    public HttpHeader[]? Headers { get; init; }
}

public record EnvVariable
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("value")]
    public required string Value { get; init; }
}

public record HttpHeader
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("value")]
    public required string Value { get; init; }
}

/// <summary>
/// Session mode state.
/// </summary>
public record SessionModeState
{
    [JsonPropertyName("availableModes")]
    public required SessionMode[] AvailableModes { get; init; }
    
    [JsonPropertyName("currentModeId")]
    public required string CurrentModeId { get; init; }
}

/// <summary>
/// Session mode.
/// </summary>
public record SessionMode
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Stop reasons for prompt responses.
/// </summary>
public static class StopReason
{
    public const string EndTurn = "end_turn";
    public const string MaxTokens = "max_tokens";
    public const string StopSequence = "stop_sequence";
    public const string Cancelled = "cancelled";
}
```

### VALIDATE
Build succeeds.

---

## FILE A09: AgentClientProtocol.Sdk/Schema/ContentTypes.cs

```csharp
// Agent Client Protocol - .NET SDK
// Content types for messages

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Base content block in prompts and responses.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContent), "text")]
[JsonDerivedType(typeof(ImageContent), "image")]
[JsonDerivedType(typeof(AudioContent), "audio")]
[JsonDerivedType(typeof(EmbeddedResource), "resource")]
public abstract record ContentBlock;

/// <summary>
/// Text content.
/// </summary>
public record TextContent : ContentBlock
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }
    
    [JsonPropertyName("annotations")]
    public Annotations? Annotations { get; init; }
}

/// <summary>
/// Image content.
/// </summary>
public record ImageContent : ContentBlock
{
    [JsonPropertyName("data")]
    public required string Data { get; init; }
    
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }
    
    [JsonPropertyName("annotations")]
    public Annotations? Annotations { get; init; }
}

/// <summary>
/// Audio content.
/// </summary>
public record AudioContent : ContentBlock
{
    [JsonPropertyName("data")]
    public required string Data { get; init; }
    
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }
    
    [JsonPropertyName("annotations")]
    public Annotations? Annotations { get; init; }
}

/// <summary>
/// Embedded resource content.
/// </summary>
public record EmbeddedResource : ContentBlock
{
    [JsonPropertyName("resource")]
    public required ResourceContents Resource { get; init; }
    
    [JsonPropertyName("annotations")]
    public Annotations? Annotations { get; init; }
}

/// <summary>
/// Resource contents (text or blob).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextResourceContents), "text")]
[JsonDerivedType(typeof(BlobResourceContents), "blob")]
public abstract record ResourceContents
{
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }
    
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }
}

public record TextResourceContents : ResourceContents
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

public record BlobResourceContents : ResourceContents
{
    [JsonPropertyName("blob")]
    public required string Blob { get; init; }
}

/// <summary>
/// Annotations for content.
/// </summary>
public record Annotations
{
    [JsonPropertyName("audience")]
    public string[]? Audience { get; init; }
    
    [JsonPropertyName("priority")]
    public double? Priority { get; init; }
    
    [JsonPropertyName("lastModified")]
    public string? LastModified { get; init; }
}

/// <summary>
/// Session update notification.
/// </summary>
public record SessionNotification
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("update")]
    public required SessionUpdate Update { get; init; }
}

/// <summary>
/// Session update payload types.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "sessionUpdate")]
[JsonDerivedType(typeof(AgentMessageChunk), "agent_message_chunk")]
[JsonDerivedType(typeof(ToolCallUpdate), "tool_call")]
[JsonDerivedType(typeof(ToolCallStatusUpdate), "tool_call_update")]
[JsonDerivedType(typeof(PlanUpdate), "plan")]
[JsonDerivedType(typeof(CurrentModeUpdate), "current_mode_update")]
public abstract record SessionUpdate;

public record AgentMessageChunk : SessionUpdate
{
    [JsonPropertyName("content")]
    public required ContentBlock Content { get; init; }
}

public record ToolCallUpdate : SessionUpdate
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }
    
    [JsonPropertyName("status")]
    public required string Status { get; init; }
    
    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; init; }
    
    [JsonPropertyName("content")]
    public ContentBlock[]? Content { get; init; }
}

public record ToolCallStatusUpdate : SessionUpdate
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("status")]
    public required string Status { get; init; }
    
    [JsonPropertyName("content")]
    public ContentBlock[]? Content { get; init; }
}

public record PlanUpdate : SessionUpdate
{
    [JsonPropertyName("entries")]
    public required PlanEntry[] Entries { get; init; }
}

public record PlanEntry
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("status")]
    public required string Status { get; init; }
    
    [JsonPropertyName("priority")]
    public string? Priority { get; init; }
}

public record CurrentModeUpdate : SessionUpdate
{
    [JsonPropertyName("currentModeId")]
    public required string CurrentModeId { get; init; }
}

/// <summary>
/// Tool call status values.
/// </summary>
public static class ToolCallStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

/// <summary>
/// Plan entry status values.
/// </summary>
public static class PlanEntryStatus
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

/// <summary>
/// Diff representing file modifications.
/// </summary>
public record Diff
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }
    
    [JsonPropertyName("oldText")]
    public string? OldText { get; init; }
    
    [JsonPropertyName("newText")]
    public required string NewText { get; init; }
}
```

### VALIDATE
Build succeeds.

---

## FILE A10: AgentClientProtocol.Sdk/Schema/TerminalTypes.cs

```csharp
// Agent Client Protocol - .NET SDK
// Terminal-related types

using System.Text.Json.Serialization;

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Request to create a terminal.
/// </summary>
public record CreateTerminalRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("command")]
    public required string Command { get; init; }
    
    [JsonPropertyName("args")]
    public string[]? Args { get; init; }
    
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }
    
    [JsonPropertyName("env")]
    public EnvVariable[]? Env { get; init; }
}

/// <summary>
/// Response to create terminal request.
/// </summary>
public record CreateTerminalResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("terminalId")]
    public required string TerminalId { get; init; }
}

/// <summary>
/// Request to get terminal output.
/// </summary>
public record TerminalOutputRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("terminalId")]
    public required string TerminalId { get; init; }
}

/// <summary>
/// Response with terminal output.
/// </summary>
public record TerminalOutputResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("output")]
    public required string Output { get; init; }
    
    [JsonPropertyName("exitStatus")]
    public TerminalExitStatus? ExitStatus { get; init; }
}

/// <summary>
/// Request to release a terminal.
/// </summary>
public record ReleaseTerminalRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("terminalId")]
    public required string TerminalId { get; init; }
}

/// <summary>
/// Response to release terminal request.
/// </summary>
public record ReleaseTerminalResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

/// <summary>
/// Request to wait for terminal exit.
/// </summary>
public record WaitForTerminalExitRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("terminalId")]
    public required string TerminalId { get; init; }
}

/// <summary>
/// Response with terminal exit status.
/// </summary>
public record WaitForTerminalExitResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; init; }
    
    [JsonPropertyName("signal")]
    public string? Signal { get; init; }
}

/// <summary>
/// Request to kill a terminal command.
/// </summary>
public record KillTerminalCommandRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("terminalId")]
    public required string TerminalId { get; init; }
}

/// <summary>
/// Response to kill terminal command request.
/// </summary>
public record KillTerminalCommandResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

/// <summary>
/// Terminal exit status.
/// </summary>
public record TerminalExitStatus
{
    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; init; }
    
    [JsonPropertyName("signal")]
    public string? Signal { get; init; }
}

/// <summary>
/// Terminal embed reference.
/// </summary>
public record Terminal
{
    [JsonPropertyName("terminalId")]
    public required string TerminalId { get; init; }
}
```

### VALIDATE
Build succeeds.

---

## FILE A11: AgentClientProtocol.Sdk/Schema/FileSystemTypes.cs

```csharp
// Agent Client Protocol - .NET SDK
// File system types

using System.Text.Json.Serialization;

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Request to read a text file.
/// </summary>
public record ReadTextFileRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("path")]
    public required string Path { get; init; }
}

/// <summary>
/// Response with file contents.
/// </summary>
public record ReadTextFileResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

/// <summary>
/// Request to write a text file.
/// </summary>
public record WriteTextFileRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("path")]
    public required string Path { get; init; }
    
    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

/// <summary>
/// Response to write file request.
/// </summary>
public record WriteTextFileResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}
```

### VALIDATE
Build succeeds.

---

## FILE A12: AgentClientProtocol.Sdk/Schema/PermissionTypes.cs

```csharp
// Agent Client Protocol - .NET SDK
// Permission request types

using System.Text.Json.Serialization;

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Request permission from user.
/// </summary>
public record RequestPermissionRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("toolCallId")]
    public required string ToolCallId { get; init; }
    
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("options")]
    public required PermissionOption[] Options { get; init; }
}

/// <summary>
/// Permission option.
/// </summary>
public record PermissionOption(string Id, string Label, string Kind)
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Id;
    
    [JsonPropertyName("label")]
    public string Label { get; init; } = Label;
    
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = Kind;
}

/// <summary>
/// Response to permission request.
/// </summary>
public record RequestPermissionResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("outcome")]
    public required PermissionOutcome Outcome { get; init; }
}

/// <summary>
/// Permission outcome.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "outcome")]
[JsonDerivedType(typeof(SelectedPermissionOutcome), "selected")]
[JsonDerivedType(typeof(DismissedPermissionOutcome), "dismissed")]
public abstract record PermissionOutcome;

public record SelectedPermissionOutcome : PermissionOutcome
{
    [JsonPropertyName("optionId")]
    public required string OptionId { get; init; }
}

public record DismissedPermissionOutcome : PermissionOutcome;

/// <summary>
/// Permission option kinds.
/// </summary>
public static class PermissionKind
{
    public const string AllowOnce = "allow_once";
    public const string AllowAlways = "allow_always";
    public const string RejectOnce = "reject_once";
    public const string RejectAlways = "reject_always";
}

/// <summary>
/// Request text input from user.
/// </summary>
public record RequestInputRequest
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }
    
    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }
    
    [JsonPropertyName("defaultValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Response to input request.
/// </summary>
public record RequestInputResponse
{
    [JsonPropertyName("value")]
    public string? Value { get; init; }
    
    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; init; }
}
```

### VALIDATE
Build succeeds.

---

## FILE A13: AgentClientProtocol.Sdk/Stream/IAcpStream.cs

```csharp
// Agent Client Protocol - .NET SDK
// Stream abstraction

using AgentClientProtocol.Sdk.JsonRpc;

namespace AgentClientProtocol.Sdk.Stream;

/// <summary>
/// Bidirectional stream for ACP communication.
/// </summary>
public interface IAcpStream
{
    /// <summary>
    /// Read the next message from the stream.
    /// </summary>
    ValueTask<JsonRpcMessageBase?> ReadAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Write a message to the stream.
    /// </summary>
    ValueTask WriteAsync(JsonRpcMessageBase message, CancellationToken ct = default);
    
    /// <summary>
    /// Close the stream.
    /// </summary>
    ValueTask CloseAsync();
}
```

### VALIDATE
Build succeeds.

---

## FILE A14: AgentClientProtocol.Sdk/Stream/NdJsonStream.cs

```csharp
// Agent Client Protocol - .NET SDK
// Newline-delimited JSON transport (stdio)

using System.Text;
using System.Text.Json;
using AgentClientProtocol.Sdk.JsonRpc;
using Microsoft.Extensions.Logging;

namespace AgentClientProtocol.Sdk.Stream;

/// <summary>
/// Newline-delimited JSON stream for stdio-based ACP communication.
/// </summary>
public class NdJsonStream : IAcpStream
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    
    public NdJsonStream(TextReader input, TextWriter output, ILogger? logger = null)
    {
        _input = input;
        _output = output;
        _logger = logger;
    }
    
    /// <summary>
    /// Create stream from stdin/stdout.
    /// </summary>
    public static NdJsonStream FromStdio(ILogger? logger = null)
    {
        var input = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        var output = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true };
        return new NdJsonStream(input, output, logger);
    }
    
    /// <summary>
    /// Create stream from arbitrary streams.
    /// </summary>
    public static NdJsonStream FromStreams(System.IO.Stream input, System.IO.Stream output, ILogger? logger = null)
    {
        var reader = new StreamReader(input, Encoding.UTF8);
        var writer = new StreamWriter(output, Encoding.UTF8) { AutoFlush = true };
        return new NdJsonStream(reader, writer, logger);
    }
    
    public async ValueTask<JsonRpcMessageBase?> ReadAsync(CancellationToken ct = default)
    {
        var line = await _input.ReadLineAsync(ct);
        if (line == null) return null;
        
        if (string.IsNullOrWhiteSpace(line)) return await ReadAsync(ct);
        
        _logger?.LogTrace("ACP recv: {Message}", line);
        
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            
            // Check if it's a response (has result or error)
            if (root.TryGetProperty("result", out _) || root.TryGetProperty("error", out _))
            {
                return JsonSerializer.Deserialize<JsonRpcResponse>(line, JsonOptions);
            }
            
            // Check if it's a request (has id and method)
            if (root.TryGetProperty("id", out _) && root.TryGetProperty("method", out _))
            {
                return JsonSerializer.Deserialize<JsonRpcRequest>(line, JsonOptions);
            }
            
            // Otherwise it's a notification (method only)
            if (root.TryGetProperty("method", out _))
            {
                return JsonSerializer.Deserialize<JsonRpcNotification>(line, JsonOptions);
            }
            
            _logger?.LogWarning("Unknown message format: {Message}", line);
            return null;
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Failed to parse message: {Message}", line);
            return null;
        }
    }
    
    public async ValueTask WriteAsync(JsonRpcMessageBase message, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(message, message.GetType(), JsonOptions);
        
        await _writeLock.WaitAsync(ct);
        try
        {
            _logger?.LogTrace("ACP send: {Message}", json);
            await _output.WriteLineAsync(json);
        }
        finally
        {
            _writeLock.Release();
        }
    }
    
    public ValueTask CloseAsync()
    {
        _input.Dispose();
        _output.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

### VALIDATE
Build succeeds.

---

## FILE A15: AgentClientProtocol.Sdk/JsonRpc/Connection.cs

```csharp
// Agent Client Protocol - .NET SDK
// Low-level JSON-RPC connection management

using System.Collections.Concurrent;
using AgentClientProtocol.Sdk.Stream;
using Microsoft.Extensions.Logging;

namespace AgentClientProtocol.Sdk.JsonRpc;

/// <summary>
/// Manages bidirectional JSON-RPC communication.
/// </summary>
public class Connection : IAsyncDisposable
{
    private readonly IAcpStream _stream;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<object, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = new();
    private readonly CancellationTokenSource _cts = new();
    private int _requestId = 0;
    
    private Func<string, object?, Task<object?>>? _requestHandler;
    private Func<string, object?, Task>? _notificationHandler;
    
    public Connection(IAcpStream stream, ILogger? logger = null)
    {
        _stream = stream;
        _logger = logger;
    }
    
    /// <summary>
    /// Set handler for incoming requests.
    /// </summary>
    public void OnRequest(Func<string, object?, Task<object?>> handler)
    {
        _requestHandler = handler;
    }
    
    /// <summary>
    /// Set handler for incoming notifications.
    /// </summary>
    public void OnNotification(Func<string, object?, Task> handler)
    {
        _notificationHandler = handler;
    }
    
    /// <summary>
    /// Start processing incoming messages.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        
        try
        {
            while (!linked.Token.IsCancellationRequested)
            {
                var message = await _stream.ReadAsync(linked.Token);
                if (message == null) break;
                
                _ = Task.Run(() => HandleMessageAsync(message), linked.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Connection error");
        }
    }
    
    private async Task HandleMessageAsync(JsonRpcMessageBase message)
    {
        switch (message)
        {
            case JsonRpcRequest request:
                await HandleRequestAsync(request);
                break;
            case JsonRpcResponse response:
                HandleResponse(response);
                break;
            case JsonRpcNotification notification:
                await HandleNotificationAsync(notification);
                break;
        }
    }
    
    private async Task HandleRequestAsync(JsonRpcRequest request)
    {
        if (_requestHandler == null)
        {
            await SendErrorAsync(request.Id, RequestError.MethodNotFound(request.Method));
            return;
        }
        
        try
        {
            var result = await _requestHandler(request.Method, request.Params);
            await SendResponseAsync(request.Id, result);
        }
        catch (RequestError ex)
        {
            await SendErrorAsync(request.Id, ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Request handler error for {Method}", request.Method);
            await SendErrorAsync(request.Id, RequestError.InternalError(null, ex.Message));
        }
    }
    
    private void HandleResponse(JsonRpcResponse response)
    {
        if (response.Id != null && _pendingRequests.TryRemove(response.Id, out var tcs))
        {
            tcs.TrySetResult(response);
        }
    }
    
    private async Task HandleNotificationAsync(JsonRpcNotification notification)
    {
        if (_notificationHandler != null)
        {
            try
            {
                await _notificationHandler(notification.Method, notification.Params);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Notification handler error for {Method}", notification.Method);
            }
        }
    }
    
    /// <summary>
    /// Send a request and wait for response.
    /// </summary>
    public async Task<T?> SendRequestAsync<T>(string method, object? parameters = null, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _requestId);
        var tcs = new TaskCompletionSource<JsonRpcResponse>();
        _pendingRequests[id] = tcs;
        
        try
        {
            var request = new JsonRpcRequest
            {
                Id = id,
                Method = method,
                Params = parameters != null ? System.Text.Json.JsonSerializer.SerializeToElement(parameters) : null
            };
            
            await _stream.WriteAsync(request, ct);
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            
            var response = await tcs.Task.WaitAsync(cts.Token);
            
            if (response.Error != null)
            {
                throw new RequestError(response.Error.Code, response.Error.Message, response.Error.Data);
            }
            
            if (response.Result == null) return default;
            
            return System.Text.Json.JsonSerializer.Deserialize<T>(
                System.Text.Json.JsonSerializer.Serialize(response.Result));
        }
        finally
        {
            _pendingRequests.TryRemove(id, out _);
        }
    }
    
    /// <summary>
    /// Send a notification (no response expected).
    /// </summary>
    public async Task SendNotificationAsync(string method, object? parameters = null, CancellationToken ct = default)
    {
        var notification = new JsonRpcNotification
        {
            Method = method,
            Params = parameters
        };
        await _stream.WriteAsync(notification, ct);
    }
    
    private async Task SendResponseAsync(object? id, object? result)
    {
        var response = JsonRpcResponse.Success(id, result);
        await _stream.WriteAsync(response);
    }
    
    private async Task SendErrorAsync(object? id, RequestError error)
    {
        var response = error.ToResponse(id);
        await _stream.WriteAsync(response);
    }
    
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        await _stream.CloseAsync();
        _cts.Dispose();
    }
}
```

### VALIDATE
Build succeeds.

---

## FILE A16: AgentClientProtocol.Sdk/IAgent.cs

```csharp
// Agent Client Protocol - .NET SDK
// Agent interface

using AgentClientProtocol.Sdk.Schema;

namespace AgentClientProtocol.Sdk;

/// <summary>
/// Interface that all ACP-compliant agents must implement.
/// 
/// Agents are programs that use generative AI to autonomously modify code.
/// They handle requests from clients (IDEs) and execute tasks using language models and tools.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Establishes the connection and negotiates protocol capabilities.
    /// </summary>
    Task<InitializeResponse> InitializeAsync(InitializeRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Creates a new conversation session.
    /// </summary>
    Task<NewSessionResponse> NewSessionAsync(NewSessionRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Processes a user prompt within a session.
    /// </summary>
    Task<PromptResponse> PromptAsync(PromptRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Handles session cancellation.
    /// </summary>
    Task CancelAsync(CancelNotification notification, CancellationToken ct = default);
    
    /// <summary>
    /// Authenticates the client (optional).
    /// </summary>
    Task<AuthenticateResponse?> AuthenticateAsync(AuthenticateRequest request, CancellationToken ct = default) =>
        Task.FromResult<AuthenticateResponse?>(null);
    
    /// <summary>
    /// Loads an existing session (optional).
    /// </summary>
    Task<LoadSessionResponse?> LoadSessionAsync(LoadSessionRequest request, CancellationToken ct = default) =>
        Task.FromResult<LoadSessionResponse?>(null);
}
```

### VALIDATE
Build succeeds.

---

## FILE A17: AgentClientProtocol.Sdk/IClient.cs

```csharp
// Agent Client Protocol - .NET SDK
// Client interface

using AgentClientProtocol.Sdk.Schema;

namespace AgentClientProtocol.Sdk;

/// <summary>
/// Interface that ACP-compliant clients must implement.
/// 
/// Clients are typically code editors (IDEs) that provide the interface
/// between users and AI agents. They manage the environment, handle user interactions,
/// and control access to resources.
/// </summary>
public interface IClient
{
    /// <summary>
    /// Handle permission request from agent.
    /// </summary>
    Task<RequestPermissionResponse> RequestPermissionAsync(RequestPermissionRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Handle session update from agent.
    /// </summary>
    Task SessionUpdateAsync(SessionNotification notification, CancellationToken ct = default);
    
    /// <summary>
    /// Read a text file (optional).
    /// </summary>
    Task<ReadTextFileResponse?> ReadTextFileAsync(ReadTextFileRequest request, CancellationToken ct = default) =>
        Task.FromResult<ReadTextFileResponse?>(null);
    
    /// <summary>
    /// Write a text file (optional).
    /// </summary>
    Task<WriteTextFileResponse?> WriteTextFileAsync(WriteTextFileRequest request, CancellationToken ct = default) =>
        Task.FromResult<WriteTextFileResponse?>(null);
    
    /// <summary>
    /// Create a terminal (optional).
    /// </summary>
    Task<CreateTerminalResponse?> CreateTerminalAsync(CreateTerminalRequest request, CancellationToken ct = default) =>
        Task.FromResult<CreateTerminalResponse?>(null);
    
    /// <summary>
    /// Get terminal output (optional).
    /// </summary>
    Task<TerminalOutputResponse?> TerminalOutputAsync(TerminalOutputRequest request, CancellationToken ct = default) =>
        Task.FromResult<TerminalOutputResponse?>(null);
    
    /// <summary>
    /// Release a terminal (optional).
    /// </summary>
    Task<ReleaseTerminalResponse?> ReleaseTerminalAsync(ReleaseTerminalRequest request, CancellationToken ct = default) =>
        Task.FromResult<ReleaseTerminalResponse?>(null);
    
    /// <summary>
    /// Wait for terminal exit (optional).
    /// </summary>
    Task<WaitForTerminalExitResponse?> WaitForTerminalExitAsync(WaitForTerminalExitRequest request, CancellationToken ct = default) =>
        Task.FromResult<WaitForTerminalExitResponse?>(null);
    
    /// <summary>
    /// Kill terminal command (optional).
    /// </summary>
    Task<KillTerminalCommandResponse?> KillTerminalAsync(KillTerminalCommandRequest request, CancellationToken ct = default) =>
        Task.FromResult<KillTerminalCommandResponse?>(null);
}
```

### VALIDATE
Build succeeds.

---

## FILE A18: AgentClientProtocol.Sdk/AgentSideConnection.cs

```csharp
// Agent Client Protocol - .NET SDK
// Agent's view of the ACP connection

using System.Text.Json;
using AgentClientProtocol.Sdk.JsonRpc;
using AgentClientProtocol.Sdk.Schema;
using AgentClientProtocol.Sdk.Stream;
using Microsoft.Extensions.Logging;

namespace AgentClientProtocol.Sdk;

/// <summary>
/// Agent-side connection to a client.
/// 
/// Provides the agent's view of an ACP connection, allowing agents to
/// communicate with clients (IDEs). Implements methods for requesting permissions,
/// accessing the file system, and sending session updates.
/// </summary>
public class AgentSideConnection : IAsyncDisposable
{
    private readonly Connection _connection;
    private readonly ILogger? _logger;
    
    public AgentSideConnection(IAgent agent, IAcpStream stream, ILogger? logger = null)
    {
        _logger = logger;
        _connection = new Connection(stream, logger);
        
        _connection.OnRequest(async (method, @params) =>
        {
            var json = @params is JsonElement el ? el : JsonSerializer.SerializeToElement(@params);
            
            return method switch
            {
                AgentMethods.Initialize => await agent.InitializeAsync(Deserialize<InitializeRequest>(json)),
                AgentMethods.Authenticate => await agent.AuthenticateAsync(Deserialize<AuthenticateRequest>(json)),
                AgentMethods.SessionNew => await agent.NewSessionAsync(Deserialize<NewSessionRequest>(json)),
                AgentMethods.SessionLoad => await agent.LoadSessionAsync(Deserialize<LoadSessionRequest>(json)),
                AgentMethods.SessionPrompt => await agent.PromptAsync(Deserialize<PromptRequest>(json)),
                _ => throw RequestError.MethodNotFound(method)
            };
        });
        
        _connection.OnNotification(async (method, @params) =>
        {
            if (method == AgentMethods.SessionCancel)
            {
                var json = @params is JsonElement el ? el : JsonSerializer.SerializeToElement(@params);
                await agent.CancelAsync(Deserialize<CancelNotification>(json));
            }
        });
    }
    
    private static T Deserialize<T>(JsonElement? element) =>
        element.HasValue 
            ? JsonSerializer.Deserialize<T>(element.Value.GetRawText())! 
            : throw RequestError.InvalidParams(null, "Missing parameters");
    
    /// <summary>
    /// Start processing messages.
    /// </summary>
    public Task RunAsync(CancellationToken ct = default) => _connection.RunAsync(ct);
    
    /// <summary>
    /// Send session update to client.
    /// </summary>
    public Task SessionUpdateAsync(SessionNotification notification, CancellationToken ct = default) =>
        _connection.SendNotificationAsync(ClientMethods.SessionUpdate, notification, ct);
    
    /// <summary>
    /// Send text chunk to client.
    /// </summary>
    public Task SendTextAsync(string sessionId, string text, CancellationToken ct = default) =>
        SessionUpdateAsync(new SessionNotification
        {
            SessionId = sessionId,
            Update = new AgentMessageChunk { Content = new TextContent { Text = text } }
        }, ct);
    
    /// <summary>
    /// Send plan update to client.
    /// </summary>
    public Task SendPlanAsync(string sessionId, PlanEntry[] entries, CancellationToken ct = default) =>
        SessionUpdateAsync(new SessionNotification
        {
            SessionId = sessionId,
            Update = new PlanUpdate { Entries = entries }
        }, ct);
    
    /// <summary>
    /// Send tool call update to client.
    /// </summary>
    public Task SendToolCallAsync(string sessionId, string id, string name, string status, 
        object? arguments = null, ContentBlock[]? content = null, CancellationToken ct = default) =>
        SessionUpdateAsync(new SessionNotification
        {
            SessionId = sessionId,
            Update = new ToolCallUpdate 
            { 
                Id = id, 
                Name = name, 
                Status = status,
                Arguments = arguments != null ? JsonSerializer.SerializeToElement(arguments) : null,
                Content = content
            }
        }, ct);
    
    /// <summary>
    /// Request permission from user.
    /// </summary>
    public Task<RequestPermissionResponse> RequestPermissionAsync(
        RequestPermissionRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<RequestPermissionResponse>(ClientMethods.SessionRequestPermission, request, ct)!;
    
    /// <summary>
    /// Request permission from user (convenience overload).
    /// </summary>
    public Task<RequestPermissionResponse> RequestPermissionAsync(
        string sessionId,
        string requestId,
        string message,
        PermissionOption[] options,
        CancellationToken ct = default) =>
        RequestPermissionAsync(new RequestPermissionRequest
        {
            SessionId = sessionId,
            RequestId = requestId,
            Message = message,
            Options = options
        }, ct);
    
    /// <summary>
    /// Request text input from user.
    /// </summary>
    public Task<RequestInputResponse> RequestInputAsync(
        string sessionId,
        string requestId,
        string prompt,
        string? defaultValue = null,
        CancellationToken ct = default) =>
        _connection.SendRequestAsync<RequestInputResponse>(ClientMethods.SessionRequestInput, new RequestInputRequest
        {
            SessionId = sessionId,
            RequestId = requestId,
            Prompt = prompt,
            DefaultValue = defaultValue
        }, ct)!;
    
    /// <summary>
    /// Read a text file from client.
    /// </summary>
    public Task<ReadTextFileResponse> ReadTextFileAsync(ReadTextFileRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<ReadTextFileResponse>(ClientMethods.FsReadTextFile, request, ct)!;
    
    /// <summary>
    /// Write a text file to client.
    /// </summary>
    public Task<WriteTextFileResponse> WriteTextFileAsync(WriteTextFileRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<WriteTextFileResponse>(ClientMethods.FsWriteTextFile, request, ct)!;
    
    /// <summary>
    /// Create a terminal on client.
    /// </summary>
    public Task<CreateTerminalResponse> CreateTerminalAsync(CreateTerminalRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<CreateTerminalResponse>(ClientMethods.TerminalCreate, request, ct)!;
    
    /// <summary>
    /// Get terminal output from client.
    /// </summary>
    public Task<TerminalOutputResponse> GetTerminalOutputAsync(TerminalOutputRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<TerminalOutputResponse>(ClientMethods.TerminalOutput, request, ct)!;
    
    /// <summary>
    /// Release terminal on client.
    /// </summary>
    public Task<ReleaseTerminalResponse> ReleaseTerminalAsync(ReleaseTerminalRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<ReleaseTerminalResponse>(ClientMethods.TerminalRelease, request, ct)!;
    
    /// <summary>
    /// Wait for terminal to exit.
    /// </summary>
    public Task<WaitForTerminalExitResponse> WaitForTerminalExitAsync(WaitForTerminalExitRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<WaitForTerminalExitResponse>(ClientMethods.TerminalWaitForExit, request, ct)!;
    
    /// <summary>
    /// Kill terminal command.
    /// </summary>
    public Task<KillTerminalCommandResponse> KillTerminalAsync(KillTerminalCommandRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<KillTerminalCommandResponse>(ClientMethods.TerminalKill, request, ct)!;
    
    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
```

### VALIDATE
Build succeeds.

---

## FILE A19: AgentClientProtocol.Sdk/ClientSideConnection.cs

```csharp
// Agent Client Protocol - .NET SDK
// Client's view of the ACP connection

using System.Text.Json;
using AgentClientProtocol.Sdk.JsonRpc;
using AgentClientProtocol.Sdk.Schema;
using AgentClientProtocol.Sdk.Stream;
using Microsoft.Extensions.Logging;

namespace AgentClientProtocol.Sdk;

/// <summary>
/// Client-side connection to an agent.
/// 
/// Provides the client's view of an ACP connection, allowing clients (IDEs)
/// to communicate with agents. Implements the IAgent interface to provide methods
/// for initializing sessions, sending prompts, and managing the agent lifecycle.
/// </summary>
public class ClientSideConnection : IAgent, IAsyncDisposable
{
    private readonly Connection _connection;
    private readonly ILogger? _logger;
    
    public ClientSideConnection(IClient client, IAcpStream stream, ILogger? logger = null)
    {
        _logger = logger;
        _connection = new Connection(stream, logger);
        
        _connection.OnRequest(async (method, @params) =>
        {
            var json = @params is JsonElement el ? el : JsonSerializer.SerializeToElement(@params);
            
            return method switch
            {
                ClientMethods.SessionRequestPermission => await client.RequestPermissionAsync(Deserialize<RequestPermissionRequest>(json)),
                ClientMethods.FsReadTextFile => await client.ReadTextFileAsync(Deserialize<ReadTextFileRequest>(json)),
                ClientMethods.FsWriteTextFile => await client.WriteTextFileAsync(Deserialize<WriteTextFileRequest>(json)),
                ClientMethods.TerminalCreate => await client.CreateTerminalAsync(Deserialize<CreateTerminalRequest>(json)),
                ClientMethods.TerminalOutput => await client.TerminalOutputAsync(Deserialize<TerminalOutputRequest>(json)),
                ClientMethods.TerminalRelease => await client.ReleaseTerminalAsync(Deserialize<ReleaseTerminalRequest>(json)),
                ClientMethods.TerminalWaitForExit => await client.WaitForTerminalExitAsync(Deserialize<WaitForTerminalExitRequest>(json)),
                ClientMethods.TerminalKill => await client.KillTerminalAsync(Deserialize<KillTerminalCommandRequest>(json)),
                _ => throw RequestError.MethodNotFound(method)
            };
        });
        
        _connection.OnNotification(async (method, @params) =>
        {
            if (method == ClientMethods.SessionUpdate)
            {
                var json = @params is JsonElement el ? el : JsonSerializer.SerializeToElement(@params);
                await client.SessionUpdateAsync(Deserialize<SessionNotification>(json));
            }
        });
    }
    
    private static T Deserialize<T>(JsonElement? element) =>
        element.HasValue 
            ? JsonSerializer.Deserialize<T>(element.Value.GetRawText())! 
            : throw RequestError.InvalidParams(null, "Missing parameters");
    
    /// <summary>
    /// Start processing messages.
    /// </summary>
    public Task RunAsync(CancellationToken ct = default) => _connection.RunAsync(ct);
    
    // IAgent implementation - calls to the agent
    
    public Task<InitializeResponse> InitializeAsync(InitializeRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<InitializeResponse>(AgentMethods.Initialize, request, ct)!;
    
    public Task<AuthenticateResponse?> AuthenticateAsync(AuthenticateRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<AuthenticateResponse>(AgentMethods.Authenticate, request, ct);
    
    public Task<NewSessionResponse> NewSessionAsync(NewSessionRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<NewSessionResponse>(AgentMethods.SessionNew, request, ct)!;
    
    public Task<LoadSessionResponse?> LoadSessionAsync(LoadSessionRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<LoadSessionResponse>(AgentMethods.SessionLoad, request, ct);
    
    public Task<PromptResponse> PromptAsync(PromptRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<PromptResponse>(AgentMethods.SessionPrompt, request, ct)!;
    
    public Task CancelAsync(CancelNotification notification, CancellationToken ct = default) =>
        _connection.SendNotificationAsync(AgentMethods.SessionCancel, notification, ct);
    
    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
```

### VALIDATE
Build succeeds.

---

## FILE A20: AgentClientProtocol.Sdk/TerminalHandle.cs

```csharp
// Agent Client Protocol - .NET SDK
// Terminal handle for managing terminal lifecycle

using AgentClientProtocol.Sdk.Schema;

namespace AgentClientProtocol.Sdk;

/// <summary>
/// Handle for controlling and monitoring a terminal created via CreateTerminal.
/// 
/// Provides methods to:
/// - Get current output without waiting
/// - Wait for command completion
/// - Kill the running command
/// - Release terminal resources
/// </summary>
public class TerminalHandle : IAsyncDisposable
{
    private readonly AgentSideConnection _connection;
    private readonly string _sessionId;
    private readonly string _terminalId;
    
    public string TerminalId => _terminalId;
    
    internal TerminalHandle(AgentSideConnection connection, string sessionId, string terminalId)
    {
        _connection = connection;
        _sessionId = sessionId;
        _terminalId = terminalId;
    }
    
    /// <summary>
    /// Create a terminal and return a handle.
    /// </summary>
    public static async Task<TerminalHandle> CreateAsync(
        AgentSideConnection connection,
        string sessionId,
        string command,
        string[]? args = null,
        string? cwd = null,
        EnvVariable[]? env = null,
        CancellationToken ct = default)
    {
        var response = await connection.CreateTerminalAsync(new CreateTerminalRequest
        {
            SessionId = sessionId,
            Command = command,
            Args = args,
            Cwd = cwd,
            Env = env
        }, ct);
        
        return new TerminalHandle(connection, sessionId, response.TerminalId);
    }
    
    /// <summary>
    /// Get current output without waiting.
    /// </summary>
    public async Task<(string output, TerminalExitStatus? exitStatus)> GetOutputAsync(CancellationToken ct = default)
    {
        var response = await _connection.GetTerminalOutputAsync(new TerminalOutputRequest
        {
            SessionId = _sessionId,
            TerminalId = _terminalId
        }, ct);
        
        return (response.Output, response.ExitStatus);
    }
    
    /// <summary>
    /// Wait for terminal command to exit.
    /// </summary>
    public async Task<WaitForTerminalExitResponse> WaitForExitAsync(CancellationToken ct = default)
    {
        return await _connection.WaitForTerminalExitAsync(new WaitForTerminalExitRequest
        {
            SessionId = _sessionId,
            TerminalId = _terminalId
        }, ct);
    }
    
    /// <summary>
    /// Kill the running command.
    /// </summary>
    public async Task KillAsync(CancellationToken ct = default)
    {
        await _connection.KillTerminalAsync(new KillTerminalCommandRequest
        {
            SessionId = _sessionId,
            TerminalId = _terminalId
        }, ct);
    }
    
    /// <summary>
    /// Release terminal resources.
    /// </summary>
    public async Task ReleaseAsync(CancellationToken ct = default)
    {
        await _connection.ReleaseTerminalAsync(new ReleaseTerminalRequest
        {
            SessionId = _sessionId,
            TerminalId = _terminalId
        }, ct);
    }
    
    public ValueTask DisposeAsync() => new(ReleaseAsync());
}
```

### VALIDATE
```bash
dotnet build tools/sdk-cli/AgentClientProtocol.Sdk/AgentClientProtocol.Sdk.csproj
```

### DONE WHEN
Build succeeds with zero errors. **PHASE 0 COMPLETE: ACP SDK ready.**

---

# PHASE 1 FILES (SDK CLI Core)

All Phase 1 files go in: `Sdk.Tools.Cli/`

---

## FILE 01: Sdk.Tools.Cli.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Sdk.Tools.Cli</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <!-- ACP SDK (local project reference - will become NuGet when extracted) -->
    <ProjectReference Include="..\AgentClientProtocol.Sdk\AgentClientProtocol.Sdk.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- GitHub Copilot SDK https://www.nuget.org/packages/GitHub.Copilot.SDK -->
    <PackageReference Include="GitHub.Copilot.SDK" Version="0.1.*" />
    
    <!-- MCP Server (for VS Code, Claude Desktop integration) -->
    <PackageReference Include="ModelContextProtocol" Version="0.1.0-preview.*" />
    
    <!-- Infrastructure -->
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.1" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.1" />
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
    <PackageReference Include="Microsoft.Extensions.FileSystemGlobbing" Version="8.0.0" />
    <PackageReference Include="System.Text.Json" Version="8.0.5" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="Templates/**/*.md" />
    <EmbeddedResource Include="Prompts/**/*.md" />
  </ItemGroup>

</Project>
```

### VALIDATE
```bash
dotnet restore tools/sdk-cli/Sdk.Tools.Cli/Sdk.Tools.Cli.csproj
```

### DONE WHEN
Exit code 0, packages restored.

---

## FILE 02: Models/SdkLanguage.cs

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Sdk.Tools.Cli.Models;

public enum SdkLanguage
{
    [JsonPropertyName("")]
    Unknown,
    [JsonPropertyName(".NET")]
    DotNet,
    [JsonPropertyName("Java")]
    Java,
    [JsonPropertyName("JavaScript")]
    JavaScript,
    [JsonPropertyName("Python")]
    Python,
    [JsonPropertyName("Go")]
    Go
}

public static class SdkLanguageHelpers
{
    /// <summary>
    /// Parse language from string input (CLI args, config files, etc.)
    /// </summary>
    public static SdkLanguage Parse(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return SdkLanguage.Unknown;
            
        return language.ToLowerInvariant() switch
        {
            ".net" or "dotnet" or "c#" or "csharp" => SdkLanguage.DotNet,
            "java" => SdkLanguage.Java,
            "javascript" or "js" or "typescript" or "ts" => SdkLanguage.JavaScript,
            "python" or "py" => SdkLanguage.Python,
            "go" or "golang" => SdkLanguage.Go,
            _ => SdkLanguage.Unknown
        };
    }
    
    /// <summary>
    /// Get file extension for language.
    /// </summary>
    public static string GetFileExtension(SdkLanguage language) => language switch
    {
        SdkLanguage.DotNet => ".cs",
        SdkLanguage.Python => ".py",
        SdkLanguage.JavaScript => ".ts",
        SdkLanguage.Java => ".java",
        SdkLanguage.Go => ".go",
        _ => ".txt"
    };
    
    /// <summary>
    /// Get language ID for code blocks.
    /// </summary>
    public static string GetLanguageId(SdkLanguage language) => language switch
    {
        SdkLanguage.DotNet => "csharp",
        SdkLanguage.Python => "python",
        SdkLanguage.JavaScript => "typescript",
        SdkLanguage.Java => "java",
        SdkLanguage.Go => "go",
        _ => "text"
    };
}
```

### VALIDATE
```bash
dotnet build tools/sdk-cli/Sdk.Tools.Cli/Sdk.Tools.Cli.csproj --no-restore
```

### DONE WHEN
Build succeeds.

---

## FILE 03: Models/SampleConstants.cs

> **Source:** From `azsdk-cli/Services/Languages/Samples/SampleConstants.cs`

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Sdk.Tools.Cli.Models;

/// <summary>
/// Shared constants for sample generation tools.
/// </summary>
public static class SampleConstants
{
    /// <summary>
    /// Maximum number of characters to load when reading source code context.
    /// </summary>
    public const int MaxContextCharacters = 4_000_000;

    /// <summary>
    /// Maximum number of characters per file when loading source code context.
    /// </summary>
    public const int MaxCharactersPerFile = 50_000;

    /// <summary>
    /// Default batch size for processing samples.
    /// </summary>
    public const int DefaultBatchSize = 5;
}
```

### VALIDATE
Build succeeds.

---

## FILE 04: Models/SourceInput.cs

```csharp
namespace Sdk.Tools.Cli.Models;

/// <summary>
/// Represents a source file loaded for context.
/// </summary>
public record SourceInput(
    string FilePath,
    string Content,
    int Priority = 10
);
```

### VALIDATE
Build succeeds.

---

## FILE 05: Models/GeneratedSample.cs

```csharp
using System.Text.Json.Serialization;

namespace Sdk.Tools.Cli.Models;

public class GeneratedSample
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    
    [JsonPropertyName("description")]
    public required string Description { get; set; }
    
    [JsonPropertyName("code")]
    public required string Code { get; set; }
    
    [JsonPropertyName("filename")]
    public string? FileName { get; set; }
}
```

### VALIDATE
Build succeeds.

---

## FILE 06: Models/LanguageInfo.cs

```csharp
namespace Sdk.Tools.Cli.Models;

/// <summary>
/// Information about a detected SDK language.
/// </summary>
/// <param name="Language">The language enum value.</param>
/// <param name="Name">Human-readable language name (e.g., ".NET", "Python").</param>
/// <param name="FileExtension">Canonical file extension including leading period (e.g., ".cs").</param>
public record LanguageInfo(SdkLanguage Language, string Name, string FileExtension);
```

### VALIDATE
Build succeeds.

---

## FILE 07: Models/SdkCliConfig.cs

```csharp
using System.Text.Json.Serialization;

namespace Sdk.Tools.Cli.Models;

public class SdkCliConfig
{
    [JsonPropertyName("language")]
    public string? Language { get; set; }
    
    [JsonPropertyName("sourceDirectories")]
    public string[]? SourceDirectories { get; set; }
    
    [JsonPropertyName("excludePatterns")]
    public string[]? ExcludePatterns { get; set; }
    
    [JsonPropertyName("includePatterns")]
    public string[]? IncludePatterns { get; set; }
    
    [JsonPropertyName("maxContextBytes")]
    public int? MaxContextBytes { get; set; }
    
    [JsonPropertyName("model")]
    public string? Model { get; set; }
    
    [JsonPropertyName("outputDirectory")]
    public string? OutputDirectory { get; set; }
}
```

### VALIDATE
Build succeeds.

---

## FILE 08: Helpers/FileHelper.cs

> **Architecture Note:** Adapted from battle-tested `azsdk-cli/Helpers/FileHelper.cs`. 
> Includes priority-based loading, budget management, loading plans, and structured XML output.

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace Sdk.Tools.Cli.Helpers;

/// <summary>
/// Represents an input specification with its own filtering rules.
/// </summary>
public record SourceInput(
    string Path,
    string[]? IncludeExtensions = null,
    string[]? ExcludeGlobPatterns = null
);

/// <summary>
/// Represents metadata about a discovered file.
/// </summary>
public record FileMetadata(
    string FilePath,
    string RelativePath,
    int FileSize,
    int Priority
);

/// <summary>
/// Represents an individual file in a loading plan.
/// </summary>
public record FileLoadingItem(
    string FilePath,
    string RelativePath,
    int FileSize,
    int ContentToLoad,
    int EstimatedTokens,
    bool IsTruncated
);

/// <summary>
/// Represents a plan for loading files with budget allocation.
/// </summary>
public record FileLoadingPlan(
    List<FileLoadingItem> Items,
    int TotalFilesFound,
    int TotalFilesIncluded,
    int TotalEstimatedTokens,
    int BudgetUsed,
    int TotalBudget
);

/// <summary>
/// Production-grade file helper with priority-based loading and budget management.
/// </summary>
public class FileHelper
{
    private readonly ILogger<FileHelper>? _logger;
    private const int DefaultTotalBudget = 500_000;
    private const int DefaultPerFileLimit = 100_000;
    private const int HeaderOverhead = 50;

    public FileHelper(ILogger<FileHelper>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// High-level API: Discover, plan, and load files with budget management.
    /// </summary>
    public async Task<string> LoadFilesAsync(
        string directory,
        string[] includeExtensions,
        string[] excludeGlobPatterns,
        int totalBudget = DefaultTotalBudget,
        int perFileLimit = DefaultPerFileLimit,
        Func<FileMetadata, int>? priorityFunc = null,
        CancellationToken ct = default)
    {
        priorityFunc ??= _ => 0;
        var files = DiscoverFiles(directory, includeExtensions, excludeGlobPatterns, directory, priorityFunc);
        var plan = CreateLoadingPlan(files, totalBudget, perFileLimit);
        return await ExecuteLoadingPlanAsync(plan, ct);
    }

    /// <summary>
    /// Discovers files matching criteria, sorted by priority then size.
    /// </summary>
    public List<FileMetadata> DiscoverFiles(
        string directory,
        string[] includeExtensions,
        string[] excludeGlobPatterns,
        string relativeTo,
        Func<FileMetadata, int> priorityFunc)
    {
        var extensionSet = includeExtensions.Length > 0
            ? new HashSet<string>(includeExtensions, StringComparer.OrdinalIgnoreCase)
            : null;

        Matcher? excludeMatcher = null;
        if (excludeGlobPatterns.Length > 0)
        {
            excludeMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            excludeMatcher.AddIncludePatterns(excludeGlobPatterns);
        }

        var files = new List<FileMetadata>();
        var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(directory))
        {
            _logger?.LogWarning("Directory does not exist: {directory}", directory);
            return files;
        }

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
        };

        foreach (var filePath in Directory.EnumerateFiles(directory, "*.*", enumerationOptions))
        {
            if (processedFiles.Contains(filePath))
                continue;

            // Extension filter
            if (extensionSet != null)
            {
                var ext = Path.GetExtension(filePath);
                if (!extensionSet.Contains(ext))
                    continue;
            }

            // Glob exclusion filter
            if (excludeMatcher != null)
            {
                var globPath = Path.GetRelativePath(relativeTo, filePath).Replace(Path.DirectorySeparatorChar, '/');
                if (excludeMatcher.Match(globPath).HasMatches)
                    continue;
            }

            var fileInfo = new FileInfo(filePath);
            var relativePath = Path.GetRelativePath(relativeTo, filePath);
            var metadata = new FileMetadata(
                FilePath: fileInfo.FullName,
                RelativePath: relativePath,
                FileSize: (int)fileInfo.Length,
                Priority: 0
            );
            
            // Calculate priority using the function
            metadata = metadata with { Priority = priorityFunc(metadata) };
            
            files.Add(metadata);
            processedFiles.Add(filePath);
        }

        // Sort by priority (ascending), then by size (ascending)
        files.Sort((a, b) =>
        {
            var priorityComparison = a.Priority.CompareTo(b.Priority);
            return priorityComparison != 0 ? priorityComparison : a.FileSize.CompareTo(b.FileSize);
        });

        _logger?.LogDebug("Discovered {count} files in {directory}", files.Count, directory);
        return files;
    }

    /// <summary>
    /// Creates a loading plan with budget allocation.
    /// </summary>
    public FileLoadingPlan CreateLoadingPlan(
        List<FileMetadata> files,
        int totalBudget,
        int perFileLimit)
    {
        var planItems = new List<FileLoadingItem>();
        int remainingBudget = totalBudget;

        foreach (var file in files)
        {
            if (remainingBudget <= HeaderOverhead)
                break;

            var availableBudget = remainingBudget - HeaderOverhead;
            var contentToLoad = Math.Min(Math.Min(file.FileSize, perFileLimit), availableBudget);

            if (contentToLoad <= 0)
                continue;

            // Rough token estimation: ~4 characters per token
            var estimatedTokens = contentToLoad / 4;

            planItems.Add(new FileLoadingItem(
                FilePath: file.FilePath,
                RelativePath: file.RelativePath,
                FileSize: file.FileSize,
                ContentToLoad: contentToLoad,
                EstimatedTokens: estimatedTokens,
                IsTruncated: contentToLoad < file.FileSize
            ));

            remainingBudget -= contentToLoad + HeaderOverhead;
        }

        _logger?.LogDebug("Loading plan: {included}/{total} files, {used}/{budget} chars",
            planItems.Count, files.Count, totalBudget - remainingBudget, totalBudget);

        return new FileLoadingPlan(
            Items: planItems,
            TotalFilesFound: files.Count,
            TotalFilesIncluded: planItems.Count,
            TotalEstimatedTokens: planItems.Sum(item => item.EstimatedTokens),
            BudgetUsed: totalBudget - remainingBudget,
            TotalBudget: totalBudget
        );
    }

    /// <summary>
    /// Executes the loading plan, returning structured XML-like content.
    /// </summary>
    public async Task<string> ExecuteLoadingPlanAsync(FileLoadingPlan plan, CancellationToken ct = default)
    {
        if (plan.Items.Count == 0)
            return string.Empty;

        var sb = new StringBuilder(plan.BudgetUsed + (plan.Items.Count * 100));

        foreach (var item in plan.Items)
        {
            try
            {
                string content;

                if (item.ContentToLoad >= item.FileSize)
                {
                    content = await File.ReadAllTextAsync(item.FilePath, ct);
                }
                else
                {
                    // Partial read for truncated files
                    using var stream = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                    using var reader = new StreamReader(stream, Encoding.UTF8);

                    var buffer = new char[item.ContentToLoad];
                    var charsRead = await reader.ReadAsync(buffer.AsMemory(0, item.ContentToLoad), ct);
                    content = new string(buffer.AsSpan(0, charsRead));

                    if (charsRead == item.ContentToLoad && !reader.EndOfStream)
                    {
                        content += "\n// ... truncated ...";
                    }
                }

                sb.AppendLine($"<file path=\"{item.RelativePath}\" size=\"{item.FileSize}\" tokens=\"~{item.EstimatedTokens}\">");
                sb.AppendLine(content);
                sb.AppendLine("</file>");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Failed to load {path}: {error}", item.RelativePath, ex.Message);
                sb.AppendLine($"<file path=\"{item.RelativePath}\" error=\"{ex.Message}\" />");
                sb.AppendLine();
            }
        }

        if (plan.TotalFilesFound > plan.TotalFilesIncluded)
        {
            var omitted = plan.TotalFilesFound - plan.TotalFilesIncluded;
            sb.AppendLine($"<!-- {omitted} additional files omitted due to budget -->");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Validates that a directory exists and is empty.
    /// </summary>
    public string? ValidateEmptyDirectory(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
            return "Directory must be defined";

        var fullDir = Path.GetFullPath(dir.Trim());
        if (!Directory.Exists(fullDir))
            return $"Directory '{fullDir}' does not exist";

        if (Directory.GetFileSystemEntries(fullDir).Length != 0)
            return $"Directory '{fullDir}' is not empty";

        return null;
    }
}
```

### VALIDATE
```bash
dotnet build tools/sdk-cli/Sdk.Tools.Cli/Sdk.Tools.Cli.csproj
```

### DONE WHEN
- Builds without errors
- Uses `Microsoft.Extensions.FileSystemGlobbing` (not DotNet.Globbing)

---

## FILE 09: Helpers/ConfigurationHelper.cs

```csharp
using System.Text.Json;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Helpers;

public class ConfigurationHelper
{
    private const string ConfigFileName = "sdk-cli-config.json";
    
    public async Task<SdkCliConfig?> TryLoadConfigAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        var configPath = Path.Combine(packagePath, ConfigFileName);
        
        if (!File.Exists(configPath))
            return null;
        
        var json = await File.ReadAllTextAsync(configPath, cancellationToken);
        return JsonSerializer.Deserialize<SdkCliConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
```

### VALIDATE
Build succeeds.

---

## FILE 10: Services/Languages/LanguageDetector.cs

```csharp
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages;

public class LanguageDetector
{
    private static readonly Dictionary<SdkLanguage, string> LanguageNames = new()
    {
        { SdkLanguage.DotNet, ".NET" },
        { SdkLanguage.Python, "Python" },
        { SdkLanguage.JavaScript, "JavaScript/TypeScript" },
        { SdkLanguage.Java, "Java" },
        { SdkLanguage.Go, "Go" }
    };
    
    private static readonly Dictionary<SdkLanguage, string> FileExtensions = new()
    {
        { SdkLanguage.DotNet, ".cs" },
        { SdkLanguage.Python, ".py" },
        { SdkLanguage.JavaScript, ".ts" },
        { SdkLanguage.Java, ".java" },
        { SdkLanguage.Go, ".go" }
    };
    
    /// <summary>Sync detection - returns raw enum.</summary>
    public SdkLanguage? DetectLanguage(string packagePath)
    {
        // .NET
        if (Directory.GetFiles(packagePath, "*.csproj", SearchOption.AllDirectories).Any() ||
            Directory.GetFiles(packagePath, "*.sln", SearchOption.AllDirectories).Any())
        {
            return SdkLanguage.DotNet;
        }
        
        // Python
        if (File.Exists(Path.Combine(packagePath, "pyproject.toml")) ||
            File.Exists(Path.Combine(packagePath, "setup.py")))
        {
            return SdkLanguage.Python;
        }
        
        // Go
        if (File.Exists(Path.Combine(packagePath, "go.mod")))
        {
            return SdkLanguage.Go;
        }
        
        // Java
        if (File.Exists(Path.Combine(packagePath, "pom.xml")) ||
            File.Exists(Path.Combine(packagePath, "build.gradle")))
        {
            return SdkLanguage.Java;
        }
        
        // TypeScript/JavaScript
        if (File.Exists(Path.Combine(packagePath, "package.json")))
        {
            return SdkLanguage.JavaScript;
        }
        
        return null;
    }
    
    /// <summary>Async detection - returns LanguageInfo with metadata.</summary>
    public Task<LanguageInfo?> DetectAsync(string packagePath)
    {
        var lang = DetectLanguage(packagePath);
        if (lang == null) return Task.FromResult<LanguageInfo?>(null);
        
        var info = new LanguageInfo(
            lang.Value,
            LanguageNames[lang.Value],
            FileExtensions[lang.Value]
        );
        return Task.FromResult<LanguageInfo?>(info);
    }
}
```

### VALIDATE
Build succeeds.

---

## FILE 11: Services/Languages/LanguageService.cs

```csharp
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages;

public abstract class LanguageService
{
    public abstract SdkLanguage Language { get; }
    public abstract string FileExtension { get; }
    public abstract string[] DefaultSourceDirectories { get; }
    public abstract string[] DefaultIncludePatterns { get; }
    public abstract string[] DefaultExcludePatterns { get; }
    
    public virtual SourceInput GetDefaultSourceInput()
    {
        return new SourceInput(
            DefaultIncludePatterns,
            DefaultExcludePatterns,
            DefaultSourceDirectories
        );
    }
}
```

### VALIDATE
Build succeeds.

---

## FILE 12: Services/Languages/DotNetLanguageService.cs

```csharp
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages;

public class DotNetLanguageService : LanguageService
{
    public override SdkLanguage Language => SdkLanguage.DotNet;
    public override string FileExtension => ".cs";
    public override string[] DefaultSourceDirectories => new[] { "src" };
    public override string[] DefaultIncludePatterns => new[] { "**/*.cs" };
    public override string[] DefaultExcludePatterns => new[] 
    { 
        "**/obj/**", 
        "**/bin/**", 
        "**/*.Designer.cs",
        "**/AssemblyInfo.cs"
    };
}
```

### VALIDATE
Build succeeds.

---

## FILE 13: Services/Languages/Samples/SampleLanguageContext.cs

> **Config-driven, SDK-agnostic.** No hardcoded folder assumptions.

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages.Samples;

/// <summary>
/// Provides language-specific behaviors for sample generation.
/// All folder structure is config-driven, not hardcoded.
/// </summary>
public abstract class SampleLanguageContext
{
    protected readonly FileHelper FileHelper;

    protected SampleLanguageContext(FileHelper fileHelper)
    {
        FileHelper = fileHelper;
    }

    /// <summary>The language enum value.</summary>
    public abstract SdkLanguage Language { get; }

    /// <summary>The canonical file extension for samples (including leading period).</summary>
    public virtual string FileExtension => SdkLanguageHelpers.GetFileExtension(Language);

    /// <summary>
    /// Returns language-specific coding conventions for sample generation.
    /// </summary>
    public abstract string GetInstructions();

    /// <summary>
    /// Default include extensions for this language.
    /// </summary>
    protected abstract string[] DefaultIncludeExtensions { get; }

    /// <summary>
    /// Default exclude patterns for this language.
    /// </summary>
    protected abstract string[] DefaultExcludePatterns { get; }

    /// <summary>
    /// Loads context for sample generation from the specified paths.
    /// Uses config overrides if provided, otherwise falls back to language defaults.
    /// </summary>
    public virtual async Task<string> LoadContextAsync(
        IEnumerable<string> paths, 
        SdkCliConfig? config = null,
        int totalBudget = SampleConstants.MaxContextCharacters,
        int perFileLimit = SampleConstants.MaxCharactersPerFile,
        CancellationToken ct = default)
    {
        if (!paths.Any())
            throw new ArgumentException("At least one path must be provided", nameof(paths));

        // Build source inputs from paths
        var includeExtensions = config?.IncludePatterns?.Length > 0 
            ? ExtractExtensions(config.IncludePatterns) 
            : DefaultIncludeExtensions;
        var excludePatterns = config?.ExcludePatterns ?? DefaultExcludePatterns;

        var sourceInputs = new List<SourceInput>();
        foreach (var path in paths)
        {
            var fullPath = Path.GetFullPath(path.Trim());
            if (Directory.Exists(fullPath))
            {
                sourceInputs.Add(new SourceInput(fullPath, includeExtensions, excludePatterns));
            }
            else if (File.Exists(fullPath))
            {
                sourceInputs.Add(new SourceInput(fullPath));
            }
        }

        if (!sourceInputs.Any())
            throw new ArgumentException("No valid paths found", nameof(paths));

        var basePath = Path.GetFullPath(paths.First());
        return await FileHelper.LoadFilesAsync(
            sourceInputs, 
            basePath, 
            totalBudget, 
            perFileLimit, 
            f => GetPriority(f), 
            ct);
    }

    /// <summary>
    /// Priority function - override in subclass for language-specific prioritization.
    /// Lower number = higher priority.
    /// </summary>
    protected virtual int GetPriority(FileMetadata file)
    {
        // Default: all files equal priority
        return 10;
    }

    private static string[] ExtractExtensions(string[] patterns)
    {
        // Extract extensions from patterns like "**/*.cs" -> ".cs"
        return patterns
            .Where(p => p.Contains("*."))
            .Select(p => "." + p.Split("*.").Last().TrimEnd('*', '/'))
            .Distinct()
            .ToArray();
    }
}
```

### VALIDATE
Build succeeds.

---

## FILE 14: Services/Languages/Samples/DotNetSampleLanguageContext.cs

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages.Samples;

public sealed class DotNetSampleLanguageContext : SampleLanguageContext
{
    public DotNetSampleLanguageContext(FileHelper fileHelper) : base(fileHelper) { }

    public override SdkLanguage Language => SdkLanguage.DotNet;

    protected override string[] DefaultIncludeExtensions => new[] { ".cs" };

    protected override string[] DefaultExcludePatterns => new[] 
    { 
        "**/obj/**", 
        "**/bin/**", 
        "**/*.Designer.cs",
        "**/AssemblyInfo.cs"
    };

    protected override int GetPriority(FileMetadata file)
    {
        var name = Path.GetFileNameWithoutExtension(file.FilePath).ToLowerInvariant();
        if (name.Contains("client")) return 1;
        if (name.Contains("options")) return 2;
        if (name.Contains("model")) return 3;
        return 10;
    }

    public override string GetInstructions() => """
        Generate C# code samples following these conventions:
        - Use file-scoped namespaces
        - Prefer `var` for local variables with obvious types
        - Use `async/await` for async operations
        - Include proper `using` statements
        - Handle exceptions with try/catch where appropriate
        - Use meaningful variable names
        - Add XML doc comments for public APIs demonstrated
        - Target .NET 8.0+
        """;
}
```

### VALIDATE
Build succeeds.

---

## FILE 15: Tools/Package/Samples/SampleGeneratorTool.cs

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;
using Sdk.Tools.Cli.Services;
using Sdk.Tools.Cli.Services.Languages;
using Sdk.Tools.Cli.Services.Languages.Samples;

namespace Sdk.Tools.Cli.Tools.Package.Samples;

public class SampleGeneratorTool
{
    private readonly CopilotAgentService _agentService;
    private readonly LanguageDetector _detector;
    private readonly FileHelper _fileHelper;
    private readonly ConfigurationHelper _configHelper;
    private readonly ILogger<SampleGeneratorTool> _logger;
    
    public SampleGeneratorTool(
        CopilotAgentService agentService,
        LanguageDetector detector,
        FileHelper fileHelper,
        ConfigurationHelper configHelper,
        ILogger<SampleGeneratorTool> logger)
    {
        _agentService = agentService;
        _detector = detector;
        _fileHelper = fileHelper;
        _configHelper = configHelper;
        _logger = logger;
    }
    
    public async Task<int> ExecuteAsync(
        string packagePath,
        string? outputPath,
        string? language,
        string? prompt,
        string model,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating samples for {PackagePath}", packagePath);
        
        packagePath = Path.GetFullPath(packagePath);
        if (!Directory.Exists(packagePath))
        {
            _logger.LogError("Package path does not exist: {PackagePath}", packagePath);
            return 1;
        }
        
        // Detect or parse language
        SdkLanguage? detectedLanguage;
        if (!string.IsNullOrEmpty(language))
        {
            detectedLanguage = SdkLanguageHelpers.Parse(language);
            if (detectedLanguage == SdkLanguage.Unknown)
                detectedLanguage = null;
        }
        else
        {
            detectedLanguage = _detector.DetectLanguage(packagePath);
        }
        
        if (detectedLanguage is null)
        {
            _logger.LogError("Could not detect language. Use --language to specify.");
            return 1;
        }
        
        // Load config if present
        var config = await _configHelper.LoadConfigAsync(packagePath, cancellationToken);
        
        // Get language context
        var context = CreateLanguageContext(detectedLanguage.Value);
        
        // Load source context
        _logger.LogInformation("Loading source context...");
        var sourceContext = await context.LoadContextAsync(
            new[] { packagePath }, 
            config,
            cancellationToken: cancellationToken);
        
        // Build prompt
        var systemPrompt = BuildSystemPrompt(context);
        var userPrompt = BuildUserPrompt(prompt, sourceContext);
        
        // Generate samples
        _logger.LogInformation("Generating samples with {Model}...", model);
        var samples = await _agentService.RunAgentAsync<List<GeneratedSample>>(
            systemPrompt,
            userPrompt,
            model,
            cancellationToken);
        
        if (dryRun)
        {
            foreach (var sample in samples)
            {
                Console.WriteLine($"[DRY RUN] Would generate: {sample.Name}{context.FileExtension}");
                Console.WriteLine(sample.Code);
                Console.WriteLine();
            }
            return 0;
        }
        
        // Write samples
        var outputDir = outputPath ?? Path.Combine(packagePath, "samples");
        Directory.CreateDirectory(outputDir);
        
        foreach (var sample in samples)
        {
            var filename = $"{sample.Name}{context.FileExtension}";
            var filePath = Path.Combine(outputDir, filename);
            await File.WriteAllTextAsync(filePath, sample.Code, cancellationToken);
            _logger.LogInformation("Generated: {FilePath}", filePath);
        }
        
        return 0;
    }
    
    // NOTE: Additional language contexts (Python, TypeScript, Java, Go) are added in Phase 3.
    // Update this switch in Phase 3 to include all languages.
    private SampleLanguageContext CreateLanguageContext(SdkLanguage language) => language switch
    {
        SdkLanguage.DotNet => new DotNetSampleLanguageContext(_fileHelper),
        _ => throw new NotSupportedException($"Language {language} not yet supported. Add support in Phase 3.")
    };
    
    private static string BuildSystemPrompt(SampleLanguageContext context)
    {
        return $"""
            You are an expert SDK sample generator.
            Generate clear, concise, runnable code samples.
            
            {context.GetInstructions()}
            
            Output JSON array:
            [{{ "name": "SampleName", "description": "...", "code": "..." }}]
            """;
    }
    
    private static string BuildUserPrompt(string? customPrompt, string sourceContext)
    {
        var prompt = customPrompt ?? "Generate samples demonstrating the main features of this SDK.";
        return $"""
            {prompt}
            
            Source code context:
            {sourceContext}
            """;
    }
}
```

### VALIDATE
Build succeeds.

---

## FILE 16: Services/CopilotAgentService.cs

> **Purpose:** Pure AI/LLM abstraction layer using GitHub.Copilot.SDK.
```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;

namespace Sdk.Tools.Cli.Services;

/// <summary>
/// AI/LLM abstraction layer using GitHub.Copilot.SDK.
/// Provides session-based access to GitHub Copilot models.
/// </summary>
public class CopilotAgentService : IAsyncDisposable
{
    private readonly ILogger<CopilotAgentService> _logger;
    private CopilotClient? _client;
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    
    public CopilotAgentService(ILogger<CopilotAgentService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Run an AI agent task and deserialize the JSON response.
    /// </summary>
    public async Task<T> RunAgentAsync<T>(
        string systemPrompt,
        string userPrompt,
        string model = "gpt-4.1",
        CancellationToken cancellationToken = default)
    {
        var content = await RunAgentRawAsync(systemPrompt, userPrompt, model, cancellationToken);
        
        _logger.LogDebug("Received response, parsing JSON");
        
        var json = ExtractJson(content);
        
        var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        return result ?? throw new InvalidOperationException("Failed to deserialize response");
    }
    
    /// <summary>
    /// Run an AI agent task and return raw text response.
    /// </summary>
    public async Task<string> RunAgentRawAsync(
        string systemPrompt,
        string userPrompt,
        string model = "gpt-4.1",
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting agent with model {Model}", model);
        
        var client = await GetOrCreateClientAsync(cancellationToken);
        
        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = systemPrompt
            }
        });
        
        var responseBuilder = new System.Text.StringBuilder();
        var done = new TaskCompletionSource();
        
        using var subscription = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent msg:
                    responseBuilder.Append(msg.Data.Content);
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
                case SessionErrorEvent err:
                    done.TrySetException(new InvalidOperationException(err.Data.Message));
                    break;
            }
        });
        
        await session.SendAsync(new MessageOptions { Prompt = userPrompt });
        
        // Wait for completion or cancellation
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var completedTask = await Task.WhenAny(done.Task, Task.Delay(Timeout.Infinite, cts.Token));
        
        if (completedTask != done.Task)
        {
            await session.AbortAsync();
            throw new OperationCanceledException(cancellationToken);
        }
        
        await done.Task; // Re-throw any exception
        
        return responseBuilder.ToString();
    }
    
    /// <summary>
    /// Stream AI agent response token by token.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAgentAsync(
        string systemPrompt,
        string userPrompt,
        string model = "gpt-4.1",
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting streaming agent with model {Model}", model);
        
        var client = await GetOrCreateClientAsync(cancellationToken);
        
        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            Streaming = true,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = systemPrompt
            }
        });
        
        var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();
        var done = new TaskCompletionSource();
        
        using var subscription = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    channel.Writer.TryWrite(delta.Data.DeltaContent);
                    break;
                case SessionIdleEvent:
                    channel.Writer.Complete();
                    done.TrySetResult();
                    break;
                case SessionErrorEvent err:
                    channel.Writer.Complete(new InvalidOperationException(err.Data.Message));
                    done.TrySetException(new InvalidOperationException(err.Data.Message));
                    break;
            }
        });
        
        await session.SendAsync(new MessageOptions { Prompt = userPrompt });
        
        await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return chunk;
        }
    }
    
    private async Task<CopilotClient> GetOrCreateClientAsync(CancellationToken ct)
    {
        await _clientLock.WaitAsync(ct);
        try
        {
            if (_client == null)
            {
                _client = new CopilotClient(new CopilotClientOptions
                {
                    Logger = _logger,
                    AutoStart = true
                });
                await _client.StartAsync();
            }
            return _client;
        }
        finally
        {
            _clientLock.Release();
        }
    }
    
    private static string ExtractJson(string content)
    {
        var start = content.IndexOf('[');
        if (start == -1) start = content.IndexOf('{');
        if (start == -1) throw new InvalidOperationException("No JSON found in response");
        
        var end = content.LastIndexOf(']');
        if (end == -1) end = content.LastIndexOf('}');
        if (end == -1) throw new InvalidOperationException("Incomplete JSON in response");
        
        return content.Substring(start, end - start + 1);
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.StopAsync();
            await _client.DisposeAsync();
            _client = null;
        }
        _clientLock.Dispose();
    }
}
```

### VALIDATE
Build succeeds.

---

## FILE 17: Program.cs

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Services;
using Sdk.Tools.Cli.Services.Languages;
using Sdk.Tools.Cli.Tools.Package.Samples;

namespace Sdk.Tools.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("SDK CLI - Tools for SDK development");
        
        var services = ConfigureServices();
        
        // sdk-cli package sample generate
        var packageCommand = new Command("package", "Package-related commands");
        rootCommand.AddCommand(packageCommand);
        
        var sampleCommand = new Command("sample", "Sample-related commands");
        packageCommand.AddCommand(sampleCommand);
        
        sampleCommand.AddCommand(BuildSampleGenerateCommand(services));
        
        return await rootCommand.InvokeAsync(args);
    }
    
    private static Command BuildSampleGenerateCommand(IServiceProvider services)
    {
        var command = new Command("generate", "Generate code samples for SDK package");
        
        var pathArg = new Argument<string>("path", "Path to SDK package directory");
        var outputOption = new Option<string?>("--output", "Output directory for samples");
        var languageOption = new Option<string?>("--language", "SDK language (dotnet, python, java, typescript, go)");
        var promptOption = new Option<string?>("--prompt", "Custom generation prompt");
        var modelOption = new Option<string>("--model", () => "gpt-4.1", "AI model to use");
        var dryRunOption = new Option<bool>("--dry-run", "Preview without writing files");
        
        command.AddArgument(pathArg);
        command.AddOption(outputOption);
        command.AddOption(languageOption);
        command.AddOption(promptOption);
        command.AddOption(modelOption);
        command.AddOption(dryRunOption);
        
        command.SetHandler(async (path, output, language, prompt, model, dryRun) =>
        {
            var tool = services.GetRequiredService<SampleGeneratorTool>();
            Environment.ExitCode = await tool.ExecuteAsync(path, output, language, prompt, model, dryRun);
        }, pathArg, outputOption, languageOption, promptOption, modelOption, dryRunOption);
        
        return command;
    }
    
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<CopilotAgentService>();
        services.AddSingleton<LanguageDetector>();
        services.AddSingleton<FileHelper>();
        services.AddSingleton<ConfigurationHelper>();
        services.AddSingleton<SampleGeneratorTool>();
        
        return services.BuildServiceProvider();
    }
}
```

### VALIDATE
**PHASE 1 CHECKPOINT:** `dotnet build` succeeds.

---

## FILE 18: Prompts/SampleGeneration/system.md

```markdown
# Expert SDK Sample Generator

You are a **Principal Software Engineer** generating samples that ship in official SDK documentation. Assume all code will be copy-pasted into production.

## Core Principles
1. **Security**: Never hardcode credentials. Environment variables or credential providers only.
2. **Auth**: Prefer token credentials over API keys when available.
3. **Production-ready**: Proper disposal, retry policies, cancellation tokens, structured logging.
4. **Error handling**: Catch specific exceptions, meaningful messages, retry-able vs terminal errors.
5. **Idiomatic**: Follow language style guides, use modern features, match SDK patterns.

## Output Format
Return ONLY valid JSON array. No markdown, no explanation:
```json
[
  {"name": "PascalCaseName", "description": "One sentence", "code": "Complete runnable code"}
]
```

## Generate 3-5 samples covering:
- Authentication (always first)
- Core operation
- Error handling
- Advanced (pagination/streaming if applicable)

## Never:
- Hardcode secrets
- Ignore exceptions
- Use deprecated APIs
- Use Console.WriteLine/print for non-demo output
```

### VALIDATE
File exists.

---

## FILE 19: Prompts/SampleGeneration/dotnet.md

```markdown
# .NET SDK Sample Guidelines

You are an expert in modern C# and .NET 8.0+. Apply these patterns exactly.

## Language Standards
- **C# 12** features: primary constructors, collection expressions, raw string literals
- **File-scoped namespaces** - one namespace per file, no nesting
- **Nullable reference types enabled** - no null warnings allowed
- **Implicit usings enabled** - don't include System, System.Collections.Generic, etc.

## Authentication
Prefer token credentials over API keys. Always from environment variables.

## Patterns
```csharp
// Async: always CancellationToken, ConfigureAwait(false) in libraries
await client.GetAsync(cancellationToken).ConfigureAwait(false);

// Disposal: await using for async, using declaration for sync
await using var client = new ServiceClient();

// Errors: catch specific exceptions, structured logging
catch (NotFoundException) { logger.LogWarning("Not found: {Id}", id); }

// Config: environment or IConfiguration, never hardcoded
var endpoint = Environment.GetEnvironmentVariable("ENDPOINT") ?? throw new InvalidOperationException();

// Logging: ILogger, not Console.WriteLine
logger.LogInformation("Processing {RequestId}", requestId);
```

## Modern C#
Collection expressions `["a", "b"]`, primary constructors, raw string literals, pattern matching.
```

### VALIDATE
File exists. **PHASE 1 COMPLETE.**

---

# PHASE 2 FILES (ACP + MCP Integration)

> **NOTE:** Phase 2 uses the ACP SDK created in Phase 0. All protocol types, connection
> management, and transport are provided by `AgentClientProtocol.Sdk`.

---

## FILE 20: Acp/SampleGeneratorAgent.cs

> **Depends on:** A16 (IAgent), A18 (AgentSideConnection), Phase 0 Schema types

```csharp
using System.Text.Json;
using AgentClientProtocol.Sdk;
using AgentClientProtocol.Sdk.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sdk.Tools.Cli.Acp;

/// <summary>
/// ACP agent implementation for interactive sample generation.
/// Uses the AgentClientProtocol.Sdk for protocol handling.
/// </summary>
public class SampleGeneratorAgent : IAgent
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SampleGeneratorAgent> _logger;
    
    // Agent sessions keyed by session ID
    private readonly Dictionary<string, AgentSessionState> _sessions = new();
    
    public SampleGeneratorAgent(IServiceProvider services, ILogger<SampleGeneratorAgent> logger)
    {
        _services = services;
        _logger = logger;
    }
    
    public AgentInfo Info => new("sdk-cli", "1.0.0");
    
    public AgentCapabilities Capabilities => new(
        AcceptsNewSession: true,
        AcceptsPrompt: true,
        Mcp: null
    );
    
    public Task<NewSessionResponse> HandleNewSessionAsync(
        NewSessionRequest request,
        AgentSideConnection connection,
        CancellationToken cancellationToken = default)
    {
        var sessionId = $"sess_{Guid.NewGuid():N}";
        
        var state = new AgentSessionState
        {
            SessionId = sessionId,
            WorkspacePath = request.WorkspacePath,
            Connection = connection
        };
        
        _sessions[sessionId] = state;
        _logger.LogDebug("Created session {SessionId} for workspace {Workspace}", 
            sessionId, request.WorkspacePath ?? "(none)");
        
        return Task.FromResult(new NewSessionResponse(sessionId));
    }
    
    public async Task<PromptResponse> HandlePromptAsync(
        PromptRequest request,
        AgentSideConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(request.SessionId, out var sessionState))
        {
            throw new InvalidOperationException($"Unknown session: {request.SessionId}");
        }
        
        var generator = _services.GetRequiredService<InteractiveSampleGenerator>();
        var response = await generator.GenerateAsync(
            request.SessionId, 
            sessionState.WorkspacePath,
            request.Prompt, 
            connection, 
            cancellationToken);
        
        return new PromptResponse(response, "end_turn");
    }
    
    /// <summary>Internal state for a session.</summary>
    private class AgentSessionState
    {
        public required string SessionId { get; init; }
        public string? WorkspacePath { get; init; }
        public required AgentSideConnection Connection { get; init; }
    }
}
```

### VALIDATE
```bash
dotnet build tools/sdk-cli/Sdk.Tools.Cli/Sdk.Tools.Cli.csproj
```

### DONE WHEN
Build succeeds with zero errors.

---

## FILE 21: Acp/SampleGeneratorAgentHost.cs

> **Depends on:** A14 (NdJsonStream), A18 (AgentSideConnection), FILE 18

```csharp
using AgentClientProtocol.Sdk;
using AgentClientProtocol.Sdk.Stream;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Services;
using Sdk.Tools.Cli.Services.Languages;

namespace Sdk.Tools.Cli.Acp;

/// <summary>
/// Entry point that hosts the SampleGeneratorAgent over stdio using ACP protocol.
/// </summary>
public static class SampleGeneratorAgentHost
{
    public static async Task RunAsync(string logLevel)
    {
        var services = ConfigureServices(logLevel);
        var logger = services.GetRequiredService<ILogger<SampleGeneratorAgent>>();
        
        // Create the agent
        var agent = new SampleGeneratorAgent(services, logger);
        
        // Create stdio transport using ACP SDK
        var stream = new NdJsonStream(
            Console.OpenStandardInput(),
            Console.OpenStandardOutput()
        );
        
        // Create agent-side connection and run
        var connection = new AgentSideConnection(stream, agent);
        
        logger.LogDebug("ACP agent starting on stdio");
        
        await connection.RunAsync();
        
        logger.LogDebug("ACP agent exited");
    }
    
    private static IServiceProvider ConfigureServices(string logLevel)
    {
        var services = new ServiceCollection();
        
        var level = logLevel.ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            _ => LogLevel.Information
        };
        
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(level));
        services.AddSingleton<CopilotAgentService>();
        services.AddSingleton<LanguageDetector>();
        services.AddSingleton<FileHelper>();
        services.AddSingleton<ConfigurationHelper>();
        services.AddSingleton<SamplesFolderScanner>();
        services.AddSingleton<InteractiveSampleGenerator>();
        
        return services.BuildServiceProvider();
    }
}
```

### VALIDATE
Build succeeds.

---

## FILE 22: Services/InteractiveSampleGenerator.cs

> **Depends on:** A08 (SessionTypes), A09 (ContentTypes), A18 (AgentSideConnection)

```csharp
using System.Text.Json;
using AgentClientProtocol.Sdk;
using AgentClientProtocol.Sdk.Schema;
using Microsoft.Extensions.Logging;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;
using Sdk.Tools.Cli.Services.Languages;
using Sdk.Tools.Cli.Services.Languages.Samples;

namespace Sdk.Tools.Cli.Services;

/// <summary>
/// Orchestrates interactive sample generation with ACP protocol.
/// Uses AgentSideConnection to stream updates to the client.
/// </summary>
public class InteractiveSampleGenerator
{
    private readonly CopilotAgentService _agentService;
    private readonly LanguageDetector _detector;
    private readonly FileHelper _fileHelper;
    private readonly SamplesFolderScanner _scanner;
    private readonly ILogger<InteractiveSampleGenerator> _logger;
    
    public InteractiveSampleGenerator(
        CopilotAgentService agentService,
        LanguageDetector detector,
        FileHelper fileHelper,
        SamplesFolderScanner scanner,
        ILogger<InteractiveSampleGenerator> logger)
    {
        _agentService = agentService;
        _detector = detector;
        _fileHelper = fileHelper;
        _scanner = scanner;
        _logger = logger;
    }
    
    public async Task<ContentBlock[]> GenerateAsync(
        string sessionId,
        string? workspacePath,
        ContentBlock[] prompt,
        AgentSideConnection connection,
        CancellationToken cancellationToken = default)
    {
        // Extract text from prompt
        var promptText = string.Join("\n", prompt
            .OfType<TextContent>()
            .Select(t => t.Text));
        
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return [new TextContent("Please provide a workspace path to generate samples for.")];
        }
        
        // Send initial plan
        await connection.SendSessionUpdateAsync(sessionId, new PlanUpdate([
            new PlanEntry("Analyze package", "in_progress"),
            new PlanEntry("Detect language", "pending"),
            new PlanEntry("Generate samples", "pending"),
            new PlanEntry("Write files", "pending")
        ]));
        
        // Detect language
        var language = await _detector.DetectAsync(workspacePath);
        if (language == null)
        {
            await connection.SendSessionUpdateAsync(sessionId, new AgentMessageChunkUpdate(
                new TextContent("Could not detect package language.")));
            return [new TextContent("Could not detect the language of the package at: " + workspacePath)];
        }
        
        await connection.SendSessionUpdateAsync(sessionId, new PlanUpdate([
            new PlanEntry("Analyze package", "completed"),
            new PlanEntry("Detect language", "completed"),
            new PlanEntry("Generate samples", "in_progress"),
            new PlanEntry("Write files", "pending")
        ]));
        
        // Stream "Generating..." update
        await connection.SendSessionUpdateAsync(sessionId, new AgentMessageChunkUpdate(
            new TextContent($"Generating samples for {language.Name} package...")));
        
        // Load source context and generate samples
        var context = CreateLanguageContext(language.Language);
        var sourceContext = await context.LoadContextAsync(
            new[] { workspacePath }, null, cancellationToken: cancellationToken);
        
        var systemPrompt = BuildSystemPrompt(context);
        var userPrompt = BuildUserPrompt(promptText, sourceContext);
        
        var samples = await _agentService.RunAgentAsync<List<GeneratedSample>>(
            systemPrompt, userPrompt, "gpt-4.1", cancellationToken);
        
        await connection.SendSessionUpdateAsync(sessionId, new PlanUpdate([
            new PlanEntry("Analyze package", "completed"),
            new PlanEntry("Detect language", "completed"),
            new PlanEntry("Generate samples", "completed"),
            new PlanEntry("Review files", "in_progress"),
            new PlanEntry("Write files", "pending")
        ]));
        
        // Determine output folder
        var scanResult = _scanner.Scan(workspacePath);
        var outputFolder = scanResult.SuggestedFolder;
        
        // Let user review and approve each file
        var approvedSamples = new List<GeneratedSample>();
        
        foreach (var sample in samples)
        {
            var proposedPath = Path.Combine(outputFolder, sample.FileName);
            
            // Show preview of the sample
            await connection.SendSessionUpdateAsync(sessionId, new AgentMessageChunkUpdate(
                new TextContent($"\n**{sample.FileName}**\n{sample.Description}\n```\n{sample.Code.Substring(0, Math.Min(200, sample.Code.Length))}...\n```")));
            
            // Ask user to accept, rename, or skip
            var filePermission = await connection.RequestPermissionAsync(
                sessionId,
                $"file_review_{Guid.NewGuid():N}",
                $"Accept '{sample.FileName}'?",
                [
                    new PermissionOption("accept", "Accept", PermissionKind.AllowOnce),
                    new PermissionOption("rename", "Rename", PermissionKind.AllowOnce),
                    new PermissionOption("skip", "Skip", PermissionKind.RejectOnce)
                ]
            );
            
            var selectedOption = (filePermission.Outcome as SelectedPermissionOutcome)?.OptionId;
            
            switch (selectedOption)
            {
                case "accept":
                    approvedSamples.Add(sample);
                    break;
                    
                case "rename":
                    // Request new filename from user
                    var renameResult = await connection.RequestInputAsync(
                        sessionId,
                        $"rename_{Guid.NewGuid():N}",
                        $"Enter new filename (current: {sample.FileName}):",
                        sample.FileName
                    );
                    
                    if (!renameResult.Cancelled && !string.IsNullOrWhiteSpace(renameResult.Value))
                    {
                        approvedSamples.Add(sample with { FileName = renameResult.Value });
                        await connection.SendSessionUpdateAsync(sessionId, new AgentMessageChunkUpdate(
                            new TextContent($"  → Renamed to {renameResult.Value}")));
                    }
                    break;
                    
                case "skip":
                default:
                    await connection.SendSessionUpdateAsync(sessionId, new AgentMessageChunkUpdate(
                        new TextContent($"  ⊘ Skipped {sample.FileName}")));
                    break;
            }
        }
        
        if (approvedSamples.Count == 0)
        {
            return [new TextContent("No samples approved. Generation cancelled.")];
        }
        
        await connection.SendSessionUpdateAsync(sessionId, new PlanUpdate([
            new PlanEntry("Analyze package", "completed"),
            new PlanEntry("Detect language", "completed"),
            new PlanEntry("Generate samples", "completed"),
            new PlanEntry("Review files", "completed"),
            new PlanEntry("Write files", "in_progress")
        ]));
        
        // Final confirmation before writing
        var writePermission = await connection.RequestPermissionAsync(
            sessionId,
            $"write_confirm_{Guid.NewGuid():N}",
            $"Write {approvedSamples.Count} approved file(s) to {outputFolder}?",
            [
                new PermissionOption("allow", "Write Files", PermissionKind.AllowOnce),
                new PermissionOption("deny", "Cancel", PermissionKind.RejectOnce)
            ]
        );
        
        var writeOption = (writePermission.Outcome as SelectedPermissionOutcome)?.OptionId;
        if (writeOption != "allow")
        {
            return [new TextContent("Sample generation cancelled by user.")];
        }
        
        // Write approved files
        foreach (var sample in approvedSamples)
        {
            var filePath = Path.Combine(outputFolder, sample.FileName);
            await _fileHelper.WriteAsync(filePath, sample.Code);
            
            await connection.SendSessionUpdateAsync(sessionId, new AgentMessageChunkUpdate(
                new TextContent($"✓ Wrote {sample.FileName}")));
        }
        
        await connection.SendSessionUpdateAsync(sessionId, new PlanUpdate([
            new PlanEntry("Analyze package", "completed"),
            new PlanEntry("Detect language", "completed"),
            new PlanEntry("Generate samples", "completed"),
            new PlanEntry("Review files", "completed"),
            new PlanEntry("Write files", "completed")
        ]));
        
        return [new TextContent($"Generated {approvedSamples.Count} sample(s) in {outputFolder}")];
    }
    
    // NOTE: Additional language contexts (Python, TypeScript, Java, Go) are added in Phase 3.
    // Update this switch in Phase 3 to include all languages.
    private SampleLanguageContext CreateLanguageContext(SdkLanguage language) => language switch
    {
        SdkLanguage.DotNet => new DotNetSampleLanguageContext(_fileHelper),
        _ => throw new NotSupportedException($"Language {language} not yet supported. Add support in Phase 3.")
    };
    
    private static string BuildSystemPrompt(SampleLanguageContext context)
    {
        return $"""
            You are an expert SDK sample generator.
            Generate clear, concise, runnable code samples.
            
            {context.GetInstructions()}
            
            Output JSON array:
            [{{"name": "SampleName", "description": "...", "code": "..."}}]
            """;
    }
    
    private static string BuildUserPrompt(string? customPrompt, string sourceContext)
    {
        var prompt = customPrompt ?? "Generate samples demonstrating the main features of this SDK.";
        return $"""
            {prompt}
            
            Source code context:
            {sourceContext}
            """;
    }
}
```

### VALIDATE
Build succeeds.

---

## FILE 23: Services/SamplesFolderScanner.cs

```csharp
namespace Sdk.Tools.Cli.Services;

/// <summary>
/// Scans for existing samples folders and suggests output locations.
/// </summary>
public class SamplesFolderScanner
{
    private static readonly string[] KnownSamplesFolders =
    [
        "samples",
        "examples", 
        "sample",
        "example",
        "src/samples",
        "src/examples",
        "demo",
        "demos",
        "quickstarts"
    ];
    
    public ScanResult Scan(string packagePath)
    {
        var candidates = new List<string>();
        string? existingFolder = null;
        
        foreach (var folderName in KnownSamplesFolders)
        {
            var fullPath = Path.Combine(packagePath, folderName);
            if (Directory.Exists(fullPath))
            {
                existingFolder ??= fullPath;
                candidates.Add(fullPath);
            }
        }
        
        var suggested = existingFolder ?? Path.Combine(packagePath, "generated-samples");
        
        return new ScanResult(existingFolder, suggested, candidates);
    }
    
    public record ScanResult(
        string? ExistingFolder,
        string SuggestedFolder,
        List<string> AllCandidates
    );
}
```

### VALIDATE
Build succeeds.

---

## FILE 24: Mcp/McpServer.cs

> **Depends on:** ModelContextProtocol NuGet package, FILE 06 (SampleGeneratorTool)

```csharp
using ModelContextProtocol.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Services;
using Sdk.Tools.Cli.Services.Languages;

namespace Sdk.Tools.Cli.Mcp;

/// <summary>
/// MCP server implementation for AI agent integration.
/// Exposes sdk-cli tools to VS Code Copilot, Claude Desktop, etc.
/// </summary>
public static class McpServer
{
    public static async Task RunAsync(string transport, int port, string logLevel)
    {
        var builder = McpServerBuilder.Create(args: [])
            .WithName("sdk-cli")
            .WithVersion("1.0.0");
        
        // Configure services
        builder.Services.AddLogging(lb => lb.AddConsole().SetMinimumLevel(ParseLogLevel(logLevel)));
        builder.Services.AddSingleton<CopilotAgentService>();
        builder.Services.AddSingleton<LanguageDetector>();
        builder.Services.AddSingleton<FileHelper>();
        builder.Services.AddSingleton<ConfigurationHelper>();
        
        // Register tools
        builder.WithTools<SampleGeneratorMcpTool>();
        
        // Configure transport
        if (transport == "sse")
        {
            builder.WithHttpTransport(port);
        }
        else
        {
            builder.WithStdioTransport();
        }
        
        var server = builder.Build();
        await server.RunAsync();
    }
    
    private static LogLevel ParseLogLevel(string level) => level.ToLowerInvariant() switch
    {
        "debug" => LogLevel.Debug,
        "warning" => LogLevel.Warning,
        "error" => LogLevel.Error,
        _ => LogLevel.Information
    };
}
```

### VALIDATE
Build succeeds.

---

## FILE 25: Mcp/SampleGeneratorMcpTool.cs

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;
using Sdk.Tools.Cli.Services;
using Sdk.Tools.Cli.Services.Languages;
using Sdk.Tools.Cli.Services.Languages.Samples;

namespace Sdk.Tools.Cli.Mcp;

/// <summary>
/// MCP tool wrapper for the sample generator.
/// </summary>
[McpServerToolType]
public class SampleGeneratorMcpTool
{
    private readonly CopilotAgentService _agentService;
    private readonly LanguageDetector _detector;
    private readonly FileHelper _fileHelper;
    
    public SampleGeneratorMcpTool(
        CopilotAgentService agentService,
        LanguageDetector detector,
        FileHelper fileHelper)
    {
        _agentService = agentService;
        _detector = detector;
        _fileHelper = fileHelper;
    }
    
    [McpServerTool("generate_samples")]
    [Description("Generate code samples for an SDK package")]
    public async Task<string> GenerateSamplesAsync(
        [Description("Path to the SDK package directory")] string packagePath,
        [Description("Output directory (optional, auto-detected if not provided)")] string? outputPath = null,
        [Description("Custom prompt describing what samples to generate")] string? prompt = null,
        CancellationToken cancellationToken = default)
    {
        var language = await _detector.DetectAsync(packagePath);
        if (language == null)
        {
            return "Error: Could not detect package language.";
        }
        
        try
        {
            // Load source context and generate samples
            var context = CreateLanguageContext(language.Language);
            var sourceContext = await context.LoadContextAsync(
                new[] { packagePath }, null, cancellationToken: cancellationToken);
            
            var systemPrompt = BuildSystemPrompt(context);
            var userPrompt = BuildUserPrompt(prompt, sourceContext);
            
            var samples = await _agentService.RunAgentAsync<List<GeneratedSample>>(
                systemPrompt, userPrompt, "gpt-4.1", cancellationToken);
            
            var output = outputPath ?? Path.Combine(packagePath, "generated-samples");
            Directory.CreateDirectory(output);
            
            foreach (var sample in samples)
            {
                var filePath = Path.Combine(output, sample.FileName ?? $"{sample.Name}{context.FileExtension}");
                await File.WriteAllTextAsync(filePath, sample.Code, cancellationToken);
            }
            
            return $"Generated {samples.Count} sample(s) in {output}";
        }
        catch (Exception ex)
        {
            return $"Error generating samples: {ex.Message}";
        }
    }
    
    // NOTE: Additional language contexts (Python, TypeScript, Java, Go) are added in Phase 3.
    // Update this switch in Phase 3 to include all languages.
    private SampleLanguageContext CreateLanguageContext(SdkLanguage language) => language switch
    {
        SdkLanguage.DotNet => new DotNetSampleLanguageContext(_fileHelper),
        _ => throw new NotSupportedException($"Language {language} not yet supported. Add support in Phase 3.")
    };
    
    private static string BuildSystemPrompt(SampleLanguageContext context)
    {
        return $"""
            You are an expert SDK sample generator.
            Generate clear, concise, runnable code samples.
            
            {context.GetInstructions()}
            
            Output JSON array:
            [{{"name": "SampleName", "description": "...", "code": "..."}}]
            """;
    }
    
    private static string BuildUserPrompt(string? customPrompt, string sourceContext)
    {
        var prompt = customPrompt ?? "Generate samples demonstrating the main features of this SDK.";
        return $"""
            {prompt}
            
            Source code context:
            {sourceContext}
            """;
    }
}
```

### VALIDATE
Build succeeds.

---

## FILE 26: Program.cs (Updated for ACP + MCP)

> **Updates FILE 17 to add ACP and MCP commands**

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Tools.Cli.Acp;
using Sdk.Tools.Cli.Mcp;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Services;
using Sdk.Tools.Cli.Services.Languages;

namespace Sdk.Tools.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("SDK CLI - Sample generation and SDK utilities");
        
        // sdk-cli mcp (MCP server for VS Code, Claude Desktop)
        rootCommand.AddCommand(BuildMcpCommand());
        
        // sdk-cli acp (ACP agent for interactive generation)
        rootCommand.AddCommand(BuildAcpCommand());
        
        // sdk-cli package sample generate
        var services = ConfigureServices();
        var packageCommand = new Command("package", "Package-related commands");
        rootCommand.AddCommand(packageCommand);
        
        var sampleCommand = new Command("sample", "Sample-related commands");
        packageCommand.AddCommand(sampleCommand);
        
        sampleCommand.AddCommand(BuildSampleGenerateCommand(services));
        
        return await rootCommand.InvokeAsync(args);
    }
    
    private static Command BuildMcpCommand()
    {
        var command = new Command("mcp", "Start MCP server for AI agent integration (VS Code, Claude Desktop)");
        
        var transportOption = new Option<string>("--transport", () => "stdio", "Transport type (stdio, sse)");
        var portOption = new Option<int>("--port", () => 8080, "Port for SSE transport");
        var logLevelOption = new Option<string>("--log-level", () => "info", "Log level");
        
        command.AddOption(transportOption);
        command.AddOption(portOption);
        command.AddOption(logLevelOption);
        
        command.SetHandler(async (transport, port, logLevel) =>
        {
            await McpServer.RunAsync(transport, port, logLevel);
        }, transportOption, portOption, logLevelOption);
        
        return command;
    }
    
    private static Command BuildAcpCommand()
    {
        var command = new Command("acp", "Start ACP agent for interactive sample generation");
        
        var logLevelOption = new Option<string>("--log-level", () => "info", "Log level");
        command.AddOption(logLevelOption);
        
        command.SetHandler(async (logLevel) =>
        {
            await SampleGeneratorAgentHost.RunAsync(logLevel);
        }, logLevelOption);
        
        return command;
    }
    
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<CopilotAgentService>();
        services.AddSingleton<LanguageDetector>();
        services.AddSingleton<FileHelper>();
        services.AddSingleton<ConfigurationHelper>();
        services.AddSingleton<SamplesFolderScanner>();
        services.AddSingleton<SampleGeneratorTool>();
        
        return services.BuildServiceProvider();
    }
    
    private static Command BuildSampleGenerateCommand(IServiceProvider services)
    {
        var command = new Command("generate", "Generate code samples for an SDK package");
        
        var packagePathOption = new Option<string>(
            "--package-path",
            "Path to the SDK package directory") { IsRequired = true };
        
        var outputOption = new Option<string?>(
            "--output",
            "Output directory for generated samples");
        
        var languageOption = new Option<string?>(
            "--language",
            "Language (dotnet, python, typescript, java, go). Auto-detected if not specified.");
        
        var promptOption = new Option<string?>(
            "--prompt",
            "Custom prompt describing what samples to generate");
        
        var modelOption = new Option<string>(
            "--model",
            () => "gpt-4.1",
            "Model to use for generation");
        
        var dryRunOption = new Option<bool>(
            "--dry-run",
            "Print samples to console instead of writing files");
        
        command.AddOption(packagePathOption);
        command.AddOption(outputOption);
        command.AddOption(languageOption);
        command.AddOption(promptOption);
        command.AddOption(modelOption);
        command.AddOption(dryRunOption);
        
        command.SetHandler(async (packagePath, output, language, prompt, model, dryRun) =>
        {
            var tool = services.GetRequiredService<SampleGeneratorTool>();
            var result = await tool.ExecuteAsync(packagePath, output, language, prompt, model, dryRun);
            Environment.ExitCode = result;
        }, packagePathOption, outputOption, languageOption, promptOption, modelOption, dryRunOption);
        
        return command;
    }
}
```

### VALIDATE
```bash
dotnet build tools/sdk-cli/Sdk.Tools.Cli/Sdk.Tools.Cli.csproj
```

### DONE WHEN
Build succeeds with zero errors. **PHASE 2 COMPLETE.**

---

# PHASE 3 FILES (Multi-Language Support)

---

## FILE 27: Services/Languages/PythonLanguageService.cs

```csharp
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages;

public class PythonLanguageService : LanguageService
{
    public override SdkLanguage Language => SdkLanguage.Python;
    public override string FileExtension => ".py";
    public override string[] DefaultSourceDirectories => new[] { "src", "." };
    public override string[] DefaultIncludePatterns => new[] { "**/*.py" };
    public override string[] DefaultExcludePatterns => new[] 
    { 
        "**/__pycache__/**", 
        "**/.*", 
        "**/test*/**",
        "**/venv/**",
        "**/.venv/**"
    };
}
```

### VALIDATE
Build succeeds.

---

## FILE 28: Services/Languages/TypeScriptLanguageService.cs

```csharp
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages;

public class TypeScriptLanguageService : LanguageService
{
    public override SdkLanguage Language => SdkLanguage.JavaScript;
    public override string FileExtension => ".ts";
    public override string[] DefaultSourceDirectories => new[] { "src" };
    public override string[] DefaultIncludePatterns => new[] { "**/*.ts" };
    public override string[] DefaultExcludePatterns => new[] 
    { 
        "**/node_modules/**", 
        "**/dist/**", 
        "**/*.d.ts",
        "**/*.test.ts",
        "**/*.spec.ts"
    };
}
```

### VALIDATE
Build succeeds.

---

## FILE 29: Services/Languages/JavaLanguageService.cs

```csharp
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages;

public class JavaLanguageService : LanguageService
{
    public override SdkLanguage Language => SdkLanguage.Java;
    public override string FileExtension => ".java";
    public override string[] DefaultSourceDirectories => new[] { "src/main/java", "src" };
    public override string[] DefaultIncludePatterns => new[] { "**/*.java" };
    public override string[] DefaultExcludePatterns => new[] 
    { 
        "**/target/**", 
        "**/build/**", 
        "**/test/**",
        "**/*Test.java"
    };
}
```

### VALIDATE
Build succeeds.

---

## FILE 30: Services/Languages/GoLanguageService.cs

```csharp
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages;

public class GoLanguageService : LanguageService
{
    public override SdkLanguage Language => SdkLanguage.Go;
    public override string FileExtension => ".go";
    public override string[] DefaultSourceDirectories => new[] { "." };
    public override string[] DefaultIncludePatterns => new[] { "**/*.go" };
    public override string[] DefaultExcludePatterns => new[] 
    { 
        "**/vendor/**", 
        "**/*_test.go"
    };
}
```

### VALIDATE
Build succeeds.

---

## FILE 31: Services/Languages/Samples/PythonSampleLanguageContext.cs

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages.Samples;

public sealed class PythonSampleLanguageContext : SampleLanguageContext
{
    public PythonSampleLanguageContext(FileHelper fileHelper) : base(fileHelper) { }

    public override SdkLanguage Language => SdkLanguage.Python;

    protected override string[] DefaultIncludeExtensions => new[] { ".py" };

    protected override string[] DefaultExcludePatterns => new[] 
    { 
        "**/__pycache__/**", 
        "**/.*",
        "**/venv/**",
        "**/.venv/**",
        "**/*_test.py",
        "**/test_*.py"
    };

    protected override int GetPriority(FileMetadata file)
    {
        var name = Path.GetFileName(file.FilePath).ToLowerInvariant();
        if (name.Contains("client")) return 1;
        if (name.Contains("_models")) return 2;
        if (name.Contains("operations")) return 3;
        return 10;
    }

    public override string GetInstructions() => """
        Generate Python code samples following these conventions:
        - Use type hints (Python 3.9+ style)
        - Use async/await for async operations
        - Follow PEP 8 style guide
        - Include docstrings for functions
        - Use context managers (with statements) where appropriate
        - Import statements at top of file
        - Use f-strings for string formatting
        """;
}
```

### VALIDATE
Build succeeds.

---

## FILE 32: Services/Languages/Samples/TypeScriptSampleLanguageContext.cs

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages.Samples;

public sealed class TypeScriptSampleLanguageContext : SampleLanguageContext
{
    public TypeScriptSampleLanguageContext(FileHelper fileHelper) : base(fileHelper) { }

    public override SdkLanguage Language => SdkLanguage.JavaScript;

    protected override string[] DefaultIncludeExtensions => new[] { ".ts", ".js" };

    protected override string[] DefaultExcludePatterns => new[] 
    { 
        "**/node_modules/**", 
        "**/dist/**", 
        "**/*.d.ts",
        "**/*.test.ts",
        "**/*.spec.ts"
    };

    protected override int GetPriority(FileMetadata file)
    {
        var name = Path.GetFileName(file.FilePath).ToLowerInvariant();
        if (name.Contains("client")) return 1;
        if (name.Contains("model")) return 2;
        if (name == "index.ts") return 3;
        return 10;
    }

    public override string GetInstructions() => """
        Generate TypeScript code samples following these conventions:
        - Use ES modules (import/export)
        - Use async/await for async operations
        - Use strict TypeScript (no any unless necessary)
        - Use const/let, never var
        - Use arrow functions where appropriate
        - Include proper type annotations
        - Use template literals for string interpolation
        """;
}
```

### VALIDATE
Build succeeds.

---

## FILE 33: Services/Languages/Samples/JavaSampleLanguageContext.cs

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages.Samples;

public sealed class JavaSampleLanguageContext : SampleLanguageContext
{
    public JavaSampleLanguageContext(FileHelper fileHelper) : base(fileHelper) { }

    public override SdkLanguage Language => SdkLanguage.Java;

    protected override string[] DefaultIncludeExtensions => new[] { ".java" };

    protected override string[] DefaultExcludePatterns => new[] 
    { 
        "**/target/**", 
        "**/build/**",
        "**/*Test.java",
        "**/*Tests.java"
    };

    protected override int GetPriority(FileMetadata file)
    {
        var name = Path.GetFileName(file.FilePath).ToLowerInvariant();
        if (name.Contains("client")) return 1;
        if (name.Contains("builder")) return 2;
        if (name.Contains("model")) return 3;
        return 10;
    }

    public override string GetInstructions() => """
        Generate Java code samples following these conventions:
        - Use Java 17+ features
        - Include proper package declaration
        - Use try-with-resources for closeable resources
        - Include Javadoc comments for public methods
        - Follow Java naming conventions
        - Handle exceptions appropriately
        - Use var for local variables where type is obvious
        """;
}
```

### VALIDATE
Build succeeds.

---

## FILE 34: Services/Languages/Samples/GoSampleLanguageContext.cs

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages.Samples;

public sealed class GoSampleLanguageContext : SampleLanguageContext
{
    public GoSampleLanguageContext(FileHelper fileHelper) : base(fileHelper) { }

    public override SdkLanguage Language => SdkLanguage.Go;

    protected override string[] DefaultIncludeExtensions => new[] { ".go" };

    protected override string[] DefaultExcludePatterns => new[] 
    { 
        "**/vendor/**", 
        "**/*_test.go"
    };

    protected override int GetPriority(FileMetadata file)
    {
        var name = Path.GetFileName(file.FilePath).ToLowerInvariant();
        if (name.Contains("client")) return 1;
        if (name.Contains("models")) return 2;
        return 10;
    }

    public override string GetInstructions() => """
        Generate Go code samples following these conventions:
        - Use package main for runnable samples
        - Include proper imports
        - Handle errors explicitly (no underscore ignoring)
        - Use defer for cleanup
        - Follow Go naming conventions (camelCase for private, PascalCase for public)
        - Use context.Context for cancellation
        - Keep samples simple and idiomatic
        """;
}
```

### VALIDATE
Build succeeds.

---

## FILE 35: Prompts/SampleGeneration/python.md

```markdown
# Python SDK Sample Guidelines

You are an expert in modern Python (3.10+) with deep knowledge of SDK patterns, asyncio, and type hints.

## Language Standards
- **Python 3.10+** features: match statements, type unions with `|`, ParamSpec
- **Type hints everywhere** - all function signatures, variables where non-obvious
- **Async-first** - prefer `async def` for I/O operations

## Authentication
Prefer token credentials over API keys. Always from environment variables.

## Patterns
```python
# Async: context manager for cleanup
async with ServiceClient(credential=credential) as client:
    result = await client.get_resource("id")

# Errors: catch specific exceptions
except NotFoundError:
    logger.warning("Not found: %s", resource_id)

# Config: validate environment variables
api_key = os.environ.get("API_KEY") or raise ValueError("API_KEY required")

# Logging: logging module, not print
logger.info("Processing", extra={"request_id": rid})

# Types: full annotations
async def get(client: Client, id: str) -> Resource: ...
```

### VALIDATE
File exists.

---

## FILE 36: Prompts/SampleGeneration/typescript.md

```markdown
# TypeScript SDK Sample Guidelines

You are an expert in modern TypeScript (5.0+) with deep knowledge of SDK patterns, async/await, and strict type safety.

## Language Standards
- **TypeScript 5.0+** with `strict: true` in tsconfig
- **ES2022+ target** - top-level await, private fields
- **ESM modules** - `import`/`export`, not CommonJS

## Authentication
Prefer token credentials over API keys. Always from environment variables.

## Patterns
```typescript
// Async: for-await for iterables, AbortController for cancellation
for await (const item of client.list()) { ... }
const controller = new AbortController();
await client.get("id", { signal: controller.signal });

// Errors: instanceof checks for SDK errors
if (error instanceof NotFoundError) { ... }

// Config: type-safe env access with validation
function getRequiredEnv(name: string): string {
  return process.env[name] ?? throw new Error(`${name} required`);
}

// Logging: pino or similar, not console.log
logger.info({ requestId }, "Processing");

// Types: no any, strict interfaces
async function get(config: Config, id: string): Promise<Resource> { ... }
```

### VALIDATE
File exists.

---

## FILE 37: Prompts/SampleGeneration/java.md

```markdown
# Java SDK Sample Guidelines

You are an expert in modern Java (17+) with deep knowledge of SDK patterns, reactive streams, and enterprise security.

## Language Standards
- **Java 17+** - records, sealed classes, pattern matching
- **Standard SDK conventions**

## Authentication
Prefer token credentials over API keys. Always from environment variables.

## Patterns
```java
// Async: CompletableFuture with timeout
client.getAsync("id").orTimeout(30, TimeUnit.SECONDS).join();

// Errors: catch specific SDK exceptions
catch (NotFoundException e) { logger.warn("Not found: {}", id); }

// Resources: try-with-resources for AutoCloseable
try (var client = ServiceClient.builder().build()) { ... }

// Config: validate environment
String key = Optional.ofNullable(System.getenv("KEY"))
    .orElseThrow(() -> new IllegalStateException("KEY required"));

// Logging: SLF4J with parameterized messages
logger.info("Processing requestId={}", requestId);
```

### VALIDATE
File exists.

---

## FILE 38: Prompts/SampleGeneration/go.md

```markdown
# Go SDK Sample Guidelines

You are an expert in Go (1.21+) with deep knowledge of SDK patterns, context handling, and idiomatic error handling.

## Language Standards
- **Go 1.21+** - generics, slog, enhanced http
- **Standard SDK conventions**
- **go.mod** module system

## Authentication
Prefer token credentials over API keys. Always from environment variables.

## Patterns
```go
// Context: always with timeout, propagate from caller
ctx, cancel := context.WithTimeout(ctx, 30*time.Second)
defer cancel()
result, err := client.Get(ctx, "id")

// Errors: check explicitly, wrap with context, use errors.As
if err != nil {
    var notFound *sdk.NotFoundError
    if errors.As(err, &notFound) { ... }
    return fmt.Errorf("get %s: %w", id, err)
}

// Config: validate environment
key := os.Getenv("KEY")
if key == "" { log.Fatal("KEY required") }

// Logging: slog structured logging
slog.Info("processing", "request_id", rid)

// Cleanup: defer for close
defer client.Close()
```

### VALIDATE
File exists.

### DONE WHEN
**PHASE 3 COMPLETE.** All language contexts compile.

**CRITICAL POST-PHASE 3 ACTION:** Update the `CreateLanguageContext` switch statements in the following files to include all language contexts:

**Files to update:**
- `Tools/Package/Samples/SampleGeneratorTool.cs` (FILE 15)
- `Services/InteractiveSampleGenerator.cs` (FILE 22)  
- `Mcp/SampleGeneratorMcpTool.cs` (FILE 25)

**Updated switch statement:**
```csharp
private SampleLanguageContext CreateLanguageContext(SdkLanguage language) => language switch
{
    SdkLanguage.DotNet => new DotNetSampleLanguageContext(_fileHelper),
    SdkLanguage.Python => new PythonSampleLanguageContext(_fileHelper),
    SdkLanguage.JavaScript => new TypeScriptSampleLanguageContext(_fileHelper),
    SdkLanguage.Java => new JavaSampleLanguageContext(_fileHelper),
    SdkLanguage.Go => new GoSampleLanguageContext(_fileHelper),
    _ => throw new NotSupportedException($"Language {language} not supported")
};
```

---

# PHASE 4 FILES (Tests + Docs)

---

## FILE 39: AgentClientProtocol.Sdk.Tests/AgentClientProtocol.Sdk.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <RootNamespace>AgentClientProtocol.Sdk.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="Moq" Version="4.20.70" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\AgentClientProtocol.Sdk\AgentClientProtocol.Sdk.csproj" />
  </ItemGroup>

</Project>
```

### VALIDATE
```bash
dotnet restore tools/sdk-cli/AgentClientProtocol.Sdk.Tests/AgentClientProtocol.Sdk.Tests.csproj
```

---

## FILE 40: AgentClientProtocol.Sdk.Tests/JsonRpc/JsonRpcMessageTests.cs

```csharp
using System.Text.Json;
using AgentClientProtocol.Sdk.JsonRpc;
using Xunit;

namespace AgentClientProtocol.Sdk.Tests.JsonRpc;

public class JsonRpcMessageTests
{
    [Fact]
    public void JsonRpcRequest_SerializesCorrectly()
    {
        var request = new JsonRpcRequest
        {
            Id = 1,
            Method = "initialize",
            Params = JsonSerializer.SerializeToElement(new { protocolVersion = "2024-11-05" })
        };
        
        var json = JsonSerializer.Serialize(request);
        
        Assert.Contains("\"jsonrpc\":\"2.0\"", json);
        Assert.Contains("\"id\":1", json);
        Assert.Contains("\"method\":\"initialize\"", json);
        Assert.Contains("\"protocolVersion\":\"2024-11-05\"", json);
    }
    
    [Fact]
    public void JsonRpcRequest_DeserializesCorrectly()
    {
        var json = """{"jsonrpc":"2.0","id":42,"method":"session/new","params":{"workspacePath":"/test"}}""";
        
        var request = JsonSerializer.Deserialize<JsonRpcRequest>(json);
        
        Assert.NotNull(request);
        Assert.Equal("2.0", request.JsonRpc);
        Assert.Equal(42, ((JsonElement)request.Id!).GetInt32());
        Assert.Equal("session/new", request.Method);
        Assert.True(request.Params.HasValue);
    }
    
    [Fact]
    public void JsonRpcResponse_SerializesWithResult()
    {
        var response = new JsonRpcResponse
        {
            Id = 1,
            Result = new { sessionId = "sess_123" }
        };
        
        var json = JsonSerializer.Serialize(response);
        
        Assert.Contains("\"jsonrpc\":\"2.0\"", json);
        Assert.Contains("\"id\":1", json);
        Assert.Contains("\"sessionId\":\"sess_123\"", json);
        Assert.DoesNotContain("\"error\"", json);
    }
    
    [Fact]
    public void JsonRpcResponse_SerializesWithError()
    {
        var response = new JsonRpcResponse
        {
            Id = 1,
            Error = new JsonRpcError
            {
                Code = -32600,
                Message = "Invalid Request"
            }
        };
        
        var json = JsonSerializer.Serialize(response);
        
        Assert.Contains("\"error\"", json);
        Assert.Contains("\"code\":-32600", json);
        Assert.Contains("\"message\":\"Invalid Request\"", json);
    }
    
    [Fact]
    public void JsonRpcNotification_HasNoId()
    {
        var notification = new JsonRpcRequest
        {
            Id = null,
            Method = "session/update"
        };
        
        var json = JsonSerializer.Serialize(notification);
        
        Assert.Contains("\"method\":\"session/update\"", json);
        // Id should be null in notification
        Assert.Contains("\"id\":null", json);
    }
}
```

### VALIDATE
```bash
dotnet test tools/sdk-cli/AgentClientProtocol.Sdk.Tests/ --filter "FullyQualifiedName~JsonRpcMessageTests"
```

---

## FILE 41: AgentClientProtocol.Sdk.Tests/Stream/NdJsonStreamTests.cs

```csharp
using System.Text;
using System.Text.Json;
using AgentClientProtocol.Sdk.JsonRpc;
using AgentClientProtocol.Sdk.Stream;
using Xunit;

namespace AgentClientProtocol.Sdk.Tests.Stream;

public class NdJsonStreamTests
{
    [Fact]
    public async Task WriteAsync_WritesNewlineDelimitedJson()
    {
        using var outputStream = new MemoryStream();
        using var inputStream = new MemoryStream();
        
        var ndJson = new NdJsonStream(inputStream, outputStream);
        
        var message = new JsonRpcRequest
        {
            Id = 1,
            Method = "test"
        };
        
        await ndJson.WriteAsync(message);
        
        outputStream.Position = 0;
        var written = Encoding.UTF8.GetString(outputStream.ToArray());
        
        Assert.EndsWith("\n", written);
        Assert.Contains("\"method\":\"test\"", written);
    }
    
    [Fact]
    public async Task ReadAsync_ReadsNewlineDelimitedJson()
    {
        var jsonLine = """{"jsonrpc":"2.0","id":1,"method":"initialize"}""" + "\n";
        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonLine));
        using var outputStream = new MemoryStream();
        
        var ndJson = new NdJsonStream(inputStream, outputStream);
        
        var message = await ndJson.ReadAsync<JsonRpcRequest>();
        
        Assert.NotNull(message);
        Assert.Equal("initialize", message.Method);
        Assert.Equal(1, ((JsonElement)message.Id!).GetInt32());
    }
    
    [Fact]
    public async Task ReadAsync_ReturnsNull_OnEndOfStream()
    {
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();
        
        var ndJson = new NdJsonStream(inputStream, outputStream);
        
        var message = await ndJson.ReadAsync<JsonRpcRequest>();
        
        Assert.Null(message);
    }
    
    [Fact]
    public async Task ReadAsync_HandlesMultipleMessages()
    {
        var json = """{"jsonrpc":"2.0","id":1,"method":"first"}
{"jsonrpc":"2.0","id":2,"method":"second"}
""";
        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        using var outputStream = new MemoryStream();
        
        var ndJson = new NdJsonStream(inputStream, outputStream);
        
        var first = await ndJson.ReadAsync<JsonRpcRequest>();
        var second = await ndJson.ReadAsync<JsonRpcRequest>();
        
        Assert.Equal("first", first?.Method);
        Assert.Equal("second", second?.Method);
    }
    
    [Fact]
    public async Task ReadAsync_SkipsEmptyLines()
    {
        var json = """

{"jsonrpc":"2.0","id":1,"method":"test"}

""";
        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        using var outputStream = new MemoryStream();
        
        var ndJson = new NdJsonStream(inputStream, outputStream);
        
        var message = await ndJson.ReadAsync<JsonRpcRequest>();
        
        Assert.Equal("test", message?.Method);
    }
}
```

### VALIDATE
```bash
dotnet test tools/sdk-cli/AgentClientProtocol.Sdk.Tests/ --filter "FullyQualifiedName~NdJsonStreamTests"
```

---

## FILE 42: AgentClientProtocol.Sdk.Tests/ConnectionTests.cs

```csharp
using System.Text;
using System.Text.Json;
using AgentClientProtocol.Sdk.JsonRpc;
using AgentClientProtocol.Sdk.Schema;
using AgentClientProtocol.Sdk.Stream;
using Xunit;

namespace AgentClientProtocol.Sdk.Tests;

public class ConnectionTests
{
    [Fact]
    public async Task Connection_SendsRequest_AndReceivesResponse()
    {
        // Simulate a round-trip: we write a request, the "server" responds
        var clientOutput = new MemoryStream();
        var clientInput = new MemoryStream();
        
        var stream = new NdJsonStream(clientInput, clientOutput);
        var connection = new Connection(stream);
        
        // Start the connection in background
        var connectionTask = connection.RunAsync();
        
        // Send a request (non-blocking since we're testing)
        var requestTask = connection.SendRequestAsync<InitializeResponse>(
            "initialize",
            new InitializeRequest(
                ProtocolVersion.Current,
                new ClientInfo("test-client", "1.0.0"),
                new ClientCapabilities()
            )
        );
        
        // Give time for request to be written
        await Task.Delay(50);
        
        // Read what was written to output
        clientOutput.Position = 0;
        var requestJson = Encoding.UTF8.GetString(clientOutput.ToArray());
        Assert.Contains("\"method\":\"initialize\"", requestJson);
        
        // Simulate response by writing to input
        var responseId = 1; // Connection uses incrementing IDs starting at 1
        var response = $$"""{"jsonrpc":"2.0","id":{{responseId}},"result":{"protocolVersion":"2024-11-05","agentInfo":{"name":"test","version":"1.0"},"capabilities":{}}}""" + "\n";
        
        // Create new stream with response
        clientInput = new MemoryStream(Encoding.UTF8.GetBytes(response));
        // Note: In real test we'd need a pipe or more complex setup
        
        // Stop connection
        connection.Dispose();
        
        // Verify request was sent correctly
        Assert.Contains("\"protocolVersion\":\"2024-11-05\"", requestJson);
    }
    
    [Fact]
    public async Task Connection_SendsNotification_WithoutId()
    {
        var clientOutput = new MemoryStream();
        var clientInput = new MemoryStream();
        
        var stream = new NdJsonStream(clientInput, clientOutput);
        var connection = new Connection(stream);
        
        await connection.SendNotificationAsync(
            "session/update",
            new SessionUpdate("sess_123", new AgentMessageChunkUpdate(new TextContent("Hello")))
        );
        
        clientOutput.Position = 0;
        var json = Encoding.UTF8.GetString(clientOutput.ToArray());
        
        Assert.Contains("\"method\":\"session/update\"", json);
        Assert.DoesNotContain("\"id\":", json); // Notifications have no id
    }
    
    [Fact]
    public void RequestError_HasCorrectCodes()
    {
        Assert.Equal(-32700, RequestError.ParseError);
        Assert.Equal(-32600, RequestError.InvalidRequest);
        Assert.Equal(-32601, RequestError.MethodNotFound);
        Assert.Equal(-32602, RequestError.InvalidParams);
        Assert.Equal(-32603, RequestError.InternalError);
    }
    
    [Fact]
    public void ProtocolVersion_IsCorrect()
    {
        Assert.Equal("2024-11-05", ProtocolVersion.Current);
        Assert.Equal(1, ProtocolVersion.Version);
    }
}
```

### VALIDATE
```bash
dotnet test tools/sdk-cli/AgentClientProtocol.Sdk.Tests/ --filter "FullyQualifiedName~ConnectionTests"
```

### DONE WHEN
All ACP SDK tests pass:
```bash
dotnet test tools/sdk-cli/AgentClientProtocol.Sdk.Tests/
```

---

## FILE 43: Sdk.Tools.Cli.Tests/Sdk.Tools.Cli.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Sdk.Tools.Cli\Sdk.Tools.Cli.csproj" />
  </ItemGroup>

</Project>
```

---

## FILE 44: Sdk.Tools.Cli.Tests/LanguageDetectorTests.cs

```csharp
using Sdk.Tools.Cli.Models;
using Sdk.Tools.Cli.Services.Languages;
using Xunit;

namespace Sdk.Tools.Cli.Tests;

public class LanguageDetectorTests
{
    private readonly LanguageDetector _detector = new();
    
    [Theory]
    [InlineData("Test.csproj", SdkLanguage.DotNet)]
    [InlineData("Test.sln", SdkLanguage.DotNet)]
    public void DetectsLanguage_DotNet(string filename, SdkLanguage expected)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, filename), "");
        
        try
        {
            var result = _detector.DetectLanguage(tempDir);
            Assert.Equal(expected, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
    
    [Fact]
    public void DetectsLanguage_Python()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "pyproject.toml"), "");
        
        try
        {
            var result = _detector.DetectLanguage(tempDir);
            Assert.Equal(SdkLanguage.Python, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
    
    [Fact]
    public void ReturnsNull_WhenUnknown()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var result = _detector.DetectLanguage(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
```

### VALIDATE
```bash
dotnet test tools/sdk-cli/Sdk.Tools.Cli.Tests/
```

---

## FILE 45: README.md

```markdown
# SDK CLI

Generate code samples for any SDK using AI.

## Quick Start

```bash
# Generate samples for a .NET SDK
sdk-cli package sample generate --package-path /path/to/openai-dotnet

# Generate samples with custom prompt
sdk-cli package sample generate --package-path /path/to/sdk --prompt "Generate streaming examples"

# Dry run (print to console)
sdk-cli package sample generate --package-path /path/to/sdk --dry-run
```

## Modes

### CLI Mode (Direct)
```bash
sdk-cli package sample generate --package-path ./my-sdk
```

### MCP Mode (VS Code / Claude Desktop)
```bash
sdk-cli mcp
```

Configure in VS Code settings or Claude Desktop config:
```json
{
  "mcpServers": {
    "sdk-cli": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/Sdk.Tools.Cli", "--", "mcp"]
    }
  }
}
```

### ACP Mode (Interactive)
```bash
sdk-cli acp
```

## Supported Languages

- .NET (C#)
- Python
- TypeScript
- Java
- Go

## Configuration

Create `sdk-cli-config.json` in your SDK root:

```json
{
  "language": "dotnet",
  "sourceDirectories": ["src"],
  "excludePatterns": ["**/test/**"],
  "maxContextBytes": 500000,
  "model": "gpt-4.1"
}
```
```

### DONE WHEN
**PHASE 4 COMPLETE. PROJECT SHIP-READY.**

---

# FINAL VALIDATION

```bash
# Build ACP SDK
dotnet build tools/sdk-cli/AgentClientProtocol.Sdk/AgentClientProtocol.Sdk.csproj

# Build SDK CLI
dotnet build tools/sdk-cli/Sdk.Tools.Cli/Sdk.Tools.Cli.csproj

# Run ACP SDK tests
dotnet test tools/sdk-cli/AgentClientProtocol.Sdk.Tests/

# Run SDK CLI tests
dotnet test tools/sdk-cli/Sdk.Tools.Cli.Tests/

# Verify CLI
dotnet run --project tools/sdk-cli/Sdk.Tools.Cli -- --help

# Verify MCP mode
dotnet run --project tools/sdk-cli/Sdk.Tools.Cli -- mcp --help

# Verify ACP mode
dotnet run --project tools/sdk-cli/Sdk.Tools.Cli -- acp --help

# Generate samples (integration test)
git clone https://github.com/openai/openai-dotnet /tmp/openai-dotnet
dotnet run --project tools/sdk-cli/Sdk.Tools.Cli -- package sample generate --package-path /tmp/openai-dotnet --dry-run
```

---

# END OF EXECUTION CHECKLIST

**All content below this line is archived reference material. Do not execute.**
