# Plan: Add "SDK Sample Generation" command to azsdk-cli

This document outlines the changes to add a new CLI/MCP tool that generates SDK samples per language. The command will live under the existing Generators command group as `azsdk generators samples` and expose an MCP method for agents.

## Goals and scope

- CLI command to generate sample code for an Azure SDK package based on user prompts.
- Support per-language conventions (dotnet, java, js/ts, python, go) across Azure SDK mono repositories.
- Default to auto-detecting the language and repo from `--package-path`.
- AI-powered generation via existing MicroagentHostService (Azure OpenAI) to create samples from user descriptions.
- Safe-by-default: dry-run and overwrite controls.

**Supported Azure SDK Repositories:**
- .NET: [azure-sdk-for-net](https://github.com/Azure/azure-sdk-for-net)
- Java: [azure-sdk-for-java](https://github.com/Azure/azure-sdk-for-java)
- JavaScript/TypeScript: [azure-sdk-for-js](https://github.com/Azure/azure-sdk-for-js)
- Python: [azure-sdk-for-python](https://github.com/Azure/azure-sdk-for-python)
- Go: [azure-sdk-for-go](https://github.com/Azure/azure-sdk-for-go)

## High-level design

- New tool class `SampleGeneratorTool : MCPTool`
  - Command path: `azsdk generators samples`
  - Options to locate package, select language, provide user prompt for sample generation, output location, dry-run, overwrite, and AI model.
  - Uses helpers and language services to compute correct sample folder and run format/lint after generation.
  - Uses MicroagentHostService to generate sample code based on user prompts and package context.
  - Exposes an MCP method for agent-triggered generation.
- Response model for structured output and JSON mode support.
- Registration in CLI tool list; no service registration changes required beyond using existing services.

## Files to add/edit

- Add tool implementation
  - New file: `Azure.Sdk.Tools.Cli/Tools/Generators/SampleGeneratorTool.cs`
    - Namespace: `Azure.Sdk.Tools.Cli.Tools.Generators`
    - Inherit from `MCPTool` ([contract](../Azure.Sdk.Tools.Cli.Contract/MCPTool.cs))
    - CommandHierarchy = `[SharedCommandGroups.Generators]` ([SharedCommandGroups.cs](../Azure.Sdk.Tools.Cli/Commands/SharedCommandGroups.cs))
    - Follow patterns from [ReadMeGeneratorTool](../Azure.Sdk.Tools.Cli/Tools/Package/ReadMeGeneratorTool.cs)

- Add Docker service for container operations
  - New file: `Azure.Sdk.Tools.Cli/Services/IDockerService.cs`
    - General-purpose interface for Docker container management and operations
  - New file: `Azure.Sdk.Tools.Cli/Services/DockerService.cs`
    - Implementation using Docker CLI via `IProcessHelper`
    - Methods: `CreateContainerAsync`, `RunCommandInContainerAsync`, `CopyToContainerAsync`, `CopyFromContainerAsync`, `RemoveContainerAsync`, etc.

- Register tool with CLI
  - Edit: `Azure.Sdk.Tools.Cli/Commands/SharedOptions.cs`
    - Add `typeof(SampleGeneratorTool)` to `ToolsList` ([link](../Azure.Sdk.Tools.Cli/Commands/SharedOptions.cs))

- Response type
  - New file: `Azure.Sdk.Tools.Cli/Models/Responses/SamplesGenerationResponse.cs`
    - Inherit from `Response`
    - Include: `FilesWritten`, `Warnings`, `Messages`, `DryRun`, `Language`, `OutputRoot`

- Tests
  - New file: `Azure.Sdk.Tools.Cli.Tests/Tools/Generators/SampleGeneratorToolTests.cs`
    - Mirror test structure from [ReadMeGeneratorToolTests](../Azure.Sdk.Tools.Cli.Tests/Tools/Generators/ReadMeGeneratorToolTests.cs)
    - Unit test the CLI handler argument parsing and a mocked MCP flow

- Docs
  - Update `Azure.Sdk.Tools.Cli/README.md` usage section to include examples for the new command

## Command contract (CLI)

- Command path: `azsdk generators samples`
- Options
  - `--package-path` (required): Absolute or repo-relative path to the package folder (under a language repo `sdk/...`)
  - `--prompt` (required): User description of the sample(s) to generate. Can describe a single scenario (e.g., "Create a sample that uploads a file to blob storage") or multiple scenarios (e.g., "Create samples for: 1) uploading a file, 2) downloading a file, 3) listing blobs in a container"). When multiple scenarios are described, separate sample files will be generated for each scenario.
  - `--language` (optional): `dotnet|java|js|ts|python|go` (default: auto-detect from repo)
  - `--output-directory` (optional): Root where samples will be written (default: language-appropriate folder)
  - `--template-path` (optional): Path to a sample template file or folder to seed from
  - `--verify` (flag, default false): Verify generated samples by running them in isolated Docker containers
  - `--dry-run` (flag, default false): Show planned files and diffs without writing
  - `--overwrite` (flag, default false): Allow overwriting existing files
  - `--model` (optional): Azure OpenAI deployment name to use (default: `gpt-4.1`)

**Example repository paths:**
- .NET: `/path/to/azure-sdk-for-net/sdk/storage/Azure.Storage.Blobs`
- Java: `/path/to/azure-sdk-for-java/sdk/storage/azure-storage-blob`
- JavaScript: `/path/to/azure-sdk-for-js/sdk/storage/storage-blob`
- Python: `/path/to/azure-sdk-for-python/sdk/storage/azure-storage-blob`
- Go: `/path/to/azure-sdk-for-go/sdk/storage/azblob`

Example usage:

- `azsdk generators samples --package-path /src/azure-sdk-for-python/sdk/servicebus/azure-servicebus --prompt "Create a sample that sends a message to a Service Bus queue"`
- `azsdk generators samples --package-path ./azure-sdk-for-js/sdk/storage/storage-blob --language js --prompt "Create samples for: 1) Upload a file to blob storage, 2) Download a file from blob storage, 3) List all blobs in a container" --verify --dry-run`
- `azsdk generators samples --package-path ./azure-sdk-for-net/sdk/keyvault/Azure.Security.KeyVault.Secrets --prompt "Generate samples demonstrating: creating a secret, retrieving a secret, updating a secret, and deleting a secret"`

## MCP method contract

- Attribute: `[McpServerTool(Name = "generate_samples")]`
- Signature (proposed):
  - `Task<SamplesGenerationResponse> GenerateSamplesAsync(string packagePath, string prompt, string? language = null, string? templatePath = null, bool verify = false, bool dryRun = false, bool overwrite = false, CancellationToken ct = default)`
- Behavior mirrors CLI options; returns `SamplesGenerationResponse`

## Implementation notes

- Tool skeleton
  - Use the template in [docs/new-tool.md](./new-tool.md) and the structure of [ReadMeGeneratorTool](../Azure.Sdk.Tools.Cli/Tools/Package/ReadMeGeneratorTool.cs)
  - Constructor DI:
    - `ILogger<SampleGeneratorTool>`
    - `IOutputHelper`
    - `IMicroagentHostService` (for AI-powered sample generation)
    - `ILanguageRepoServiceFactory` to get `ILanguageRepoService` for post-gen format/lint
    - `IDockerService` for Docker container operations (Phase 2: used for sample verification when `--verify` flag is set)
    - `IRagService` for retrieval-augmented generation (Phase 4: optional dependency for enhanced context)
    - `IGitHelper`, `IProcessHelper` as needed

- Language-specific output locations (defaults):
  - dotnet: `samples/` under package ([azure-sdk-for-net](https://github.com/Azure/azure-sdk-for-net))
  - java: `samples/` under package ([azure-sdk-for-java](https://github.com/Azure/azure-sdk-for-java))
  - js/ts: `samples/` under package ([azure-sdk-for-js](https://github.com/Azure/azure-sdk-for-js))
  - python: `samples/` under package ([azure-sdk-for-python](https://github.com/Azure/azure-sdk-for-python))
  - go: `sample/` or `examples/` (prefer `samples/` for consistency) ([azure-sdk-for-go](https://github.com/Azure/azure-sdk-for-go))
  - Compute from `--package-path` when `--output-directory` not provided

- Generation flow (CLI and MCP share core method):

  **Phase 1 - Basic Generation:**
  1. Validate `--package-path` exists and discover repo root with `IGitHelper.DiscoverRepoRoot()`
  2. Auto-detect language from repo root (e.g., folder name patterns) if not specified; otherwise use `ILanguageRepoServiceFactory`
  3. Resolve output directory default per language; create dirs unless `--dry-run`
  4. Parse the user prompt to identify individual scenarios (single scenario vs multiple scenarios with numbered lists, bullet points, etc.)
  5. Look for and parse `sample.env` file in the package directory to extract environment variable names and example values
  6. If `--template-path` provided: copy and patch placeholders as starting point
  7. Use `IMicroagentHostService` with the user prompt, package context, language-specific instructions, scenario parsing, and environment variable information to generate appropriate sample code that uses the discovered env vars when applicable
  8. Write files unless `--dry-run`; honor `--overwrite` (generate descriptive filenames for multiple scenarios)
  9. Run language service format/lint: `FormatCodeAsync` and `LintCodeAsync` (best-effort) following standard repo procedures
  10. Return `SamplesGenerationResponse` with files written and any warnings

  **Phase 2 - Verification Loop (when `--verify` flag is set):**
  11. If `--verify` flag set: use `IDockerService.CreateContainerAsync()` to create container, copy samples via `CopyToAsync()`, run language-specific typecheck and execution commands via `ExecAsync()` following standard repo procedures for each language
  12. If verification fails: regenerate samples with feedback from verification errors (up to 3 attempts)
  13. Only finalize samples that pass all verification checks
  **Phase 4 - RAG-Enhanced Generation (when RAG is available):**
  15. If RAG service is available: discover and index existing samples across the language repository
  16. Use RAG service to find relevant existing samples based on user prompt and package context
  17. Enhance AI generation prompt with relevant existing sample patterns and code snippets
  18. Generate samples that follow established patterns and conventions from the repository
  19. Return `SamplesGenerationResponse` with RAG context information and pattern adherence metrics

- Safety and validation
  - Never modify files outside package directory
  - If file exists and `--overwrite` not set, skip and add a warning
  - Respect `CancellationToken`
  - Environment variable discovery and usage:
    - Look for `sample.env`, `.env.sample`, or similar files in package directory
    - Parse environment variables in standard .env format (KEY=value, comments with #)
    - Provide environment variable context to AI with variable names, example values, and inferred purposes
    - AI should generate samples that use these environment variables with appropriate language-specific patterns:
      - .NET: `Environment.GetEnvironmentVariable("VAR_NAME")` or configuration patterns
      - Python: `os.environ["VAR_NAME"]` or `os.getenv("VAR_NAME")`
      - JavaScript/TypeScript: `process.env.VAR_NAME`
      - Java: `System.getenv("VAR_NAME")`
      - Go: `os.Getenv("VAR_NAME")`
  - Language-specific verification procedures (standard repo practices):
    - .NET: Use `dotnet build` for compilation, `dotnet run` for execution, follow package-specific build scripts ([azure-sdk-for-net](https://github.com/Azure/azure-sdk-for-net))
    - Python: Use `python -m py_compile` for syntax check, `python script.py` for execution, follow repo's tox/pytest patterns ([azure-sdk-for-python](https://github.com/Azure/azure-sdk-for-python))
    - JavaScript/TypeScript: Use `npm install`, `tsc` for TypeScript compilation, `node script.js` for execution, follow package.json scripts ([azure-sdk-for-js](https://github.com/Azure/azure-sdk-for-js))
    - Java: Use `mvn compile` or `gradle build` for compilation, `java` for execution, follow repo's build system ([azure-sdk-for-java](https://github.com/Azure/azure-sdk-for-java))
    - Go: Use `go build` for compilation, `go run` for execution, follow go.mod patterns ([azure-sdk-for-go](https://github.com/Azure/azure-sdk-for-go))

## Code pointers and exact edits

1) Create tool file
   - Path: `Azure.Sdk.Tools.Cli/Tools/Generators/SampleGeneratorTool.cs`
   - Content outline:
     - Class `SampleGeneratorTool : MCPTool`
     - `CommandHierarchy = [ SharedCommandGroups.Generators ]`
     - `GetCommand()` returns `new Command("samples", "Generate SDK sample code")` with options listed above and `SetHandler`
     - `HandleCommand` parses options, calls `GenerateSamplesAsync` and `output.Output(result)`
     - MCP method as specified above

   References:
   - [SharedCommandGroups.cs](../Azure.Sdk.Tools.Cli/Commands/SharedCommandGroups.cs)
   - [SharedOptions.cs](../Azure.Sdk.Tools.Cli/Commands/SharedOptions.cs)
   - [ReadMeGeneratorTool.cs](../Azure.Sdk.Tools.Cli/Tools/Package/ReadMeGeneratorTool.cs)

2) Register tool with CLI
   - Edit: [SharedOptions.cs](../Azure.Sdk.Tools.Cli/Commands/SharedOptions.cs)
   - Append to `ToolsList`:
     - `typeof(SampleGeneratorTool),`
   - Add `using Azure.Sdk.Tools.Cli.Tools.Generators;` at top if needed

3) Response model
   - Path: `Azure.Sdk.Tools.Cli/Models/Responses/SamplesGenerationResponse.cs`
   - Minimal properties (Phase 1):
     - `IEnumerable<string> FilesWritten`
     - `IEnumerable<string> Warnings`
     - `IEnumerable<string> EnvironmentVariablesUsed` (env vars discovered from sample.env and used in generated samples)
     - `int ScenariosGenerated` (number of scenarios identified and generated)
     - `string? Language`
     - `string OutputRoot`
     - `bool DryRun`
   - Enhanced properties (Phase 2):
     - `IEnumerable<string> VerificationResults` (container execution outputs)
     - `bool Verified`
     - `int VerificationAttempts` (number of generate-verify cycles performed)
     - `bool VerificationPassed` (true only if all samples pass verification)
   - RAG properties (Phase 4):
     - `IEnumerable<string> RelevantSamplesFound` (paths to existing samples used for context)
     - `int SamplesIndexed` (total samples discovered and indexed from repository)
     - `double ContextRelevanceScore` (0.0-1.0 score of how relevant the RAG context was)
     - Override `ToString()` to produce readable output; ensure JSON attributes match conventions (see [docs/new-tool.md](./new-tool.md))

4) Docker service (general-purpose) - **Phase 2**
   - Path: `Azure.Sdk.Tools.Cli/Services/IDockerService.cs`
   - Factory method to create containers:
     - `Task<IContainer> CreateContainerAsync(ContainerOptions? options = null, CancellationToken ct = default)`
   - Container options class:
     - `string? Image` (default: "ubuntu:latest")
     - `bool Persistent` (default: false - auto-purge when done)
     - `bool NetworkEnabled` (default: false)
     - `Dictionary<string, string>? EnvironmentVariables`
     - `PortBinding[]? Ports`
     - `string? WorkingDirectory`
   - Path: `Azure.Sdk.Tools.Cli/Services/IContainer.cs`
   - Container interface methods (modeled after GenAIScript):
     - `Task<ExecResult> ExecAsync(string command, string[]? args = null, CancellationToken ct = default)`
     - `Task WriteTextAsync(string path, string content, CancellationToken ct = default)`
     - `Task<string> ReadTextAsync(string path, CancellationToken ct = default)`
     - `Task CopyToAsync(string sourcePath, string containerPath, CancellationToken ct = default)`
     - `Task CopyFromAsync(string containerPath, string destinationPath, CancellationToken ct = default)`
     - `Task DisconnectNetworkAsync(CancellationToken ct = default)`
     - `Task DisposeAsync()` (for cleanup)
   - Supporting types:
     - `ExecResult` class with `int ExitCode`, `string StdOut`, `string StdErr`
     - `PortBinding` class with `string ContainerPort`, `int HostPort`
     - `ContainerOptions` class as described above
   - Path: `Azure.Sdk.Tools.Cli/Services/DockerService.cs`
   - Implementation using Docker CLI via `IProcessHelper`
   - Reusable across any tool that needs Docker functionality
   - Usage example: `using var container = await dockerService.CreateContainerAsync(new ContainerOptions { Image = "node:20" });`
   - Language-specific verification in containers:
     - .NET: Use `mcr.microsoft.com/dotnet/sdk:8.0` image, copy package files, run `dotnet restore`, `dotnet build`, `dotnet run` ([azure-sdk-for-net](https://github.com/Azure/azure-sdk-for-net) patterns)
     - Python: Use `python:3.11` image, copy requirements/setup files, run `pip install`, `python -m py_compile`, `python script.py` ([azure-sdk-for-python](https://github.com/Azure/azure-sdk-for-python) patterns)
     - JavaScript/TypeScript: Use `node:20` image, copy package.json, run `npm install`, `tsc` (if TypeScript), `node script.js` ([azure-sdk-for-js](https://github.com/Azure/azure-sdk-for-js) patterns)
     - Java: Use `openjdk:17` image, copy pom.xml/build.gradle, run `mvn compile` or `gradle build`, `java` execution ([azure-sdk-for-java](https://github.com/Azure/azure-sdk-for-java) patterns)
     - Go: Use `golang:1.21` image, copy go.mod, run `go mod download`, `go build`, `go run` ([azure-sdk-for-go](https://github.com/Azure/azure-sdk-for-go) patterns)

5) RAG service (general-purpose) - **Phase 4**
   - Path: `Azure.Sdk.Tools.Cli/Services/IRagService.cs`
   - Interface methods (API-agnostic design):
     - `Task IndexRepositoryAsync(string repositoryPath, string language, CancellationToken ct = default)`
     - `Task<IEnumerable<RelevantSample>> FindRelevantSamplesAsync(string query, string language, int maxResults = 5, CancellationToken ct = default)`
     - `Task<string> GenerateContextPromptAsync(string userPrompt, IEnumerable<RelevantSample> relevantSamples, CancellationToken ct = default)`
     - `Task<bool> IsIndexAvailableAsync(string repositoryPath, string language, CancellationToken ct = default)`
     - `Task ClearIndexAsync(string repositoryPath, string language, CancellationToken ct = default)`
   - Supporting types:
     - `RelevantSample` class with `string FilePath`, `string Content`, `double RelevanceScore`, `string Summary`, `IEnumerable<string> Tags`
     - `IndexMetadata` class with `DateTime LastIndexed`, `int SampleCount`, `string Language`, `string RepositoryPath`
   - Path: `Azure.Sdk.Tools.Cli/Services/RagService.cs`
   - Implementation using configurable backends:
     - File-based indexing with simple text search (default, no external dependencies)
     - Optional: Vector embeddings using Azure OpenAI or other embedding services
     - Optional: Local vector database integration (SQLite with vector extensions)
   - Repository sample discovery:
     - Scan for sample files using language-specific patterns (*.cs, *.py, *.js, *.java, *.go)
     - Extract code patterns, API usage, and common structures
     - Index by package name, service type, and code patterns
     - Support incremental indexing for performance
   - Sample analysis capabilities:
     - Identify authentication patterns per language
     - Extract environment variable usage patterns
     - Recognize common error handling approaches
     - Catalog service-specific API usage patterns

6) Optional helper (if you want a clean separation)
   - Path: `Azure.Sdk.Tools.Cli/Helpers/SamplesHelper.cs`
   - Methods:
     - `DetectLanguageFromRepoRoot(string repoRoot)`
     - `GetDefaultSamplesPath(string packagePath, string language)`
     - `ParseScenariosFromPrompt(string prompt)` (returns list of individual scenario descriptions)
     - `GenerateFileNamesForScenarios(IEnumerable<string> scenarios, string language)` (creates descriptive filenames)
     - `ParseSampleEnvFile(string packagePath)` (looks for sample.env and parses environment variables)
     - `Dictionary<string, string> ExtractEnvironmentVariables(string envFileContent)` (parses .env format)
     - `GetLanguageVerificationCommands(string language, string packagePath)` (returns standard typecheck and run commands for the language)
     - `GetLanguageDockerImage(string language)` (returns appropriate base Docker image for verification)
     - These keep logic out of the tool and facilitate testing

6) Tests
   - Path: `Azure.Sdk.Tools.Cli.Tests/Tools/Generators/SampleGeneratorToolTests.cs`
   - Unit tests:
     - Parses CLI options; requires `--package-path` and `--prompt`
     - Dry-run writes no files and lists intended paths
     - Overwrite behavior when files exist
     - Auto-detect language from repo
     - Multi-scenario prompt parsing (single vs multiple scenarios)
     - Environment variable parsing from sample.env files
     - Language-specific verification command generation
     - Container verification flow with mocked Docker service
   - Mock `IMicroagentHostService` and `IDockerService` to avoid network/LLM/Docker calls
   - Reuse `TestClients` pattern from other tool tests

7) Docs and examples
   - Update: [Azure.Sdk.Tools.Cli/README.md](../Azure.Sdk.Tools.Cli/README.md)
   - Add examples:
     - `dotnet run --project Azure.Sdk.Tools.Cli -- generators samples --package-path ./azure-sdk-for-net/sdk/storage/Azure.Storage.Blobs --prompt "Upload a file to blob storage"`
     - `dotnet run --project Azure.Sdk.Tools.Cli -- generators samples --package-path ./azure-sdk-for-js/sdk/servicebus/service-bus --prompt "Create samples for: 1) Send a message, 2) Receive messages, 3) Create a topic and subscription" --verify --dry-run`## Dependencies and configuration

**Phase 1 Dependencies:**
- Reuse existing DI registrations in [ServiceRegistrations.cs](../Azure.Sdk.Tools.Cli/Services/ServiceRegistrations.cs)
  - `ILanguageRepoServiceFactory` for post-gen formatting/linting
  - `IMicroagentHostService` for AI-powered generation
- External dependencies:
  - Azure OpenAI config: `AZURE_OPENAI_ENDPOINT` and credential as configured by `AzureService`

**Phase 2 Dependencies (Verification):**
- New service registration required:
  - `IDockerService` -> `DockerService` as scoped service (general-purpose Docker operations)
**Phase 4 Dependencies (RAG Enhancement):**
- New service registration required:
  - `IRagService` -> `RagService` as scoped service (general-purpose retrieval-augmented generation)
- External dependencies:
  - Optional: Vector database or embedding service (service should work with multiple backends)
  - File system access for indexing existing samples across repositories

## Non-goals / deferrals

**Phase 1:**
- Not implementing Docker verification in initial release; focus on core generation functionality
- Not implementing advanced template systems; basic template support only
- Not changing `ILanguageRepoService` interface; formatting/linting calls are best-effort

**Phase 2:**
- Not implementing real-time verification feedback during generation (post-generation verification only)
- Not implementing custom verification rules per package (use standard language verification only)

**Future Considerations:**
- Advanced template systems with variable substitution
- Integration with package-specific test suites
- Real-time collaboration features

## Edge cases

**Phase 1 Edge Cases:**
- Invalid `--package-path` (not a directory or outside repo) -> fail with clear error
- Existing files without `--overwrite` -> skip and warn
- Missing `pwsh` when running verify/format steps -> capture process error but do not crash the tool
- `--language` provided but does not match repo -> warn and proceed with provided language
- Missing `sample.env` file -> proceed without environment variable context (log as info, not warning)
- Malformed `sample.env` file -> log parsing errors as warnings but continue with available environment variables

**Phase 2 Edge Cases (Verification):**
- Docker not available when `--verify` is used -> fail with clear error message and guidance
- Container verification failures -> regenerate samples with feedback (up to 3 attempts)
- Language-specific build/verification failures -> capture output and include in verification results, attempt regeneration
- Missing build files (package.json, pom.xml, go.mod, etc.) -> create minimal build files or skip verification for that aspect
- Verification loop timeout -> fail gracefully after maximum attempts and provide debugging information

## Rollout checklist

### Phase 1: Core Sample Generation (MVP)
- [ ] Add tool and response class files (without verification)
- [ ] Register tool in DI container
- [ ] Implement basic sample generation with AI
- [ ] Environment variable parsing from sample.env
- [ ] Multi-scenario prompt parsing
- [ ] Language detection and appropriate output paths
- [ ] Unit tests for core functionality
- [ ] README updated with basic usage examples
- [ ] **Milestone: Working sample generation tool**

### Phase 2: Verification and Quality Loop
- [ ] Add general-purpose Docker service interface and implementation
- [ ] Implement language-specific verification procedures
- [ ] Add verification loop: generate → verify → regenerate if needed → finalize
- [ ] Docker integration tests (optional: can be integration tests that require Docker)
- [ ] Language-specific Dockerfile templates for sample verification
- [ ] Enhanced response model with verification results
- [ ] **Milestone: Samples validated before finalization**

### Phase 3: Polish and Optimization
- [ ] Performance optimizations for verification loop
- [ ] Advanced error handling and recovery
- [ ] Template system enhancements
- [ ] Additional language-specific features

### Phase 4: RAG-Enhanced Sample Generation
- [ ] Add general-purpose RAG service interface and implementation
- [ ] Implement sample discovery and indexing across language repositories
- [ ] Integrate RAG service with sample generation for context-aware examples
- [ ] Add repository-wide sample analysis and pattern recognition
- [ ] Enhanced AI prompting with relevant existing sample context
- [ ] **Milestone: Context-aware sample generation using existing repository patterns**