using Microsoft.Extensions.Logging;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.AI.Agents.Persistent;

namespace Azure.Tools.GeneratorAgent.Agent
{
    internal class ErrorFixerAgent : IAsyncDisposable
    {
        private readonly AgentFileManager FileManager;
        private readonly PersistentAgentsClient Client;
        private readonly AppSettings AppSettings;
        private readonly ILogger<ErrorFixerAgent> Logger;
        private readonly ILoggerFactory LoggerFactory;
        private readonly FixPromptService FixPromptService;
        private readonly Lazy<PersistentAgent> Agent;
        private volatile bool _disposed = false;

        private AgentConversationProcessor? ConversationProcessor;

        private string AgentId => Agent.Value.Id;

        private string? CurrentVectorStoreId; 

        public ErrorFixerAgent(
            AgentFileManager fileManager,
            PersistentAgentsClient client,
            AppSettings appSettings,
            ILogger<ErrorFixerAgent> logger,
            ILoggerFactory loggerFactory,
            FixPromptService fixPromptService)
        {
            ArgumentNullException.ThrowIfNull(fileManager);
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ArgumentNullException.ThrowIfNull(fixPromptService);

            FileManager = fileManager;
            Client = client;
            AppSettings = appSettings;
            Logger = logger;
            LoggerFactory = loggerFactory;
            FixPromptService = fixPromptService;
            Agent = new Lazy<PersistentAgent>(() => CreateAgent());
        }

        private PersistentAgent CreateAgent()
        {
            Logger.LogDebug("Creating Generator Agent...");

            var response = Client.Administration.CreateAgent(
                model: AppSettings.Model,
                name: AppSettings.AgentName,
                instructions: AppSettings.AgentInstructions,
                tools: new[] { new FileSearchToolDefinition() });

            var agent = response.Value;
            if (string.IsNullOrEmpty(agent?.Id))
            {
                throw new InvalidOperationException("Failed to create Generator Agent");
            }

            Logger.LogDebug("Agent created successfully: {Name} ({Id})", agent.Name, agent.Id);
            return agent;
        }

        public virtual async Task InitializeAgentEnvironmentAsync(Dictionary<string, string> typeSpecFiles, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(typeSpecFiles);

            Logger.LogDebug("Initializing agent environment with {FileCount} TypeSpec files", typeSpecFiles.Count);

            // Step 1: Upload files
            CurrentVectorStoreId = await FileManager.UploadFilesAsync(typeSpecFiles, cancellationToken).ConfigureAwait(false);

            // Step 2: Update Agent Vector Store (this will trigger agent creation if needed)
            await UpdateAgentVectorStoreAsync(CurrentVectorStoreId, cancellationToken).ConfigureAwait(false);

            // Step 3: Create conversation processor once for the lifetime of this agent
            ConversationProcessor = await AgentConversationProcessor.CreateAsync(
                Client,
                LoggerFactory,
                AppSettings,
                AgentId,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a single file in the vector store. Call this when client.tsp is updated.
        /// </summary>
        public virtual async Task UpdateFileAsync(string fileName, string content, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            ArgumentNullException.ThrowIfNull(content);

            if (string.IsNullOrEmpty(CurrentVectorStoreId))
            {
                throw new InvalidOperationException("Agent environment must be initialized before updating files. Call InitializeAgentEnvironmentAsync first.");
            }

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Updating file {FileName} in vector store {VectorStoreId}", fileName, CurrentVectorStoreId);
            }

            // Update the single file in the existing vector store
            await FileManager.UpdateFileInVectorStoreAsync(CurrentVectorStoreId, fileName, content, cancellationToken).ConfigureAwait(false);

        }

        public virtual async Task<Result<string>> FixCodeAsync(List<Fix> fixes, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fixes);

            if (ConversationProcessor == null)
            {
                return Result<string>.Failure(new InvalidOperationException("Agent environment must be initialized before calling FixCodeAsync. Call InitializeAgentEnvironmentAsync first."));
            }

            var result = await ConversationProcessor.FixCodeAsync(fixes, FixPromptService, cancellationToken).ConfigureAwait(false);
            
            return result;
        }

        public virtual async Task<Result<IEnumerable<RuleError>>> AnalyzeErrorsAsync(string errorLogs, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(errorLogs);

            if (ConversationProcessor == null)
            {
                return Result<IEnumerable<RuleError>>.Failure(new InvalidOperationException("Agent environment must be initialized before calling AnalyzeErrorsAsync. Call InitializeAgentEnvironmentAsync first."));
            }

            var errors = await ConversationProcessor.AnalyzeErrorsAsync(errorLogs, cancellationToken).ConfigureAwait(false);

            return errors;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (Agent.IsValueCreated)
            {
                try
                {
                    await DeleteAgentsAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error deleting agents during disposal");
                }
            }
        }
 
        private async Task UpdateAgentVectorStoreAsync(string vectorStoreId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(vectorStoreId);
            
            await Client.Administration.UpdateAgentAsync(
                AgentId,
                toolResources: new ToolResources
                {
                    FileSearch = new FileSearchToolResource
                    {
                        VectorStoreIds = { vectorStoreId }
                    }
                },
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            Logger.LogDebug("Agent vector store updated to: {VectorStoreId}", vectorStoreId);
        }

        private async Task DeleteAgentsAsync(CancellationToken cancellationToken)
        {
            var deleteTasks = new List<Task>();

            await foreach (PersistentAgent agent in Client.Administration.GetAgentsAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                var deleteTask = Client.Administration.DeleteAgentAsync(agent.Id, cancellationToken)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Logger.LogError(t.Exception, "Failed to delete agent {Name} ({Id})", agent.Name, agent.Id);
                        }
                        else
                        {
                            Logger.LogInformation("Deleted agent: {Name} ({Id})", agent.Name, agent.Id);
                        }
                    }, cancellationToken);

                deleteTasks.Add(deleteTask);
            }

            await Task.WhenAll(deleteTasks).ConfigureAwait(false);
        }
    }
}
