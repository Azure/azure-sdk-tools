using Azure.AI.Agents.Persistent;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IAzureAgentService
{
    string ProjectEndpoint { get; }

    Task DeleteAgents(CancellationToken ct);
    Task<string> QueryFiles(List<string> files, string session, string query, CancellationToken ct);
}

public class AzureAgentService(
    IAzureService azureService,
    ILogger<AzureAgentService> logger,
    TokenUsageHelper tokenUsageHelper,
    string? _projectEndpoint,
    string? _model
) : IAzureAgentService
{
    public string ProjectEndpoint { get; } = _projectEndpoint ?? defaultProjectEndpoint;
    private static readonly string defaultProjectEndpoint = "https://azsdk-engsys-openai.services.ai.azure.com/api/projects/azsdk-engsys-openai-foundry";
    private readonly string model = _model ?? "gpt-4o";

    private readonly PersistentAgentsClient client = new(_projectEndpoint ?? defaultProjectEndpoint, azureService.GetCredential());

    private const string LogQueryPrompt = @"You are an assistant that analyzes Azure Pipelines failure logs.
You will be provided with log files from an Azure Pipelines build.
Your task is to analyze the logs and provide a summary of the failures.
Include relevant data like error type, error messages, functions and error lines.
Find other log lines in addition to the final error that may be descriptive of the problem.
Errors like 'Powershell exited with code 1' are not error messages, but the error message may be in the logs above it.
The full response should be parsed as valid json, do not prefix with another message.
Provide suggested next steps. Respond only in valid JSON with a single object in the following format:
{
    ""summary"": ""..."",
    ""errors"": [
        { ""file"": ""..."", ""line"": ..., ""message"": ""..."" }
    ],
    ""suggested_fixes"": ""...""
}
Again, the entire response **MUST BE VALID JSON**";

    public async Task DeleteAgents(CancellationToken ct = default)
    {
        logger.LogDebug("Deleting agents in project '{ProjectEndpoint}'", ProjectEndpoint);
        AsyncPageable<PersistentAgent> agents = client.Administration.GetAgentsAsync(cancellationToken: ct);
        await foreach (var agent in agents)
        {
            logger.LogDebug("Deleting agent {AgentId} ({AgentName})", agent.Id, agent.Name);
            await client.Administration.DeleteAgentAsync(agent.Id, ct);
        }

        AsyncPageable<PersistentAgentThread> threads = client.Threads.GetThreadsAsync(cancellationToken: ct);
        await foreach (var thread in threads)
        {
            logger.LogDebug("Deleting thread {ThreadId}", thread.Id);
            await client.Threads.DeleteThreadAsync(thread.Id, ct);
        }

        AsyncPageable<PersistentAgentsVectorStore> vectorStores = client.VectorStores.GetVectorStoresAsync(cancellationToken: ct);
        await foreach (var vectorStore in vectorStores)
        {
            logger.LogDebug("Deleting vector store {VectorStoreId} ({VectorStoreName})", vectorStore.Id, vectorStore.Name);
            await client.VectorStores.DeleteVectorStoreAsync(vectorStore.Id, ct);
        }

        var files = await client.Files.GetFilesAsync(cancellationToken: ct);
        foreach (var file in files.Value)
        {
            logger.LogDebug("Deleting file {FileId} ({FileName})", file.Id, file.Filename);
            await client.Files.DeleteFileAsync(file.Id, ct);
        }
    }

    public async Task DeleteAgentResources(
        PersistentAgent agent,
        PersistentAgentsVectorStore vectorStore,
        PersistentAgentThread thread,
        List<PersistentAgentFileInfo> sessionFiles,
        CancellationToken ct = default)
    {
        logger.LogDebug("Deleting agent {AgentId} ({AgentName})", agent.Id, agent.Name);
        await client.Administration.DeleteAgentAsync(agent.Id, ct);

        logger.LogDebug("Deleting thread {ThreadId}", thread.Id);
        await client.Threads.DeleteThreadAsync(thread.Id, ct);

        logger.LogDebug("Deleting vector store {VectorStoreId} ({VectorStoreName})", vectorStore.Id, vectorStore.Name);
        await client.VectorStores.DeleteVectorStoreAsync(vectorStore.Id, ct);

        foreach (var file in sessionFiles)
        {
            logger.LogDebug("Deleting file {FileId} ({FileName})", file.Id, file.Filename);
            await client.Files.DeleteFileAsync(file.Id, ct);
        }
    }

    public async Task<string> QueryFiles(List<string> files, string session, string query, CancellationToken ct)
    {
        List<string> uploaded = [];
        List<PersistentAgentFileInfo> sessionFiles = [];
        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file) || Path.GetExtension(file) == string.Empty)
            {
                throw new Exception($"Filename '{file}' must have a file extension (*.txt, *.md, ...)");
            }
            logger.LogDebug("Uploading file {FileName}", file);
            PersistentAgentFileInfo info = await client.Files.UploadFileAsync(file, PersistentAgentFilePurpose.Agents, ct);
            uploaded.Add(info.Id);
            sessionFiles.Add(info);
        }

        PersistentAgentsVectorStore vectorStore = await client.VectorStores.CreateVectorStoreAsync(uploaded, name: session, cancellationToken: ct);
        FileSearchToolResource tool = new();
        tool.VectorStoreIds.Add(vectorStore.Id);

        PersistentAgent agent = await client.Administration.CreateAgentAsync(
            model: model,
            name: session,
            instructions: LogQueryPrompt,
            tools: [new FileSearchToolDefinition()],
            toolResources: new ToolResources() { FileSearch = tool });

        PersistentAgentThread thread = await client.Threads.CreateThreadAsync(cancellationToken: ct);
        await client.Messages.CreateMessageAsync(thread.Id, MessageRole.User, query, cancellationToken: ct);
        ThreadRun run = await client.Runs.CreateRunAsync(thread, agent, ct);

        do
        {
            await Task.Delay(500, ct);
            run = await client.Runs.GetRunAsync(thread.Id, run.Id, cancellationToken: ct);
        }
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

        if (run.Status != RunStatus.Completed)
        {
            throw new Exception("Run did not complete successfully, error: " + run.LastError?.Message);
        }

        AsyncPageable<PersistentThreadMessage> messages = client.Messages.GetMessagesAsync(
            threadId: thread.Id,
            order: ListSortOrder.Ascending,
            cancellationToken: ct
        );

        var response = new List<string>();

        await foreach (var threadMessage in messages)
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

        var tokenSession = tokenUsageHelper.NewSession($"azure-agent:{model}");
        tokenSession.Add(model, run.Usage.PromptTokens, run.Usage.CompletionTokens);

        // NOTE: in the future we will want to keep these around if the user wants to keep querying the file in a session
        logger.LogInformation("Deleting temporary resources: agent {AgentId}, vector store {VectorStoreId}, {fileCount} files", agent.Id, vectorStore.Id, uploaded.Count);
        await DeleteAgentResources(agent, vectorStore, thread, sessionFiles, ct);

        return string.Join("\n", response);
    }
}
