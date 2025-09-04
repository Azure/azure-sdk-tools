using Microsoft.Extensions.Logging;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.AI.Agents.Persistent;

namespace Azure.Tools.GeneratorAgent.Agent
{
    internal class ErrorFixerAgent : IAsyncDisposable
    {
        private readonly AgentConversationProcessor ConversationProcessor;
        private readonly AgentFileManager FileManager;
        private readonly PersistentAgentsClient Client;
        private readonly AppSettings AppSettings;
        private readonly ILogger<ErrorFixerAgent> Logger;
        private readonly FixPromptService FixPromptService;
        private readonly Lazy<PersistentAgent> Agent;
        private volatile bool Disposed = false;
        
        private string AgentId => Agent.Value.Id;

        public ErrorFixerAgent(
            AgentConversationProcessor conversationProcessor,
            AgentFileManager fileManager,
            PersistentAgentsClient client,
            AppSettings appSettings,
            ILogger<ErrorFixerAgent> logger,
            FixPromptService fixPromptService)
        {
            ArgumentNullException.ThrowIfNull(conversationProcessor);
            ArgumentNullException.ThrowIfNull(fileManager);
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(fixPromptService);

            ConversationProcessor = conversationProcessor;
            FileManager = fileManager;
            Client = client;
            AppSettings = appSettings;
            Logger = logger;
            FixPromptService = fixPromptService;
            Agent = new Lazy<PersistentAgent>(() => CreateAgent());
        }

        private PersistentAgent CreateAgent()
        {
            Logger.LogInformation("Creating Generator Agent...");

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

            Logger.LogInformation("Agent created successfully: {Name} ({Id})", agent.Name, agent.Id);
            return agent;
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

            Logger.LogInformation("Agent vector store updated to: {VectorStoreId}", vectorStoreId);
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

        public virtual async Task InitializeAgentEnvironmentAsync(Dictionary<string, string> typeSpecFiles, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(typeSpecFiles);
            
            Logger.LogInformation("Initializing agent environment...");

            // Step 1: Upload files
            var (uploadedFileIds, vectorStoreId) = await FileManager.UploadFilesAsync(typeSpecFiles, cancellationToken);

            // Step 2: Update Agent Vector Store (this will trigger agent creation if needed)
            await UpdateAgentVectorStoreAsync(vectorStoreId, cancellationToken);

            Logger.LogInformation("Agent environment initialized successfully with agent {AgentId}.", AgentId);
        }

        public virtual async Task<string> FixCodeAsync(List<Fix> fixes, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fixes);
            
            try
            {
                // Generate a new thread ID for FixCodeAsync
                var fixThreadId = await ConversationProcessor.CreateThreadAsync(cancellationToken);

                // Call ConversationProcessor to fix the code
                var result = await ConversationProcessor.FixCodeAsync(fixes, fixThreadId, AgentId, FixPromptService, cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Code fix failed for agent {AgentId}", AgentId);
                throw;
            }
        }

        public virtual async Task<IEnumerable<RuleError>> AnalyzeErrorsAsync(string errorLogs, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(errorLogs);
            
            try
            {
                // Generate a new thread ID for AnalyzeErrorsAsync
                var analyzeThreadId = await ConversationProcessor.CreateThreadAsync(cancellationToken);

                // Call ConversationProcessor to analyze errors
                var errors = await ConversationProcessor.AnalyzeErrorsAsync(errorLogs, analyzeThreadId, AgentId, cancellationToken);

                return errors;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error analysis failed for agent {AgentId}", AgentId);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Disposed)
            {
                return;
            }

            Disposed = true;

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
    }
}
