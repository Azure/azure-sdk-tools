using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    public class ErrorFixerAgent : IAsyncDisposable
    {
        private readonly AppSettings _appSettings;
        private readonly ILogger<ErrorFixerAgent> _logger;
        private readonly PersistentAgentsAdministrationClient _adminClient;
        private PersistentAgent? _agent;

        public ErrorFixerAgent(
            AppSettings appSettings,
            ILogger<ErrorFixerAgent> logger,
            PersistentAgentsAdministrationClient adminClient)
        {
            _appSettings = appSettings;
            _logger = logger;
            _adminClient = adminClient;
        }

        public async Task InitializeAsync(CancellationToken ct)
        {
            await CreateAgentAsync(ct);
        }

        public async Task FixCodeAsync(CancellationToken ct)
        {
            if (_agent == null)
            {
                throw new InvalidOperationException("Agent not initialized. Call InitializeAsync first.");
            }

            // TODO: Implement the code fixing logic here
            await Task.CompletedTask;
        }

        private async Task CreateAgentAsync(CancellationToken ct)
        {
            if (_agent != null)
            {
                _logger.LogInformation("Agent already exists: {Name} ({Id})", _agent.Name, _agent.Id);
                return;
            }

            _logger.LogInformation("Creating AZC Fixer agent...");
            _agent = await _adminClient.CreateAgentAsync(
                model: _appSettings.Model,
                name: _appSettings.AgentName,
                instructions: _appSettings.AgentInstructions,
                tools: new[] { new FileSearchToolDefinition() },
                cancellationToken: ct);

            if (_agent == null || string.IsNullOrEmpty(_agent.Id))
            {
                throw new InvalidOperationException("Failed to create AZC Fixer agent");
            }

            _logger.LogInformation("✅ Agent created successfully: {Name} ({Id})", _agent.Name, _agent.Id);
        }

        private async Task DeleteAgentsAsync(CancellationToken ct)
        {
            var agents = new List<PersistentAgent>();

            await foreach (PersistentAgent agent in _adminClient.GetAgentsAsync(cancellationToken: ct))
            {
                agents.Add(agent);
            }

            var deleteTasks = agents.Select(async agent =>
            {
                try
                {
                    _logger.LogInformation("Deleting agent: {Name} ({Id})", agent.Name, agent.Id);
                    await _adminClient.DeleteAgentAsync(agent.Id, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete agent {Name} ({Id})", agent.Name, agent.Id);
                }
            });

            await Task.WhenAll(deleteTasks);
        }

        public async ValueTask DisposeAsync()
        {
            if (_agent != null)
            {
                try
                {
                    await DeleteAgentsAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting agents during disposal");
                }
            }
        }
    }
}