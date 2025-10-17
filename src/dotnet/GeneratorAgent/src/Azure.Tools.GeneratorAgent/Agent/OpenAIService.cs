using System.Text.Json;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Constants;
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
    private readonly ILogger<OpenAIService> Logger;

    public OpenAIService(
        ChatClient chatClient,
        AppSettings appSettings,
        FormatPromptService formatPromptService,
        ToolExecutor toolExecutor,
        ILogger<OpenAIService> logger)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(appSettings);
        ArgumentNullException.ThrowIfNull(formatPromptService);
        ArgumentNullException.ThrowIfNull(toolExecutor);
        ArgumentNullException.ThrowIfNull(logger);

        ChatClient = chatClient;
        AppSettings = appSettings;
        FormatPromptService = formatPromptService;
        ToolExecutor = toolExecutor;
        Logger = logger;
    }


    public async Task<IEnumerable<RuleError>> AnalyzeErrorsAsync(string errorLogs, ValidationContext validationContext, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(errorLogs))
        {
            Logger.LogWarning("Empty error logs provided to AnalyzeErrorsAsync");
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
        
        // Clean the response - remove markdown code blocks if present
        var cleanedResponse = CleanJsonResponse(jsonResponse);
        
        var errors = AgentResponseParser.ParseErrors(cleanedResponse);

        Logger.LogDebug("Received OpenAI error analysis response (cleaned): {Response}", cleanedResponse);
        
        return errors;
    }

    public async Task<string> GenerateFixesAsync(IList<Fix> fixes, ValidationContext validationContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fixes);
        ArgumentNullException.ThrowIfNull(validationContext);

        var prompt = FormatPromptService.ConvertFixesToBatchPrompt(fixes.ToList());

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(AppSettings.AgentInstructions),
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
        
        // Clean the response - remove markdown code blocks if present
        var cleanedResult = CleanJsonResponse(result);
        
        Logger.LogDebug("OpenAI Response: {Response}", cleanedResult);
        
        return cleanedResult;
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

    /// <summary>
    /// Cleans JSON response by removing markdown code blocks if present
    /// </summary>
    private static string CleanJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        var trimmedResponse = response.Trim();
        
        // Find first occurrence of ```json (case insensitive)
        var jsonStartMarker = "```json";
        var jsonStartIndex = trimmedResponse.IndexOf(jsonStartMarker, StringComparison.OrdinalIgnoreCase);
        
        if (jsonStartIndex >= 0)
        {
            // Move past the ```json marker and any newlines
            var jsonContentStart = jsonStartIndex + jsonStartMarker.Length;
            
            // Skip any whitespace/newlines after ```json
            while (jsonContentStart < trimmedResponse.Length && 
                   char.IsWhiteSpace(trimmedResponse[jsonContentStart]))
            {
                jsonContentStart++;
            }
            
            // Find the closing ``` marker
            var jsonEndIndex = trimmedResponse.IndexOf("```", jsonContentStart);
            
            if (jsonEndIndex > jsonContentStart)
            {
                // Extract just the JSON content
                return trimmedResponse.Substring(jsonContentStart, jsonEndIndex - jsonContentStart).Trim();
            }
            else
            {
                // No closing marker found, take everything after ```json
                return trimmedResponse.Substring(jsonContentStart).Trim();
            }
        }
        
        // Fallback: handle generic code blocks that start with ```
        if (trimmedResponse.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            int startIndex = trimmedResponse.IndexOf('\n');
            if (startIndex != -1)
            {
                startIndex++; // Skip the newline
                int endIndex = trimmedResponse.LastIndexOf("```");
                if (endIndex > startIndex)
                {
                    return trimmedResponse.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
        }
        
        // No markdown code blocks found, return original response
        return trimmedResponse;
    }
}