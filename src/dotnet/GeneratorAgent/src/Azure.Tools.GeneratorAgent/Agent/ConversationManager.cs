using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;
using Azure.Tools.GeneratorAgent.Configuration;

namespace Azure.Tools.GeneratorAgent.Agent;

/// <summary>
/// Manages a single conversation with an Azure AI agent, handling tool calls cleanly
/// </summary>
internal class ConversationManager
{
    private readonly PersistentAgentsClient Client;
    private readonly ToolExecutor ToolExecutor;
    private readonly AppSettings AppSettings;
    private readonly ILogger<ConversationManager> Logger;
    private ValidationContext? ValidationContext;
    
    private string? _agentId;
    
    public string? AgentId
    {
        get => _agentId;
        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(AgentId));
            _agentId = value;
        }
    }
    public string? ThreadId { get; private set; }

    public ConversationManager(
        PersistentAgentsClient client,
        ToolExecutor toolExecutor,
        AppSettings appSettings,
        ILogger<ConversationManager> logger)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        ToolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        AppSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // ThreadId starts as null until conversation is started
    }

    /// <summary>
    /// Sets the validation context on the tool executor
    /// </summary>
    public void SetValidationContext(ValidationContext validationContext)
    {
        ValidationContext = validationContext;
    }

    /// <summary>
    /// Creates a new conversation thread
    /// </summary>
    public async Task<string> StartConversationAsync(CancellationToken cancellationToken = default)
    {
        var threadResponse = await Client.Threads.CreateThreadAsync(cancellationToken: cancellationToken);
        ThreadId = threadResponse.Value.Id;
        
        Logger.LogDebug("Created conversation thread: {ThreadId}", ThreadId);
        return ThreadId;
    }

    /// <summary>
    /// Sends a message to the agent and waits for the complete response
    /// </summary>
    public async Task<string> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ThreadId))
        {
            throw new InvalidOperationException("Conversation not started. Call StartConversationAsync first.");
        }

        // Step 1: Add user message to thread
        await Client.Messages.CreateMessageAsync(ThreadId, MessageRole.User, message, cancellationToken: cancellationToken);

        // Step 2: Create and process the run
        var response = await ProcessRunAsync(cancellationToken);
        Logger.LogDebug("Agent Response: {response}", response);
        
        return response;
    }

    /// <summary>
    /// Processes an agent run, handling tool calls and waiting for completion
    /// </summary>
    private async Task<string> ProcessRunAsync(CancellationToken cancellationToken)
    {
        // Create the run
        var runResponse = await Client.Runs.CreateRunAsync(ThreadId, AgentId, cancellationToken: cancellationToken);
        var run = runResponse.Value;
        
        // Poll until completion or tool calls needed
        var maxWaitTime = AppSettings.AgentRunMaxWaitTime;
        var pollingInterval = AppSettings.AgentRunPollingInterval;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (true)
        {
            // Wait before checking status
            await Task.Delay(pollingInterval, cancellationToken);
            
            // Get current run status
            var runUpdateResponse = await Client.Runs.GetRunAsync(ThreadId, run.Id, cancellationToken: cancellationToken);
            run = runUpdateResponse.Value;

            // Check for timeout
            if (stopwatch.Elapsed > maxWaitTime)
            {
                Logger.LogError("Agent run {RunId} timed out after {Elapsed:F1}s", run.Id, stopwatch.Elapsed.TotalSeconds);
                throw new TimeoutException($"Agent run timed out after {stopwatch.Elapsed.TotalSeconds:F1} seconds");
            }

            // Handle different run statuses
            if (run.Status == RunStatus.Completed)
            {
                Logger.LogDebug("Agent run completed in {Elapsed:F1}s with tool calls", 
                    stopwatch.Elapsed.TotalSeconds);
                return await GetFinalResponseAsync(cancellationToken);
            }
            else if (run.Status == RunStatus.RequiresAction)
            {
                Logger.LogDebug("Run {RunId} requires action - processing tool calls", run.Id);
                await HandleToolCallsAsync(run, cancellationToken);
                // Continue polling after handling tool calls
            }
            else if (run.Status == RunStatus.Failed ||
                     run.Status == RunStatus.Cancelled ||
                     run.Status == RunStatus.Expired)
            {
                Logger.LogError("Agent run {RunId} failed with status: {Status} after {Elapsed:F1}s", 
                    run.Id, run.Status, stopwatch.Elapsed.TotalSeconds);
                throw new InvalidOperationException($"Agent run failed with status: {run.Status}");
            }
            // For Queued and InProgress, continue polling
        }
    }

    /// <summary>
    /// Handles tool calls during agent execution
    /// </summary>
    private async Task HandleToolCallsAsync(ThreadRun run, CancellationToken cancellationToken)
    {
        if (run.RequiredAction is not SubmitToolOutputsAction submitToolOutputsAction)
        {
            Logger.LogError("Unsupported required action type: {Type}", 
                run.RequiredAction?.GetType().FullName ?? "null");
            throw new InvalidOperationException($"Unsupported required action type: {run.RequiredAction?.GetType().FullName}");
        }

        var toolOutputs = new List<ToolOutput>();

        foreach (var toolCall in submitToolOutputsAction.ToolCalls)
        {   
            try
            {
                var output = await ExecuteToolCallAsync(toolCall, cancellationToken);
                toolOutputs.Add(new ToolOutput(toolCall.Id, output));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Tool call {ToolCallId} failed", toolCall.Id);
                var errorOutput = CreateErrorResponse($"Tool execution failed: {ex.Message}");
                toolOutputs.Add(new ToolOutput(toolCall.Id, errorOutput));
            }
        }
    
        var toolOutputsData = toolOutputs.Select(to => new 
        {
            tool_call_id = to.ToolCallId,
            output = to.Output
        }).ToArray();
        
        var requestData = new { tool_outputs = toolOutputsData };
        
        await Client.Runs.SubmitToolOutputsToRunAsync(
            ThreadId, 
            run.Id, 
            BinaryData.FromObjectAsJson(requestData),
            new Azure.RequestContext { CancellationToken = cancellationToken });
    }

    /// <summary>
    /// Executes a single tool call using the ToolExecutor
    /// </summary>
    private async Task<string> ExecuteToolCallAsync(RequiredToolCall toolCall, CancellationToken cancellationToken)
    {
        if (toolCall is RequiredFunctionToolCall functionCall)
        {
            var toolName = functionCall.Name;
            var arguments = functionCall.Arguments;
            
            return await ToolExecutor.ExecuteToolCallAsync(toolName, arguments, ValidationContext ?? throw new InvalidOperationException("ValidationContext has not been set"), cancellationToken);
        }
        
        Logger.LogWarning("Unknown tool call type: {Type}", toolCall.GetType().Name);
        return CreateErrorResponse($"Unknown tool call type: {toolCall.GetType().Name}");
    }

    /// <summary>
    /// Retrieves the final response from the agent after run completion
    /// </summary>
    private async Task<string> GetFinalResponseAsync(CancellationToken cancellationToken)
    {
        var messages = Client.Messages.GetMessagesAsync(ThreadId, order: ListSortOrder.Descending, cancellationToken: cancellationToken);
        
        await foreach (var message in messages)
        {
            if (message.Role != MessageRole.User)
            {
                foreach (MessageTextContent content in message.ContentItems.OfType<MessageTextContent>())
                {
                    return content.Text;
                }
            }
        }

        Logger.LogWarning("No assistant response found in thread messages");
        throw new InvalidOperationException("No assistant response found in thread messages");
    }

    /// <summary>
    /// Creates a JSON error response
    /// </summary>
    private static string CreateErrorResponse(string errorMessage)
    {
        var errorResponse = new { error = errorMessage };
        return System.Text.Json.JsonSerializer.Serialize(errorResponse);
    }

    /// <summary>
    /// Deletes the current conversation thread
    /// </summary>
    public async Task DeleteThreadAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ThreadId))
        {
            Logger.LogDebug("No thread to delete - ThreadId is null");
            return;
        }

        try
        {
            await Client.Threads.DeleteThreadAsync(ThreadId, cancellationToken);
            Logger.LogInformation("Deleted conversation thread: {ThreadId}", ThreadId);
            ThreadId = null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete conversation thread: {ThreadId}", ThreadId);
        }
    }
}