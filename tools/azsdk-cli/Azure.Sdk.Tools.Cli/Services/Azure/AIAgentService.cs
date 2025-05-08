using Azure.AI.Projects;

namespace Azure.Sdk.Tools.Cli.Services;

public class TokenUsage
{
    protected double PromptTokens { get; set; }
    protected double CompletionTokens { get; set; }
    protected double InputCost { get; set; }
    protected double OutputCost { get; set; }
    protected double TotalCost { get; set; }
    public List<string> Models { get; set; } = [];

    public TokenUsage(string model, long inputTokens, long outputTokens)
    {
        PromptTokens = inputTokens;
        CompletionTokens = outputTokens;
        Models = [model];
        SetCost(model);
    }

    protected TokenUsage() { }

    private void SetCost(string model)
    {
        var oneMillion = 1000000;
        double inputPrice, outputPrice;

        // Prices assume the slightly more expensive regional model pricing
        if (model == "gpt-4o")
        {
            (inputPrice, outputPrice) = (2.75, 11);
        }
        else if (model == "gpt-4o-mini")
        {
            (inputPrice, outputPrice) = (0.165, 0.66);
        }
        else if (model == "o3-mini")
        {
            (inputPrice, outputPrice) = (1.21, 4.84);
        }
        else
        {
            return;
        }


        InputCost = PromptTokens / oneMillion * inputPrice;
        OutputCost = CompletionTokens / oneMillion * outputPrice;
    }

    public void LogCost()
    {
        var _inputCost = InputCost == 0 ? "?" : InputCost.ToString("F3");
        var _outputCost = OutputCost == 0 ? "?" : OutputCost.ToString("F3");
        var _totalCost = (InputCost + OutputCost) == 0 ? "?" : (InputCost + OutputCost).ToString("F3");
        Console.WriteLine("Usage (cost / tokens):");
        Console.WriteLine($"  Input: ${_inputCost} / {PromptTokens}");
        Console.WriteLine($"  Output: ${_outputCost} / {CompletionTokens}");
        Console.WriteLine($"  Total: ${_totalCost} / {PromptTokens + CompletionTokens}");
    }

    public static TokenUsage operator +(TokenUsage a, TokenUsage b) => new()
    {
        Models = a.Models.Union(b.Models).ToList(),
        PromptTokens = a.PromptTokens + b.PromptTokens,
        CompletionTokens = a.CompletionTokens + b.CompletionTokens,
        InputCost = a.InputCost + b.InputCost,
        OutputCost = a.OutputCost + b.OutputCost,
    };
}

public interface IAIAgentService
{
    AgentsClient GetClient();
    Task<(string, TokenUsage)> QueryFileAsync(Stream contents, string filename, string session, string query);
}

public class AIAgentService(IAzureService azureService) : IAIAgentService
{
    private string vectorStoreName;
    private string vectorStoreId;
    private AgentsClient client;
    private string agentId;
    private string model;
    private readonly IAzureService azureService = azureService;

    private const string LogQueryPrompt = @"
You are an assistant that analyzes Azure Pipelines failure logs. You will be provided with a log file from an Azure Pipelines build. Your task is to analyze the log and provide a summary of the failure. Include relevant data like error type, error messages, functions and error lines. Find other log lines in addition to the final error that may be descriptive of the problem. Errors like 'Powershell exited with code 1' are not error messages, but the error message may be in the logs above it. Provide suggested next steps. Respond only in valid JSON, in the following format:
{
    ""summary"": ""..."",
    ""errors"": [
        { ""file"": ""..."", ""line"": ..., ""message"": ""..."" }
    ],
    ""suggested_fix"": ""...""
}";

    private void Initialize()
    {
        var connectionString = System.Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("AZURE_AI_PROJECT_CONNECTION_STRING environment variable is not set.");
        }
        var _agentId = System.Environment.GetEnvironmentVariable("AZURE_AI_AGENT_ID");
        if (string.IsNullOrEmpty(_agentId))
        {
            throw new InvalidOperationException("AZURE_AI_AGENT_ID environment variable is not set.");
        }
        agentId = _agentId;
        var modelDeploymentName = System.Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME");
        if (string.IsNullOrEmpty(modelDeploymentName))
        {
            throw new InvalidOperationException("MODEL_DEPLOYMENT_NAME environment variable is not set.");
        }
        model = modelDeploymentName;
        // The vector store ID is annoying to find, so support name as an alternative
        var _vectorStoreId = System.Environment.GetEnvironmentVariable("AZURE_AI_VECTOR_STORE_ID");
        var _vectorStoreName = System.Environment.GetEnvironmentVariable("AZURE_AI_VECTOR_STORE_NAME");
        if (string.IsNullOrEmpty(_vectorStoreName) && string.IsNullOrEmpty(_vectorStoreId))
        {
            throw new InvalidOperationException("AZURE_AI_VECTOR_STORE_NAME or AZURE_AI_VECTOR_STORE_ID environment variable is not set.");
        }
        vectorStoreId = _vectorStoreId ?? string.Empty;
        vectorStoreName = _vectorStoreName ?? string.Empty;

        client = new(connectionString, azureService.GetCredential());
    }

    public AgentsClient GetClient()
    {
        if (client == null)
        {
            Initialize();
        }
        return client;
    }

    public async Task<(string, TokenUsage)> QueryFileAsync(Stream contents, string filename, string session, string query)
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

        var tokenUsage = new TokenUsage(model, runResponse.Usage.PromptTokens, runResponse.Usage.CompletionTokens);
        return (string.Join("\n", response), tokenUsage);
    }

    // Old code to upload file with pre-existing vector store
    public async Task UploadFileAsync(Stream contents, string filename)
    {
        if (string.IsNullOrWhiteSpace(filename) || Path.GetExtension(filename) == string.Empty)
        {
            throw new ArgumentException($"Filename '{filename}' must have a file extension (*.txt, *.md, ...)", nameof(filename));
        }

        var client = GetClient();

        var files = await client.GetFilesAsync(purpose: AgentFilePurpose.Agents);
        if (files.Value.Any(f => f.Filename == filename))
        {
            Console.WriteLine($"File '{filename}' already exists. Skipping upload.");
            return;
        }

        if (string.IsNullOrEmpty(vectorStoreId))
        {
            AgentPageableListOfVectorStore vectors = await client.GetVectorStoresAsync();
            var vectorStore = vectors.Data.FirstOrDefault(v => v.Name == vectorStoreName);
            if (vectorStore == null)
            {
                throw new InvalidOperationException($"Vector store with name '{vectorStoreName}' not found.");
            }
            vectorStoreId = vectorStore.Id;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"[INFO] Starting upload of '{filename}' to vector store '{vectorStoreName ?? vectorStoreId}' at {DateTime.UtcNow:O}");
        AgentFile file = await client.UploadFileAsync(contents, AgentFilePurpose.Agents, filename);

        VectorStoreFileBatch batch = await client.CreateVectorStoreFileBatchAsync(
            vectorStoreId: vectorStoreId,
            fileIds: [file.Id]
        );

        while (true)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            batch = await client.GetVectorStoreFileBatchAsync(vectorStoreId, batch.Id);
            if (batch.Status == VectorStoreFileBatchStatus.Completed)
            {
                break;
            }
            else if (batch.Status == VectorStoreFileBatchStatus.Failed)
            {
                throw new Exception($"File processing failed for {filename} uploading to vector store {vectorStoreId}.");
            }
            await Task.Delay(TimeSpan.FromSeconds(0.5));
        }

        stopwatch.Stop();
        Console.WriteLine($"[INFO] Upload and indexing of '{filename}' completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
    }
}
