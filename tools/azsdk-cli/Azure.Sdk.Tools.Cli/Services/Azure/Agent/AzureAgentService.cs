using Azure.AI.Projects;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IAzureAgentService
{
    AgentsClient GetClient();
    Task<(string, TokenUsageHelper)> QueryFileAsync(Stream contents, string filename, string session, string query);
}

public class AzureAgentService(IAzureService azureService, string? endpoint, string? model) : IAzureAgentService
{
    private readonly string endpoint = endpoint ?? "eastus2.api.azureml.ms;faa080af-c1d8-40ad-9cce-e1a450ca5b57;prmarott-apiview;prmarott-apiview";
    private readonly string model = model ?? "gpt-4o-mini";

    private AgentsClient client;

    private const string LogQueryPrompt = @"You are an assistant that analyzes Azure Pipelines failure logs.
You will be provided with a log file from an Azure Pipelines build.
Your task is to analyze the log and provide a summary of the failure.
Include relevant data like error type, error messages, functions and error lines.
Find other log lines in addition to the final error that may be descriptive of the problem.
Errors like 'Powershell exited with code 1' are not error messages, but the error message may be in the logs above it.
Provide suggested next steps. Respond only in valid JSON, in the following format:
{
    ""summary"": ""..."",
    ""errors"": [
        { ""file"": ""..."", ""line"": ..., ""message"": ""..."" }
    ],
    ""suggested_fix"": ""...""
}";

    private void Initialize()
    {
        client = new(endpoint, azureService.GetCredential());
    }

    public AgentsClient GetClient()
    {
        if (client == null)
        {
            Initialize();
        }
        return client;
    }

    public async Task<(string, TokenUsageHelper)> QueryFileAsync(Stream contents, string filename, string session, string query)
    {
        if (string.IsNullOrWhiteSpace(filename) || Path.GetExtension(filename) == string.Empty)
        {
            throw new ArgumentException($"Filename '{filename}' must have a file extension (*.txt, *.md, ...)", nameof(filename));
        }

        var client = GetClient();
        AgentFile uploaded = await client.UploadFileAsync(contents, AgentFilePurpose.Agents, filename);
        VectorStore vectorStore = await client.CreateVectorStoreAsync(fileIds: [uploaded.Id], name: filename);
        FileSearchToolResource tool = new();
        tool.VectorStoreIds.Add(vectorStore.Id);

        Agent agent = await client.CreateAgentAsync(
            model: model,
            name: session,
            instructions: LogQueryPrompt,
            tools: [new FileSearchToolDefinition()],
            toolResources: new ToolResources() { FileSearch = tool }
        );

        AgentThread thread = await client.CreateThreadAsync();
        ThreadMessage message = await client.CreateMessageAsync(thread.Id, MessageRole.User, query);
        ThreadRun runResponse = await client.CreateRunAsync(thread, agent);

        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            runResponse = await client.GetRunAsync(thread.Id, runResponse.Id);
        }
        while (runResponse.Status == RunStatus.Queued || runResponse.Status == RunStatus.InProgress);

        PageableList<ThreadMessage> afterRunMessagesResponse = await client.GetMessagesAsync(thread.Id);
        var messages = afterRunMessagesResponse.Data;
        var response = new List<string>();

        // Note: messages iterate from newest to oldest, with the messages[0] being the most recent
        foreach (ThreadMessage threadMessage in messages)
        {
            foreach (MessageContent contentItem in threadMessage.ContentItems)
            {
                if (contentItem is MessageTextContent textItem)
                {
                    if (textItem.Text != query)
                    {
                        response.Add(textItem.Text);
                    }
                }
                else
                {
                    throw new NotImplementedException($"Content type of {contentItem.GetType()} is not supported yet.");
                }
            }
        }

        var tokenUsage = new TokenUsageHelper(model, runResponse.Usage.PromptTokens, runResponse.Usage.CompletionTokens);
        return (string.Join("\n", response), tokenUsage);
    }
}
