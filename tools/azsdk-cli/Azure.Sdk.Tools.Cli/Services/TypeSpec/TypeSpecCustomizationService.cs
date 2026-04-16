// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;
using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Services.TypeSpec;

/// <summary>
/// Service for applying TypeSpec client.tsp customizations using a copilot agent.
/// </summary>
public interface ITypeSpecCustomizationService
{
    /// <summary>
    /// Applies TypeSpec client.tsp customizations based on the provided request.
    /// </summary>
    /// <param name="typespecProjectPath">Path to the TypeSpec project directory (must contain tspconfig.yaml)</param>
    /// <param name="customizationRequest">Description of the customization to apply</param>
    /// <param name="referenceDocPath">Optional path to the customizing-client-tsp.md reference document</param>
    /// <param name="maxIterations">Maximum number of iterations the copilot agent can make (default: 20)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the customization operation</returns>
    Task<TypeSpecCustomizationServiceResult> ApplyCustomizationAsync(
        string typespecProjectPath,
        string customizationRequest,
        string? referenceDocPath = null,
        int maxIterations = 20,
        CancellationToken ct = default);
}

/// <summary>
/// Service for applying TypeSpec client.tsp customizations using a copilot agent.
/// </summary>
public class TypeSpecCustomizationService : ITypeSpecCustomizationService
{
    private readonly ILogger<TypeSpecCustomizationService> logger;
    private readonly ICopilotAgentRunner copilotAgentRunner;
    private readonly INpxHelper npxHelper;
    private readonly ITypeSpecHelper typeSpecHelper;
    private readonly IAzureSdkKnowledgeBaseService azureSdkKnowledgeBaseService;
    private readonly IGitHelper gitHelper;
    private readonly ILoggerFactory loggerFactory;

    public TypeSpecCustomizationService(
        ILogger<TypeSpecCustomizationService> logger,
        ICopilotAgentRunner copilotAgentRunner,
        INpxHelper npxHelper,
        TokenUsageHelper tokenUsageHelper,
        ITypeSpecHelper typeSpecHelper,
        IAzureSdkKnowledgeBaseService azureSdkKnowledgeBaseService,
        IGitHelper gitHelper,
        ILoggerFactory loggerFactory)
    {
        this.logger = logger;
        this.copilotAgentRunner = copilotAgentRunner;
        this.npxHelper = npxHelper;
        this.typeSpecHelper = typeSpecHelper;
        this.azureSdkKnowledgeBaseService = azureSdkKnowledgeBaseService;
        this.gitHelper = gitHelper;
        this.loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown when typespecProjectPath is invalid (doesn't exist or missing tspconfig.yaml).</exception>
    /// <exception cref="FileNotFoundException">Thrown when the reference document cannot be found.</exception>
    public async Task<TypeSpecCustomizationServiceResult> ApplyCustomizationAsync(
        string typespecProjectPath,
        string customizationRequest,
        string? referenceDocPath = null,
        int maxIterations = 20,
        CancellationToken ct = default)
    {
        logger.LogInformation("Starting TypeSpec customization for project: {Path}", typespecProjectPath);
        logger.LogInformation("Customization request: {Request}", customizationRequest);

        // Validate TypeSpec project path using existing helper
        if (!typeSpecHelper.IsValidTypeSpecProjectPath(typespecProjectPath))
        {
            throw new ArgumentException(
                $"Invalid TypeSpec project path: {typespecProjectPath}. Directory must exist and contain tspconfig.yaml.",
                nameof(typespecProjectPath));
        }
        
        var instructions = $"""
            You are applying TypeSpec client customizations to a client.tsp file.

            **TypeSpec Project Path:** {typespecProjectPath}

            **Working Directory for Tools:**
            All file operations use RELATIVE paths from the TypeSpec project directory above.
            - To read client.tsp, use: ReadFile("client.tsp")
            - To read main.tsp, use: ReadFile("main.tsp")
            - To read files in subdirectories, use: ReadFile("connections/models.tsp")
            - Do NOT use absolute paths or paths relative to the repository root
            - The WriteFile and CompileTypeSpec tools also use relative paths from this directory

            **Your Tasks:**
            step 1: Understand the customization request: {customizationRequest}, and read the relavant '.tsp' code from the project to understand the context.
            step 2: invoke `azure-sdk-mcp:azsdk_typespec_generate_authoring_plan` with:

            | Parameter                 | Value                                                                                                                                                                       |
            | ------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
            | `request`                 |  {customizationRequest} (verbatim)|
            | `additionalInformation`   | All content gathered from Steps 1 (analysis, relevant `.tsp` code read from the project) |
            | `typeSpecProjectRootPath` | {typespecProjectPath}|
            step 3: apply the solution return from step 2
            """;
        // Create the tools using shared tool factories
        var tools = CreateTools(typespecProjectPath);

        // Create and run the copilot agent
        var agent = new CopilotAgent<TypeSpecCustomizationServiceResult>
        {
            Instructions = instructions,
            Tools = tools,
            MaxIterations = maxIterations,
            Model = "claude-opus-4.5"
        };

        logger.LogInformation("Running TypeSpecCustomization copilot agent with {ToolCount} tools and max {MaxIterations} iterations...",
            tools.Count, maxIterations);

        var result = await copilotAgentRunner.RunAsync(agent, ct);

        logger.LogInformation("TypeSpecCustomization copilot agent completed. Success: {Success}", result.Success);

        return result;
    }

    /// <summary>
    /// Creates the tools for the copilot agent using shared tool factories.
    /// </summary>
    private List<AIFunction> CreateTools(string typespecProjectPath)
    {
        // Create an instance of TypeSpecAuthoringTool with required dependencies
        var authoringToolLogger = loggerFactory.CreateLogger<Tools.TypeSpec.TypeSpecAuthoringTool>();
        var typeSpecAuthoringToolInstance = new Tools.TypeSpec.TypeSpecAuthoringTool(
            azureSdkKnowledgeBaseService,
            authoringToolLogger,
            typeSpecHelper);

        return
        [
            FileTools.CreateReadFileTool(typespecProjectPath, description: "Read the contents of a file from the TypeSpec project directory"),
            FileTools.CreateWriteFileTool(typespecProjectPath, "Write content to a file in the TypeSpec project directory"),
            TypeSpecTools.CreateCompileTypeSpecTool(typespecProjectPath, npxHelper),
            TypeSpecTools.CreateTypeSpecAuthoringTool(typeSpecAuthoringToolInstance)
        ];
    }

    /// <summary>
    /// Tries to find the customizing-client-tsp.md reference document by looking 
    /// in eng/common/knowledge/ under the repository root.
    /// </summary>
    private async Task<string?> FindReferenceDocAsync(string typespecProjectPath, CancellationToken ct)
    {
        var repoRoot = await gitHelper.DiscoverRepoRootAsync(typespecProjectPath, ct);
        if (string.IsNullOrEmpty(repoRoot))
        {
            return null;
        }
        var candidate = Path.Combine(repoRoot, "eng", "common", "knowledge", "customizing-client-tsp.md");
        return File.Exists(candidate) ? candidate : null;
    }
}
