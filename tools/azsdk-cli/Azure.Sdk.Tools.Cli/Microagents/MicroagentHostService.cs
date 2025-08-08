
using System.ComponentModel;
using Azure.AI.OpenAI;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using OpenAI.Chat;

namespace Azure.Sdk.Tools.Cli.Microagents;

public interface IMicroagentHostService
{
    Task<TResult> RunAgentToCompletionAsync<TResult>(Microagent<TResult> agentDefinition, CancellationToken ct = default);
}

public class MicroagentHostService(AzureOpenAIClient openAI, ILogger<MicroagentHostService> logger) : IMicroagentHostService
{
    private AzureOpenAIClient openAI = openAI;
    private ILogger logger = logger;

    public async Task<TResult> RunAgentToCompletionAsync<TResult>(Microagent<TResult> agentDefinition, CancellationToken ct = default)
    {
        logger.LogInformation("Starting agent with model '{Model}'", agentDefinition.Model);

        var chatClient = openAI.GetChatClient(agentDefinition.Model);
        var tools = agentDefinition.Tools?.ToDictionary(t => t.Name) ?? new Dictionary<string, IAgentTool>();

        // This list keeps track of all chat messages (essentially, just tool requests from the LLM and results from our program).
        // As the agent loop continues, this conversation history gets longer, until either the LLM calls the "exit" tool
        // with a result, indicating success, or we hit the max number of iterations.
        var conversationHistory = new List<ChatMessage>
        {
            new SystemChatMessage(agentDefinition.SystemPrompt)
        };

        var chatCompletionOptions = new ChatCompletionOptions
        {
            // Force the LLM to choose a tool on every turn.
            ToolChoice = ChatToolChoice.CreateRequiredChoice(),
            // For simplicity, only allow for one tool to be called at once.
            AllowParallelToolCalls = false,
        };

        // Add the special-case "exit" tool
        chatCompletionOptions.Tools.Add(ChatTool.CreateFunctionTool("Exit", "Call this tool when you are finished with the work or are otherwise unable to continue.", BinaryData.FromString(ToolHelpers.GetJsonSchemaRepresentation(typeof(ToolCallResult<TResult>)))));
        foreach (var tool in tools.Values)
        {
            // Create our tool definitions using the tool's name and by converting the tool's input schema type to a JSON schema.
            logger.LogDebug("Registering tool '{ToolName}' with description: {Description}", tool.Name, tool.Description);
            chatCompletionOptions.Tools.Add(ChatTool.CreateFunctionTool(tool.Name, tool.Description, BinaryData.FromString(ToolHelpers.GetJsonSchemaRepresentation(tool.InputSchema))));
        }

        for (var i = 0; i < agentDefinition.MaxIterations; i++)
        {
            // Request the chat completion
            logger.LogDebug("Sending conversation history with {MessageCount} messages to model '{Model}'", conversationHistory.Count, agentDefinition.Model);
            var response = await chatClient.CompleteChatAsync(conversationHistory, chatCompletionOptions, ct);

            var toolCall = response.Value.ToolCalls.Single();
            logger.LogInformation("Model called tool '{ToolName}'", toolCall.FunctionName);

            // Add the agent's response to the conversation history
            conversationHistory.Add(new AssistantChatMessage(response.Value));

            logger.LogDebug("Invoking tool '{ToolName}' with arguments: {Arguments}", toolCall.FunctionName, toolCall.FunctionArguments.ToString());

            // Dispatch the tool call...
            if (toolCall.FunctionName == "Exit")
            {
                // exit tool was called with the result, return it.
                logger.LogInformation("Agent is exiting with result.");
                return toolCall.FunctionArguments.ToObjectFromJson<ToolCallResult<TResult>>()!.Result ?? throw new InvalidOperationException("Exit tool did not return a valid result.");
            }

            var tool = tools[toolCall.FunctionName];

            // TODO: error handling. What if the tool is not found? What if the arguments are invalid?

            string toolResult;
            try
            {
                toolResult = await tool.InvokeAsync(toolCall.FunctionArguments.ToString(), ct);
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

        throw new Exception("Agent did not return a result within the maximum number of iterations");
    }

    /// <summary>
    /// Wrapper object for all tool call results so that we are able to make calls that e.g. just return a string
    /// (OpenAI expects an object at the top level)
    /// </summary>
    /// <typeparam name="T">Type of the result</typeparam>
    private class ToolCallResult<T>
    {
        [Description("The result of the agent run. Output the result requested exactly, without additional padding, explanation, or code fences unless requested.")]
        public T Result { get; set; }
    }
}
