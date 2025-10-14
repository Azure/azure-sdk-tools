using Azure.AI.Agents.Persistent;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Constants;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent.Agent;

/// <summary>
/// Tool-based agent that orchestrates the complete error fixing workflow with lazy agent initialization
/// </summary>
internal class ToolBasedAgent : IAsyncDisposable
{
    private readonly ConversationManager ConversationManager;
    private readonly FormatPromptService FormatPromptService;
    private readonly AppSettings AppSettings;
    private readonly PersistentAgentsClient Client;
    private readonly ILogger<ToolBasedAgent> Logger;
    private readonly Lazy<PersistentAgent> Agent;
    private volatile bool _disposed = false;
    private string AgentId => Agent.Value.Id;

    public ToolBasedAgent(
        ConversationManager conversationManager,
        FormatPromptService formatPromptService,
        AppSettings appSettings,
        PersistentAgentsClient client,
        ILogger<ToolBasedAgent> logger)
    {
        ConversationManager = conversationManager ?? throw new ArgumentNullException(nameof(conversationManager));
        FormatPromptService = formatPromptService ?? throw new ArgumentNullException(nameof(formatPromptService));
        AppSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Agent = new Lazy<PersistentAgent>(() => CreateAgent());
    }

    /// <summary>
    /// Sets the validation context for tool execution
    /// </summary>
    public void SetValidationContext(ValidationContext validationContext)
    {
        ConversationManager.SetValidationContext(validationContext);
    }

