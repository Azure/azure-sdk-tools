using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    internal class AgentManager : IAsyncDisposable
    {
        private readonly PersistentAgentsClient Client;
        private readonly ILogger<AgentManager> Logger;
        private readonly Lazy<PersistentAgent> Agent;
        private volatile bool Disposed = false;

        public AgentManager(PersistentAgentsClient client, ILogger<AgentManager> logger, AppSettings appSettings)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(appSettings);
            
            Client = client;
            Logger = logger;
            Agent = new Lazy<PersistentAgent>(() => CreateAgent(appSettings));
        }

        private PersistentAgent CreateAgent(AppSettings appSettings)
        {
            Logger.LogInformation("Creating Generator Agent...");

            var response = Client.Administration.CreateAgent(
                model: appSettings.Model,
                name: appSettings.AgentName,
                instructions: appSettings.AgentInstructions,
                tools: new[] { new FileSearchToolDefinition() });

            var agent = response.Value;
            if (string.IsNullOrEmpty(agent?.Id))
            {
                throw new InvalidOperationException("Failed to create Generator Agent");
            }

            Logger.LogInformation("Agent created successfully: {Name} ({Id})", agent.Name, agent.Id);
            return agent;
        }

        public PersistentAgent GetAgent()
        {
            return Agent.Value;
        }

        public virtual async Task DeleteAgentsAsync(CancellationToken cancellationToken)
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

        public virtual async Task UpdateAgentVectorStoreAsync(string agentId, string vectorStoreId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(agentId);
            ArgumentNullException.ThrowIfNull(vectorStoreId);
            
            await Client.Administration.UpdateAgentAsync(
                agentId,
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
