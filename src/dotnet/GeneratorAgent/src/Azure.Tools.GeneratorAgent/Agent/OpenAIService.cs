using System.Text.Json;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Constants;
using Azure.Tools.GeneratorAgent.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Azure.Tools.GeneratorAgent;

/// <summary>
/// OpenAI service implementation that provides AI-powered code fixing capabilities
/// </summary>
internal class OpenAIService
{
    private readonly ChatClient ChatClient;
    private readonly AppSettings AppSettings;
    private readonly FormatPromptService FormatPromptService;
    private readonly ToolExecutor ToolExecutor;
    private readonly KnowledgeBaseService KnowledgeBaseService;
    private readonly ILogger<OpenAIService> Logger;

    public OpenAIService(
        ChatClient chatClient,
        AppSettings appSettings,
        FormatPromptService formatPromptService,
        ToolExecutor toolExecutor,
        KnowledgeBaseService knowledgeBaseService,
        ILogger<OpenAIService> logger)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(appSettings);
        ArgumentNullException.ThrowIfNull(formatPromptService);
        ArgumentNullException.ThrowIfNull(toolExecutor);
        ArgumentNullException.ThrowIfNull(knowledgeBaseService);
        ArgumentNullException.ThrowIfNull(logger);

        ChatClient = chatClient;
        AppSettings = appSettings;
        FormatPromptService = formatPromptService;
        ToolExecutor = toolExecutor;
        KnowledgeBaseService = knowledgeBaseService;
        Logger = logger;
    }


    public async Task<IEnumerable<RuleError>> AnalyzeErrorsAsync(string errorLogs, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(errorLogs))
        {
            Logger.LogWarning("Error logs are null or empty, returning empty results");
            return Enumerable.Empty<RuleError>();
        }

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(AppSettings.ErrorAnalysisInstructions),
            new UserChatMessage(errorLogs)
        };

        Logger.LogDebug("Sending error analysis request to OpenAI");

        var response = await ChatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);

        if (response?.Value == null || response.Value.Content == null || response.Value.Content.Count == 0)
        {
            Logger.LogWarning("OpenAI response is null or does not contain any content.");
            return Enumerable.Empty<RuleError>();
        }

        var jsonResponse = response.Value.Content[0].Text;

        Logger.LogDebug("Raw OpenAI error analysis response: {Response}", jsonResponse);

        return AgentResponseParser.ParseErrors(jsonResponse);
    }

    public async Task<PatchRequest> GenerateFixesAsync(List<Fix> fixes, ValidationContext validationContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fixes);
        ArgumentNullException.ThrowIfNull(validationContext);

        var prompt = FormatPromptService.ConvertFixesToBatchPrompt(fixes);

        // Combine system instructions with knowledge base
        var systemMessage = BuildSystemMessage();

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemMessage),
            new UserChatMessage(prompt)
        };

        var tools = CreateToolDefinitions();
        var options = new ChatCompletionOptions();
        foreach (var tool in tools)
        {
            options.Tools.Add(tool);
        }

        Logger.LogDebug("Sending request to OpenAI with {MessageCount} messages and {ToolCount} tools", 
            messages.Count, tools.Count);
        
        var response = await ChatClient.CompleteChatAsync(messages, options, cancellationToken);

        string result;

        // Handle tool calls if any
        if (response.Value.FinishReason == ChatFinishReason.ToolCalls)
        {
            result = await HandleToolCallsAsync(ChatClient, messages, response.Value, validationContext, cancellationToken);
        }
        else
        {
            if (response.Value.Content != null && response.Value.Content.Count > 0)
            {
                result = response.Value.Content[0].Text;
            }
            else
            {
                Logger.LogWarning("OpenAI response content is empty in GenerateFixesAsync.");
                result = string.Empty;
            }
        }

        Logger.LogDebug("Raw OpenAI file change response: {Response}", result);

        return AgentResponseParser.ParsePatchRequest(result);
    }

    /// <summary>
    /// Builds the system message by combining agent instructions with knowledge base content
    /// </summary>
    private string BuildSystemMessage()
    {
        var systemMessage = AppSettings.AgentInstructions;
        
        if (KnowledgeBaseService.IsKnowledgeBaseAvailable)
        {
            var knowledgeBase = KnowledgeBaseService.GetKnowledgeBase();
            systemMessage = $"{systemMessage}\n\n## TypeSpec Knowledge Base\n\n{knowledgeBase}";
            
            Logger.LogDebug("Added knowledge base to system message ({KnowledgeSize} characters)", 
                knowledgeBase.Length);
        }
        else
        {
            Logger.LogWarning("Knowledge base is not available, using only basic agent instructions");
        }
        
        return systemMessage;
    }

    /// <summary>
    /// Creates tool definitions for OpenAI function calling
    /// </summary>
    private List<ChatTool> CreateToolDefinitions()
    {
        return new List<ChatTool>
        {
            ChatTool.CreateFunctionTool(
                ToolNames.ListTypeSpecFiles,
                "Lists all TypeSpec files with complete content, metadata, version, line count, and SHA256 for comprehensive analysis",
                BinaryData.FromString(JsonSerializer.Serialize(new
                {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                }))
            ),
            ChatTool.CreateFunctionTool(
                ToolNames.GetTypeSpecFile,
                "Retrieves the content of a specific TypeSpec file with metadata",
                BinaryData.FromString(JsonSerializer.Serialize(new
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
                }))
            )
        };
    }

    /// <summary>
    /// Handles tool calls from OpenAI response
    /// </summary>
    private async Task<string> HandleToolCallsAsync(ChatClient chatClient, List<ChatMessage> messages, ChatCompletion completion, ValidationContext validationContext, CancellationToken cancellationToken)
    {        
        // Add the assistant's response with tool calls
        messages.Add(new AssistantChatMessage(completion));

        // Execute each tool call
        foreach (var toolCall in completion.ToolCalls)
        {
            if (toolCall is ChatToolCall functionCall)
            {
                Logger.LogDebug("Executing tool: {ToolName}", functionCall.FunctionName);
                var argumentsJson = functionCall.FunctionArguments.ToString();
                var result = await ToolExecutor.ExecuteToolCallAsync(functionCall.FunctionName, argumentsJson, validationContext, cancellationToken);
                messages.Add(new ToolChatMessage(functionCall.Id, result));
            }
        }
        
        // Keep calling until we get a non-tool response
        ChatCompletion finalResponse;
        int maxIterations = 10; // Prevent infinite loops
        int iteration = 0;
        
        do
        {
            var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
            finalResponse = response.Value;
            
            // If we get more tool calls, handle them
            if (finalResponse.FinishReason == ChatFinishReason.ToolCalls)
            {
                messages.Add(new AssistantChatMessage(finalResponse));
                
                foreach (var toolCall in finalResponse.ToolCalls)
                {
                    if (toolCall is ChatToolCall functionCall)
                    {
                        Logger.LogDebug("Executing additional tool: {ToolName} (iteration {Iteration})", 
                            functionCall.FunctionName, iteration + 1);
                        var argumentsJson = functionCall.FunctionArguments.ToString();
                        var result = await ToolExecutor.ExecuteToolCallAsync(functionCall.FunctionName, argumentsJson, validationContext, cancellationToken);
                        messages.Add(new ToolChatMessage(functionCall.Id, result));
                    }
                }
            }
            
            iteration++;
        } 
        while (finalResponse.FinishReason == ChatFinishReason.ToolCalls && iteration < maxIterations);
        
        if (iteration >= maxIterations)
        {
            Logger.LogWarning("Maximum tool call iterations reached ({MaxIterations}), stopping", maxIterations);
        }
        
        if (finalResponse?.Content != null && finalResponse.Content.Count > 0 && finalResponse.Content[0] != null)
        {
            return finalResponse.Content[0].Text;
        }
        else
        {
            Logger.LogWarning("OpenAI response did not contain any content after tool calls.");
            return string.Empty;
        }
    }
}