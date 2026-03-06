// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
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
    private readonly TokenUsageHelper tokenUsageHelper;
    private readonly ITypeSpecHelper typeSpecHelper;
    private readonly IGitHelper gitHelper;

    public TypeSpecCustomizationService(
        ILogger<TypeSpecCustomizationService> logger,
        ICopilotAgentRunner copilotAgentRunner,
        INpxHelper npxHelper,
        TokenUsageHelper tokenUsageHelper,
        ITypeSpecHelper typeSpecHelper,
        IGitHelper gitHelper)
    {
        this.logger = logger;
        this.copilotAgentRunner = copilotAgentRunner;
        this.npxHelper = npxHelper;
        this.tokenUsageHelper = tokenUsageHelper;
        this.typeSpecHelper = typeSpecHelper;
        this.gitHelper = gitHelper;
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

        // Find reference doc path if not provided
        if (string.IsNullOrEmpty(referenceDocPath))
        {
            referenceDocPath = await FindReferenceDocAsync(typespecProjectPath, ct) ?? throw new FileNotFoundException(
                    "Could not find customizing-client-tsp.md reference document. Please provide the reference doc path.");
        }

        if (!File.Exists(referenceDocPath))
        {
            throw new FileNotFoundException(
                $"Reference document not found: {referenceDocPath}", referenceDocPath);
        }

        logger.LogInformation("Using reference doc: {RefDoc}", referenceDocPath);

        // Read the reference doc content
        var referenceDocContent = await File.ReadAllTextAsync(referenceDocPath, ct);

        // Build the prompt template
        var template = new TypeSpecCustomizationTemplate(
            customizationRequest: customizationRequest,
            typespecProjectPath: typespecProjectPath,
            referenceDocContent: referenceDocContent);

        var instructions = template.BuildPrompt();
        logger.LogDebug("Generated prompt with {Length} characters", instructions.Length);

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
        return
        [
            FileTools.CreateReadFileTool(typespecProjectPath, description: "Read the contents of a file from the TypeSpec project directory"),
            FileTools.CreateWriteFileTool(typespecProjectPath, "Write content to a file in the TypeSpec project directory"),
            TypeSpecTools.CreateCompileTypeSpecTool(typespecProjectPath, npxHelper)
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
