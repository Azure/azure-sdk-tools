using System.ComponentModel;
using Azure.AI.OpenAI;
using Azure.Sdk.Tools.Cli.Helpers;
using OpenAI.Chat;

namespace Azure.Sdk.Tools.Cli.Microagents;

public class MicroagentHostService(AzureOpenAIClient openAI, ILogger<MicroagentHostService> logger, TokenUsageHelper tokenUsageHelper) : IMicroagentHostService
{
    private const string ExitToolName = "Exit";

    private AzureOpenAIClient openAI = openAI;
    private ILogger logger = logger;

    public async Task<TResult> RunAgentToCompletion<TResult>(Microagent<TResult> agentDefinition, CancellationToken ct = default) where TResult : notnull
    {
        var tools = agentDefinition.Tools?.ToDictionary(t => t.Name) ?? new Dictionary<string, IAgentTool>();
        if (tools.ContainsKey(ExitToolName))
        {
            throw new ArgumentException($"Cannot name a tool with the special name '{ExitToolName}'. Please choose a different name.", nameof(agentDefinition.Tools));
        }

        logger.LogInformation("Starting agent with model '{Model}'", agentDefinition.Model);
        var chatClient = openAI.GetChatClient(agentDefinition.Model);

        // This list keeps track of all chat messages (essentially, just tool requests from the LLM and results from our program).
        // As the agent loop continues, this conversation history gets longer, until either the LLM calls the "exit" tool
        // with a result, indicating success, or we hit the max number of iterations.
        var conversationHistory = new List<ChatMessage>
        {
            new SystemChatMessage(agentDefinition.Instructions)
        };

        var chatCompletionOptions = new ChatCompletionOptions
        {
            // Force the LLM to choose a tool on every turn.
            ToolChoice = ChatToolChoice.CreateRequiredChoice(),
            // For simplicity, only allow for one tool to be called at once.
            AllowParallelToolCalls = false,
        };

        // Add the special-case "exit" tool
        // We define a tool for this as it allows us to provide a JSON schema for the expected output. This is more
        // reliable than parsing the end-of-conversation message from the agent.
        chatCompletionOptions.Tools.Add(
            ChatTool.CreateFunctionTool(ExitToolName,
                "Call this tool when you are finished with the work or are otherwise unable to continue.",
                BinaryData.FromString(ToolHelpers.GetJsonSchemaRepresentation(typeof(MicroagentResult<TResult>)))
            )
        );

        foreach (var tool in tools.Values)
        {
            logger.LogDebug("Registering tool '{ToolName}' with description: {Description}", tool.Name, tool.Description);
            var chatTool = ChatTool.CreateFunctionTool(tool.Name, tool.Description, BinaryData.FromString(tool.InputSchema));
            chatCompletionOptions.Tools.Add(chatTool);
        }

        for (var i = 0; i < agentDefinition.MaxToolCalls; i++)
        {
            // Request the chat completion
            logger.LogDebug("Sending conversation history with {MessageCount} messages to model '{Model}'", conversationHistory.Count, agentDefinition.Model);
            var response = await chatClient.CompleteChatAsync(conversationHistory, chatCompletionOptions, ct);
            if (null != response.Value.Usage)
            {
                tokenUsageHelper.Add(agentDefinition.Model, response.Value.Usage.InputTokenCount, response.Value.Usage.OutputTokenCount);
            }

            var toolCall = response.Value.ToolCalls.Single();
            logger.LogInformation("Model called tool '{ToolName}'", toolCall.FunctionName);

            // Add the agent's response to the conversation history
            conversationHistory.Add(new AssistantChatMessage(response.Value));

            logger.LogDebug("Invoking tool '{ToolName}' with arguments: {Arguments}", toolCall.FunctionName, toolCall.FunctionArguments.ToString());

            // Dispatch the tool call...
            if (toolCall.FunctionName == ExitToolName)
            {
                // exit tool was called with the result, return it.
                logger.LogInformation("Agent is exiting with result.");
                var result = toolCall.FunctionArguments.ToObjectFromJson<MicroagentResult<TResult>>();
                if (result == null)
                {
                    throw new InvalidOperationException($"Exit tool did not return a valid result: {toolCall.FunctionArguments}");
                }

                if (agentDefinition.ValidateResult != null)
                {
                    var validation = await agentDefinition.ValidateResult(result.Result);
                    if (!validation.Success)
                    {
                        var serializedReason = validation.Reason is string str ? str : System.Text.Json.JsonSerializer.Serialize(validation.Reason);
                        logger.LogWarning("Agent returned a result that did not pass validation: {Reason}. Continuing the run.", serializedReason);
                        conversationHistory.Add(ChatMessage.CreateToolMessage(toolCall.Id, $"The result you provided did not pass validation: {serializedReason}. Please try again."));
                        continue;
                    }
                }

                return result.Result;
            }

            if (!tools.TryGetValue(toolCall.FunctionName, out var tool))
            {
                throw new InvalidOperationException($"Agent attempted to call nonexistent tool: {toolCall.FunctionName}");
            }

            string toolResult;
            try
            {
                toolResult = await tool.Invoke(toolCall.FunctionArguments.ToString(), ct);
            }
            catch (Exception e)
            {
                // TODO: is this the best way to communicate an error to the LLM?
                toolResult = $"Error: {e.Message}";
            }
            logger.LogInformation("Tool {ToolName} returned: {Result}", tool.Name, toolResult);

            // Add the result of calling the tool function to the conversation history.
            conversationHistory.Add(ChatMessage.CreateToolMessage(toolCall.Id, toolResult));
        }

        throw new Exception($"Agent did not return a result within the maximum number of {agentDefinition.MaxToolCalls} iterations");
    }

    /// <summary>
    /// Wrapper object for all tool call results so that we are able to make calls that e.g. just return a string
    /// (OpenAI expects an object at the top level)
    /// </summary>
    /// <typeparam name="T">Type of the result</typeparam>
    private class MicroagentResult<T> where T : notnull
    {
        [Description("The result of the agent run. Output the result requested exactly, without additional padding, explanation, or code fences unless requested.")]
        public required T Result { get; set; }
    }
}
