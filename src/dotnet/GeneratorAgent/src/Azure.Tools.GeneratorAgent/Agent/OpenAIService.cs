using OpenAI;
using OpenAI.Chat;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Constants;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Azure.Tools.GeneratorAgent;

/// <summary>
/// OpenAI service implementation that provides AI-powered code fixing capabilities
/// using OpenAI's API (either direct or Azure OpenAI)
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
            result = response.Value.Content[0].Text;
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
        
        var finalResponse = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        var finalResult = finalResponse.Value.Content[0].Text;
        
        return finalResult;
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