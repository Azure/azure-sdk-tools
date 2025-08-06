using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    internal class ErrorFixerAgent : IAsyncDisposable
    {
        private readonly AppSettings AppSettings;
        private readonly ILogger<ErrorFixerAgent> Logger;
        private readonly PersistentAgentsAdministrationClient AdminClient;
        private readonly Lazy<PersistentAgent> Agent;
        private bool Disposed = false;

        public ErrorFixerAgent(
            AppSettings appSettings,
            ILogger<ErrorFixerAgent> logger,
            PersistentAgentsAdministrationClient adminClient)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(adminClient);
            
            AppSettings = appSettings;
            Logger = logger;
            AdminClient = adminClient;
            Agent = new Lazy<PersistentAgent>(() => CreateAgent());
        }

        public async Task FixCodeAsync(CancellationToken ct)
        {
            PersistentAgent agent = Agent.Value;

            // TODO: Implement the code fixing logic here
            await Task.CompletedTask.ConfigureAwait(false);
        }

        internal virtual PersistentAgent CreateAgent()
        {
            Logger.LogInformation("Creating AZC Fixer agent...");
            
            Response<PersistentAgent> response = AdminClient.CreateAgent(
                model: AppSettings.Model,
                name: AppSettings.AgentName,
                instructions: AppSettings.AgentInstructions,
                tools: new[] { new FileSearchToolDefinition() });

            PersistentAgent agent = response.Value;
            if (string.IsNullOrEmpty(agent?.Id))
            {
                throw new InvalidOperationException("Failed to create AZC Fixer agent");
            }

            Logger.LogInformation("✅ Agent created successfully: {Name} ({Id})", agent.Name, agent.Id);
            return agent;
        }

        private async Task DeleteAgentsAsync(CancellationToken ct)
        {
            List<Task> deleteTasks = new List<Task>();

            await foreach (PersistentAgent agent in AdminClient.GetAgentsAsync(cancellationToken: ct))
            {
                Task deleteTask = AdminClient.DeleteAgentAsync(agent.Id, ct)
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
                    }, ct);

                deleteTasks.Add(deleteTask);
            }

            await Task.WhenAll(deleteTasks).ConfigureAwait(false);
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