    /// <summary>
    /// Creates the agent (called only once when Agent.Value is first accessed)
    /// </summary>
    private PersistentAgent CreateAgent()
    {
        // Create tool definitions for TypeSpec operations
        var toolDefinitions = new List<ToolDefinition>
        {
            new FunctionToolDefinition(
                name: ToolNames.ListTypeSpecFiles,
                description: "Lists all TypeSpec files with complete content, metadata, version, line count, and SHA256 for comprehensive analysis",
                parameters: BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                })
            ),
            new FunctionToolDefinition(
                name: ToolNames.GetTypeSpecFile,
                description: "Retrieves the content of a specific TypeSpec file with metadata",
                parameters: BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new
                    {
                        path = new
                        {
                            type = "string",
                            description = "The path to the TypeSpec file to retrieve"
                        }
                    },
                    required = new[] { "path" }
                })
            )
        };

        // Create the agent
        var response = Client.Administration.CreateAgent(
            model: AppSettings.Model,
            name: AppSettings.AgentName,
            description: "TypeSpec expert for fixing Azure SDK analyzer errors",
            instructions: AppSettings.AgentInstructions,
            tools: toolDefinitions);

        var agent = response.Value;
        if (string.IsNullOrEmpty(agent?.Id))
        {
            throw new InvalidOperationException("Failed to create Azure AI agent");
        }

        return agent;
    }

    /// <summary>
    /// Initializes the agent for use (creates agent and starts conversation thread)
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Initializing tool-based agent...");

        ConversationManager.AgentId = AgentId;

        await ConversationManager.StartConversationAsync(cancellationToken);

        Logger.LogInformation("Agent conversation initialized successfully with agent: {AgentId}, thread: {ThreadId}",
            AgentId, ConversationManager.ThreadId);
    }

    /// <summary>
    /// Fixes code based on a list of analyzer errors
    /// </summary>
    public async Task<Result<string>> FixCodeAsync(List<Fix> fixes, CancellationToken cancellationToken = default)
    {
        try
        {
            if (fixes?.Count == 0)
            {
                Logger.LogWarning("No fixes provided to FixCodeAsync");
                return Result<string>.Failure(new ArgumentException("No fixes provided"));
            }

            Logger.LogInformation("Processing {FixCount} fixes with Agent", fixes?.Count);

            // Create comprehensive prompt from fixes
            var batchPrompt = FormatPromptService.ConvertFixesToBatchPrompt(fixes);

            // Send prompt to agent and get response
            var agentResponse = await ConversationManager.SendMessageAsync(batchPrompt, cancellationToken);

            if (string.IsNullOrEmpty(agentResponse))
            {
                Logger.LogError("Agent returned empty response");
                return Result<string>.Failure(new InvalidOperationException("Agent returned empty response"));
            }

            // Clean the response - remove markdown code blocks if present
            var cleanedResponse = CleanJsonResponse(agentResponse);

            // Validate response contains a JSON patch
            if (!IsValidJsonPatch(cleanedResponse))
            {
                Logger.LogWarning("Agent response does not contain valid JSON patch");
                return Result<string>.Failure(new InvalidOperationException("Agent response does not contain valid JSON patch"));
            }

            return Result<string>.Success(cleanedResponse);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to fix code using Agent");
            return Result<string>.Failure(ex);
        }
    }

    /// <summary>
    /// Analyzes error logs and extracts structured error information
    /// </summary>
    public async Task<Result<IEnumerable<RuleError>>> AnalyzeErrorsAsync(string errorLogs, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(errorLogs))
            {
                Logger.LogWarning("Empty error logs provided to AnalyzeErrorsAsync");
                return Result<IEnumerable<RuleError>>.Success(Enumerable.Empty<RuleError>());
            }

            // Create error analysis prompt
            var analysisPrompt = AppSettings.ErrorAnalysisPromptTemplate.Replace("{0}", errorLogs);

            // Send to agent for analysis
            var agentResponse = await ConversationManager.SendMessageAsync(analysisPrompt, cancellationToken);

            if (string.IsNullOrEmpty(agentResponse))
            {
                Logger.LogError("Agent returned empty response for error analysis");
                return Result<IEnumerable<RuleError>>.Failure(new InvalidOperationException("Agent returned empty response for error analysis"));
            }

            var errors = AgentResponseParser.ParseErrors(agentResponse);

            return Result<IEnumerable<RuleError>>.Success(errors);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to analyze errors using Agent");
            return Result<IEnumerable<RuleError>>.Failure(ex);
        }
    }

    private static bool IsValidJsonPatch(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        var containsFile = response.Contains("\"file\"");
        var containsChanges = response.Contains("\"changes\"") || response.Contains("\"reason\"");
        var containsJson = response.TrimStart().StartsWith("{") && response.TrimEnd().EndsWith("}");

        return containsFile && containsChanges && containsJson;
    }

    private string CleanJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        string cleaned = response.Trim();

        // Remove markdown code blocks if present
        if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            // Find the end of the opening ```json
            int startIndex = cleaned.IndexOf('\n');
            if (startIndex == -1) startIndex = 6; // If no newline, start after ```json
            else startIndex++; // Skip the newline

            // Find the closing ```
            int endIndex = cleaned.LastIndexOf("```");
            if (endIndex > startIndex)
            {
                cleaned = cleaned.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }
        else if (cleaned.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            // Handle generic code blocks
            int startIndex = cleaned.IndexOf('\n');
            if (startIndex != -1)
            {
                startIndex++; // Skip the newline
                int endIndex = cleaned.LastIndexOf("```");
                if (endIndex > startIndex)
                {
                    cleaned = cleaned.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
        }

        return cleaned;
    }

    /// <summary>
    /// Disposes the agent and cleans up Azure AI resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            // Delete the conversation thread if it exists
            await ConversationManager.DeleteThreadAsync(CancellationToken.None).ConfigureAwait(false);

            // Only delete the agent if it was actually created
            if (Agent.IsValueCreated)
            {
                // Delete the specific agent we created
                await Client.Administration.DeleteAgentAsync(AgentId, CancellationToken.None).ConfigureAwait(false);
                
                Logger.LogInformation("Agent {AgentId} deleted successfully", AgentId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error cleaning up agent and thread during disposal");
        }
    }
}