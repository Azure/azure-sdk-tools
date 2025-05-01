using Azure.AI.Projects;
using OpenAI.VectorStores;

namespace Azure.SDK.Tools.MCP.Hub.Services.Azure;

public interface IAIAgentService
{
    AgentsClient GetClient();
    Task<VectorStoreFileBatch> UploadFileAsync(Stream contents, string filename);
}

public class AIAgentService : IAIAgentService
{
    private readonly string vectorStoreName;
    private string vectorStoreId;
    private readonly AgentsClient client;

    public AIAgentService(IAzureService azureService)
    {
        var connectionString = System.Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("AZURE_AI_PROJECT_CONNECTION_STRING environment variable is not set.");
        }
        var modelDeploymentName = System.Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME");
        if (string.IsNullOrEmpty(modelDeploymentName))
        {
            throw new InvalidOperationException("MODEL_DEPLOYMENT_NAME environment variable is not set.");
        }
        // The vector store ID is annoying to find, so support name as an alternative
        var _vectorStoreId = System.Environment.GetEnvironmentVariable("AZURE_AI_VECTOR_STORE_ID");
        var _vectorStoreName = System.Environment.GetEnvironmentVariable("AZURE_AI_VECTOR_STORE_NAME");
        if (string.IsNullOrEmpty(_vectorStoreName) && string.IsNullOrEmpty(_vectorStoreId))
        {
            throw new InvalidOperationException("AZURE_AI_VECTOR_STORE_NAME or AZURE_AI_VECTOR_STORE_ID environment variable is not set.");
        }
        this.vectorStoreId = _vectorStoreId ?? string.Empty;
        this.vectorStoreName = _vectorStoreName ?? string.Empty;

        this.client = new(connectionString, azureService.GetCredential());
    }

    public AgentsClient GetClient()
    {
        return this.client;
    }

    public async Task<VectorStoreFileBatch> UploadFileAsync(Stream contents, string filename)
    {
        if (string.IsNullOrWhiteSpace(filename) || Path.GetExtension(filename) == string.Empty)
        {
            throw new ArgumentException($"Filename '{filename}' must have a file extension (*.txt, *.md, ...)", nameof(filename));
        }

        var client = GetClient();

        if (string.IsNullOrEmpty(this.vectorStoreId))
        {
            AgentPageableListOfVectorStore vectors = await client.GetVectorStoresAsync();
            var vectorStore = vectors.Data.FirstOrDefault(v => v.Name == this.vectorStoreName);
            if (vectorStore == null)
            {
                throw new InvalidOperationException($"Vector store with name '{this.vectorStoreName}' not found.");
            }
            this.vectorStoreId = vectorStore.Id;
        }

        AgentFile file = await client.UploadFileAsync(contents, AgentFilePurpose.Agents, filename);

        return await client.CreateVectorStoreFileBatchAsync(
            vectorStoreId: this.vectorStoreId,
            fileIds: [file.Id]
        );
    }
}