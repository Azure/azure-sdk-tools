# Contributing to the Azure SDK Tools MCP Server

Welcome! This guide explains how the `Azure.Sdk.Tools.Cli` project is organized so you can confidently navigate the code, add new functionality, and submit pull requests.

It is targeted at **new contributors** to the MCP server. For end-user installation and usage, see the [README](./README.md). For tool-authoring deep-dives, see the docs under [`tools/azsdk-cli/docs/`](../docs).

## Table of Contents

- [Overview](#overview)
- [Repository Layout](#repository-layout)
- [Project Structure](#project-structure)
  - [Entry Point: `Program.cs`](#entry-point-programcs)
  - [Top-Level Namespaces](#top-level-namespaces)
- [The `Tools/` Namespace (Core Concept)](#the-tools-namespace-core-concept)
  - [Tool Categories](#tool-categories)
  - [Anatomy of an MCP Tool](#anatomy-of-an-mcp-tool)
- [The `Helpers/` Namespace](#the-helpers-namespace)
- [The `Services/` Namespace](#the-services-namespace)
- [Other Namespaces at a Glance](#other-namespaces-at-a-glance)
- [Building, Running, and Testing](#building-running-and-testing)
- [Adding a New Tool](#adding-a-new-tool)
- [Coding Conventions](#coding-conventions)
- [Submitting a Pull Request](#submitting-a-pull-request)

---

## Overview

`Azure.Sdk.Tools.Cli` is a **dual-mode .NET 8 application** that runs as either:

1. **An MCP (Model Context Protocol) server** — invoked by LLM clients (GitHub Copilot, Claude, etc.) over stdio JSON-RPC.
2. **A standalone CLI tool** — invoked by developers from the terminal.

Both modes share the same code: every operation is implemented once and exposed as both a CLI verb and an MCP tool. The application encapsulates Azure SDK release-process automation (version updates, changelog generation, APIView review, TypeSpec authoring, pipeline analysis, release-plan orchestration, etc.) across multiple SDK languages (.NET, Python, Java, JavaScript, Go, Rust, C++).

## Repository Layout

The MCP server lives under `tools/azsdk-cli/` alongside its sibling projects:

| Folder | Purpose |
| --- | --- |
| [Azure.Sdk.Tools.Cli/](../Azure.Sdk.Tools.Cli) | The MCP server and CLI (this project). |
| [Azure.Sdk.Tools.Cli.Tests/](../Azure.Sdk.Tools.Cli.Tests) | NUnit test project. |
| [Azure.Sdk.Tools.Cli.Analyzer/](../Azure.Sdk.Tools.Cli.Analyzer) | Roslyn analyzers enforcing project conventions. |
| [Azure.Sdk.Tools.Cli.Benchmarks/](../Azure.Sdk.Tools.Cli.Benchmarks) | Performance benchmarks. |
| [Azure.Sdk.Tools.Cli.Evaluations/](../Azure.Sdk.Tools.Cli.Evaluations) | LLM-evaluation harness for tools. |
| [Azure.Sdk.Tools.Mock/](../Azure.Sdk.Tools.Mock) | Mock services used in tests. |
| [docs/](../docs) | Author-facing guides (new-tool, mcp-tools, per-language, etc.). |

## Project Structure

### Entry Point: `Program.cs`

[Program.cs](./Program.cs) is the single entry point for both modes:

1. Inspects the command-line args. If `start` or `mcp` is present, the app runs as an **MCP server** (ASP.NET hosted service streaming JSON-RPC over stdio). Otherwise, it runs as a **CLI**.
2. Builds a `WebApplicationBuilder`, configures logging (re-routed through `McpLogging` in server mode so log output doesn't corrupt the stdio JSON-RPC channel), and registers dependencies via `ServiceRegistrations.RegisterCommonServices()`.
3. For CLI mode, hands off to `CommandRunner.BuildAndRun()` (under [Commands/](./Commands)) which discovers all `MCPTool` subclasses via reflection, builds a `System.CommandLine` tree, parses args, and dispatches.
4. For MCP mode, starts the ASP.NET host which exposes the discovered tools (any class marked `[McpServerToolType]` with `[McpServerTool]` methods) over the MCP protocol.

### Top-Level Namespaces

All namespaces are rooted at `Azure.Sdk.Tools.Cli.*` and correspond to folders under this project:

| Folder | Namespace role |
| --- | --- |
| [Attributes/](./Attributes) | Reflection metadata attributes used by serializers and the analyzer (e.g., `FieldNameAttribute`). |
| [Commands/](./Commands) | CLI command infrastructure: `CommandRunner`, `SharedCommandGroups`, the `HostServerCommand` (the `start` / `mcp` verb), and supporting handlers. Built on **`System.CommandLine`**. |
| [Configuration/](./Configuration) | Strongly-typed configuration objects, e.g. `AzSdkToolsMcpServerConfiguration`, `APIViewConfiguration`. Bound from `appsettings.json` and environment variables. |
| [CopilotAgents/](./CopilotAgents) | Wrappers around the GitHub Copilot Agent SDK used by some tools to delegate work to Copilot (`CopilotAgentRunner`, `CopilotClientWrapper`, `CopilotSessionWrapper`). |
| [Extensions/](./Extensions) | C# extension methods (currently logging extensions). |
| [Formatters/](./Formatters) | Console output formatters such as `SimpleCliConsoleFormatter` used when running interactively. |
| [Helpers/](./Helpers) | Stateless utility classes — see [Helpers section](#the-helpers-namespace). |
| [Models/](./Models) | DTOs, enums, and response types. See [Models](#models). |
| [Options/](./Options) | `IOptions<T>` configuration classes (e.g., `AzureSdkKnowledgeBaseOptions`). |
| [Prompts/](./Prompts) | LLM prompt templates (`BasePromptTemplate` plus `.prompty`-style templates under `Templates/`). |
| [Services/](./Services) | Long-lived dependency-injected services that talk to external systems — see [Services section](#the-services-namespace). |
| [Telemetry/](./Telemetry) | OpenTelemetry instrumentation (`TelemetryService`, `TelemetryProcessor`) and Application Insights wiring. |
| [Templates/](./Templates) | File templates used to generate README/doc artifacts. |
| [Tools/](./Tools) | **MCP tools** — the heart of the project. See [Tools section](#the-tools-namespace-core-concept). |

## The `Tools/` Namespace (Core Concept)

Everything a Copilot/LLM client can invoke is implemented as a **tool**. A tool is a class that:

- Inherits from `MCPTool` (or `MCPMultiCommandTool` for tools that expose multiple verbs) in [Tools/Core/](./Tools/Core).
- Is decorated with `[McpServerToolType]` so the MCP server discovers it.
- Declares its place in the CLI tree via `CommandHierarchy` (one or more `CommandGroup` values from [`SharedCommandGroups`](./Commands/SharedCommandGroups.cs)).
- Builds a `System.CommandLine` `Command` in `GetCommand()` and implements `HandleCommand(ParseResult, CancellationToken)` for CLI invocation.
- Exposes one or more public methods marked `[McpServerTool(Name = "azsdk_*")]` for direct MCP invocation.
- Returns a `CommandResponse` subclass so the framework can apply uniform exit-code and error handling.

Dependencies (loggers, services, helpers) are supplied through **constructor injection**; the DI container is configured in `ServiceRegistrations`.

### Tool Categories

Tools are organized by domain under [Tools/](./Tools):

| Folder | What it does | Example tools |
| --- | --- | --- |
| [Core/](./Tools/Core) | Base classes: `MCPTool`, `MCPToolBase`, `MCPMultiCommandTool`. | (no end-user tools) |
| [APIView/](./Tools/APIView) | Create, fetch, and comment on API reviews. | `APIViewReviewTool` |
| [Config/](./Tools/Config) | Maintain repo-wide config such as CODEOWNERS and GitHub labels. | `CodeownersTool`, `GitHubLabelsTool` |
| [EngSys/](./Tools/EngSys) | Engineering-system utilities (log triage, package introspection, test analysis). | `LogAnalysisTool`, `PackageInfoTool`, `TestAnalysisTool` |
| [Example/](./Tools/Example) | Demo / smoke-test tools, compiled only in `DEBUG`. | `HelloWorldTool`, `ExampleTool` |
| [GitHub/](./Tools/GitHub) | GitHub PR operations. | `PullRequestTools` |
| [Package/](./Tools/Package) | Package lifecycle: version bumps, changelogs, build, test, release, README. | `VersionUpdateTool`, `ChangelogContentUpdateTool`, `SdkBuildTool`, `SdkReleaseTool`, `ReadMeGeneratorTool` |
| [Pipeline/](./Tools/Pipeline) | Azure Pipelines build/test analysis. | `PipelineAnalysisTool`, `PipelineTestsTool` |
| [ReleasePlan/](./Tools/ReleasePlan) | Release-plan work-item orchestration. | `ReleasePlanTool`, `PackageReleaseStatusTool`, `SpecWorkFlowTool` |
| [TypeSpec/](./Tools/TypeSpec) | TypeSpec authoring, validation, conversion. | `TypeSpecAuthoringTool`, `SpecValidationTool`, `TspInitTool`, `TspConvertTool` |
| [Verify/](./Tools/Verify) | Verify and install local prerequisites. | `VerifySetupTool`, `VerifySetupInstallTool` |

### Anatomy of an MCP Tool

The minimal pattern, from [Tools/Example/HelloWorldTool.cs](./Tools/Example/HelloWorldTool.cs):

```csharp
[McpServerToolType, Description("Simple echo tool for testing and demonstration purposes")]
public class HelloWorldTool(ILogger<HelloWorldTool> logger) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Example];

    private Argument<string> _inputArg = new("input") { Description = "The text to echo back" };

    protected override Command GetCommand() => new("hello-world", "...") { _inputArg };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        => await Task.FromResult(EchoSuccess(parseResult.GetValue(_inputArg) ?? ""));

    [McpServerTool(Name = "azsdk_hello_world"), Description("Returns your message with success status")]
    public DefaultCommandResponse EchoSuccess(string message) =>
        new() { Message = $"RESPONDING TO '{message}' with SUCCESS: 0" };
}
```

Key conventions:

- MCP tool names use the prefix `azsdk_` and `snake_case`.
- Each handler returns a `CommandResponse` (or subclass) — never a raw string, exception, or `void`.
- All `try`/`catch` should populate `ResponseError` rather than throw out to the framework.
- For step-by-step instructions on creating a new tool, see [`docs/new-tool.md`](../docs/new-tool.md) and [`docs/mcp-tools.md`](../docs/mcp-tools.md).

## The `Helpers/` Namespace

Helpers are **stateless** utility classes used by tools and services. Add a helper when you have a piece of logic that's reused, doesn't own external state, and doesn't need to be a DI singleton. Notable helpers in [Helpers/](./Helpers):

- **External-process wrappers** — `GitHelper`, `TspClientHelper`, `TypeSpecHelper`, `Process/` (for `dotnet`, `git`, `npx`, etc.).
- **File / path / environment** — `FileHelper`, `RealPath`, `EnvFileHelper`, `EnvironmentHelper`.
- **Parsing & domain logic** — `ChangelogHelper`, `VersionHelper`, `LogAnalysisHelper`, `PackageInfoHelper`, `SpecGenSdkConfigHelper`, `SpecPullRequestHelper`, `LabelHelper`, `CommonLanguageHelpers`.
- **LLM support** — `AzureOpenAIClientHelper`, `PromptHelper`, `ConversationLogger`, `TokenUsageHelper`.
- **MCP protocol plumbing** — `McpLogging`, `McpLoggingHostedService`, `OutputHelper`, `ProgressReporter`.
- **User interaction & security** — `UserHelper`, `InputSanitizer`.
- **CODEOWNERS** — `Codeowners/` subfolder.
- **Testing** — `TestHelper`.

Most helpers expose an interface (e.g., `IFileHelper`, `ITspClientHelper`) so they can be mocked in tests; new helpers should follow the same pattern when called from tools or services.

## The `Services/` Namespace

Services are **DI-registered, long-lived** classes that encapsulate access to an external system. They are typically registered as **singletons** (language services are scoped). Each has an interface so it can be mocked in tests.

| Interface | Implementation | Connects to |
| --- | --- | --- |
| `IAzureService` | `AzureService` | Azure auth (`DefaultAzureCredential` token acquisition). |
| `IDevOpsConnection` | `DevOpsConnection` | Azure DevOps connection/auth bootstrap. |
| `IDevOpsService` | `DevOpsService` | Azure DevOps REST API (work items, builds, projects). |
| `IGitHubService` | `GitHubService` | GitHub REST API via **Octokit** (PRs, issues, labels). |
| `IAPIViewService` | `APIViewService` | APIView review API (high-level operations). |
| `IAPIViewHttpService` | `APIViewHttpService` | APIView HTTP transport. |
| `IAPIViewAuthenticationService` | `APIViewAuthenticationService` | APIView authentication. |
| `IAPIViewFeedbackService` | `APIViewFeedbackService` | APIView feedback submission. |
| `IAzureSdkKnowledgeBaseService` | `AzureSdkKnowledgeBaseService` | Azure SDK knowledge-base HTTP service (MSAL-protected). |
| `IUpgradeService` | `UpgradeService` | GitHub Releases API (self-update / version check). |
| `IFeedbackClassifierService` | `FeedbackClassifierService` | Azure OpenAI for LLM-based feedback classification. |
| `IUserPromptProcessor` | `UserPromptProcessor` | Interactive user-prompt processing. |
| `LanguageService` (abstract) | `DotnetLanguageService`, `PythonLanguageService`, `JavaLanguageService`, `GoLanguageService`, `JavaScriptLanguageService`, `RustLanguageService` | Language-specific SDK CLIs: `dotnet`, `python`/`pip`, `mvn`, `go`, `npm`, `cargo`. Selected per-package by detecting the language. See [`docs/per-language.md`](../docs/per-language.md). |

When adding a service:

1. Define an `IFoo` interface and a `Foo` implementation under `Services/`.
2. Register it in `ServiceRegistrations.RegisterCommonServices()`.
3. Inject it via constructor parameters in tools/helpers that need it.

### Models

[Models/](./Models) holds:

- **Response types** — `CommandResponse` (abstract base, owns exit code + error handling) and subclasses such as `DefaultCommandResponse`, `ObjectCommandResponse`, `APIViewResponse`, `ValidationResponse`, `UpgradeResponse`, `FeedbackClassificationResponse` (all under `Models/Responses/`).
- **Domain DTOs** — `PackageInfo`, `PullRequestDetails`, `ParsedSdkPullRequest`, and per-area folders (`AzureDevOps/`, `APIView/`, TypeSpec models).
- **Enums** — `SdkLanguage`, `SdkType`.
- **Value types** — `NormalizedPath` for cross-platform path handling.

All tool handlers must return a `CommandResponse` subclass.

## Other Namespaces at a Glance

- **`Commands/`** — `System.CommandLine`-based CLI tree. Verb hierarchies are defined in [`SharedCommandGroups`](./Commands/SharedCommandGroups.cs) (e.g., `Package` → `pkg`, `AzurePipelines` → `azp`, `ReleasePlan` → `release-plan`). The MCP server itself is invoked through [`HostServerCommand`](./Commands/HostServer/HostServerCommand.cs). See [`docs/cli-commands-guidelines.md`](../docs/cli-commands-guidelines.md).
- **`Telemetry/`** — wraps OpenTelemetry. Every command/tool invocation produces an `Activity` span tagged with the command name and response status. Application Insights is the default export.
- **`Prompts/`** — prompt templates rendered at runtime for LLM-backed tools (e.g., TypeSpec authoring, feedback classification).
- **`CopilotAgents/`** — used by tools that off-load work to a Copilot Agent session rather than calling the model directly.
- **`Configuration/` & `Options/`** — strongly-typed config bound from `appsettings.json` and environment variables.

## Building, Running, and Testing

Prerequisites: **.NET 8.0 SDK** (`winget install Microsoft.DotNet.SDK.8`).

From `tools/azsdk-cli/`:

```bash
# Build
dotnet build

# Run unit tests
dotnet test

# Run the CLI
dotnet run --project Azure.Sdk.Tools.Cli -- --help
dotnet run --project Azure.Sdk.Tools.Cli -- example hello-world foobar

# Run as MCP server (stdio)
dotnet run --project Azure.Sdk.Tools.Cli -- start
```

To use your local build with VS Code / Copilot, point `.vscode/mcp.json` at this project (the repo already ships such a config — see the [README](./README.md#1-mcp-server-mode)).

Tests live in [`Azure.Sdk.Tools.Cli.Tests`](../Azure.Sdk.Tools.Cli.Tests), mirroring the source layout (`Tools/`, `Services/`, `Helpers/`, etc.). When adding a tool, add a corresponding test class. Set `AZSDKTOOLS_AGENT_TESTING=true` to enable test-mode behavior in tools that call out to external services.

## Adding a New Tool

A condensed checklist (see [`docs/new-tool.md`](../docs/new-tool.md) for the full walkthrough):

1. **Pick a category** under [Tools/](./Tools) (or create a new folder if no existing category fits).
2. **Create the class**: inherit from `MCPTool`, add `[McpServerToolType]`, declare `CommandHierarchy`, implement `GetCommand()` and `HandleCommand(...)`.
3. **Expose MCP methods**: mark public methods with `[McpServerTool(Name = "azsdk_<verb>_<object>")]` and a `[Description]`.
4. **Inject dependencies** via the primary constructor (`ILogger<T>`, services, helpers). The DI container resolves them automatically.
5. **Return a `CommandResponse`** subclass — set `ResponseError` for failures rather than throwing.
6. **Register any new service** in `ServiceRegistrations` (helpers usually don't need registration unless they have an interface used by DI).
7. **Add tests** under `Azure.Sdk.Tools.Cli.Tests/Tools/<Category>/`.
8. **Run the analyzer** (`dotnet build`) — the Roslyn analyzer enforces several conventions automatically.
9. **Update docs** if appropriate (e.g., [`docs/mcp-tools.md`](../docs/mcp-tools.md)).

## Coding Conventions

- **Language version**: C# 12, nullable enabled, file-scoped namespaces where practical.
- **Async**: prefer `async`/`await`; all tool handlers return `Task<CommandResponse>`.
- **Logging**: use `ILogger<T>` via DI; never `Console.WriteLine` from production code paths (the MCP stdio channel must remain pure JSON-RPC — that's why `McpLogging` exists).
- **Process invocation**: go through the wrappers in `Helpers/Process/` rather than calling `System.Diagnostics.Process` directly. See [`docs/process-calling.md`](../docs/process-calling.md).
- **Input safety**: validate and sanitize user-supplied strings (paths, repo names) with `InputSanitizer` before passing them to external processes.
- **No secrets**: never log tokens, connection strings, or PAT values.
- **Editor settings**: `.editorconfig` at the repo root governs formatting.

## Submitting a Pull Request

1. Branch from `main` (e.g., `users/<alias>/<short-description>`).
2. Ensure `dotnet build` and `dotnet test` both succeed locally.
3. Update [CHANGELOG.md](./CHANGELOG.md) with a one-line entry for user-visible changes.
4. Open a PR against `Azure/azure-sdk-tools:main`; the CI pipeline (`tools/azsdk-cli/ci.yml`) runs build + tests across platforms.
5. Tag a code owner from the `Tools` area for review.

Thanks for contributing!
