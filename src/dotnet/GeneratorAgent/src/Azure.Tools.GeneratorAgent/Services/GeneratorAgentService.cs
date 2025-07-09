using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Azure.Tools.GeneratorAgent.Interfaces;
using Azure.Tools.GeneratorAgent.Configuration;

namespace Azure.Tools.GeneratorAgent.Services
{
    public class GeneratorAgentService : IGeneratorAgentService
    {
        private readonly AppSettings _appSettings;
        private readonly PersistentAgentsClient client;

        private PersistentAgent? agent;

        public GeneratorAgentService(AppSettings appSettings)
        {
            _appSettings = appSettings;
            client = new PersistentAgentsClient(_appSettings.ProjectEndpoint, new DefaultAzureCredential());
            agent = null;
        }
        
       
        public async Task CreateAgentAsync(CancellationToken ct)
        {
            if (agent != null)
            {
                Console.WriteLine($"Agent already exists: {agent.Name} ({agent.Id})");
                return;
            }

            Console.WriteLine("Creating AZC Fixer agent...");
            agent = await client.Administration.CreateAgentAsync(
                model: _appSettings.Model,
                name: _appSettings.AgentName,
                instructions: _appSettings.AgentInstructions,
                tools: new[] { new FileSearchToolDefinition() },
                cancellationToken: ct);

            if (agent == null || string.IsNullOrEmpty(agent.Id))
            {
                throw new InvalidOperationException("Failed to create AZC Fixer agent");
            }

            Console.WriteLine($"✅ Agent created successfully: {agent.Name} ({agent.Id})");
        }

        public async Task DeleteAgentsAsync(CancellationToken ct)
        {
            // Delete all agents
            await foreach (var agent in client.Administration.GetAgentsAsync(cancellationToken: ct))
            {
                await client.Administration.DeleteAgentAsync(agent.Id, ct);
            }
        }
    }
}