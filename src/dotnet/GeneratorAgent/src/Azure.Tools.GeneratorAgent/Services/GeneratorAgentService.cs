using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Azure.Tools.GeneratorAgent.Interfaces;

namespace Azure.Tools.GeneratorAgent.Services
{
    public class GeneratorAgentService : IAsyncDisposable
    {
        private readonly IAppSettings _appSettings;
        private readonly ILoggerService _logger;
        private readonly PersistentAgentsClient _client;
        private PersistentAgent? _agent;

        public GeneratorAgentService(IAppSettings appSettings, ILoggerService logger, PersistentAgentsClient client)
        {
            _appSettings = appSettings;
            _logger = logger;
            _client = client;
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
            // Adding a placeholder await until the actual implementation is added
            await Task.CompletedTask;
        }

        private async Task CreateAgentAsync(CancellationToken ct)
        {
            if (_agent != null)
            {
                _logger.LogInformation($"Agent already exists: {_agent.Name} ({_agent.Id})");
                return;
            }

            _logger.LogInformation("Creating AZC Fixer agent...");
            _agent = await _client.Administration.CreateAgentAsync(
                model: _appSettings.Model,
                name: _appSettings.AgentName,
                instructions: _appSettings.AgentInstructions,
                tools: new[] { new FileSearchToolDefinition() },
                cancellationToken: ct);

            if (_agent == null || string.IsNullOrEmpty(_agent.Id))
            {
                throw new InvalidOperationException("Failed to create AZC Fixer agent");
            }

            _logger.LogInformation($"✅ Agent created successfully: {_agent.Name} ({_agent.Id})");
        }

        private async Task DeleteAgentsAsync(CancellationToken ct)
        {
            // Delete all agents
            await foreach (var agent in _client.Administration.GetAgentsAsync(cancellationToken: ct))
            {
                _logger.LogInformation($"Deleting agent: {agent.Name} ({agent.Id})");
                await _client.Administration.DeleteAgentAsync(agent.Id, ct);
            }
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
                    _logger.LogInformation($"Error deleting agents during disposal: {ex.Message}");
                }
            }
        }
    }
}