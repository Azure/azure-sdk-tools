using Microsoft.Extensions.Logging;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;

namespace Azure.Tools.GeneratorAgent.Agent
{
    internal class AgentOrchestrator
    {
        private readonly AgentProcessor AgentProcessor;
        private readonly AgentFileManager FileManager;
        private readonly AgentThreadManager AgentThreadManager;
        private readonly AgentManager AgentManager;
        private readonly ILogger<AgentOrchestrator> Logger;
        private readonly FixPromptService FixPromptService;
        private string AgentId => AgentManager.GetAgent().Id;

        public AgentOrchestrator(
            AgentProcessor agentProcessor,
            AgentFileManager fileManager,
            AgentThreadManager agentThreadManager,
            AgentManager agentManager,
            ILogger<AgentOrchestrator> logger,
            FixPromptService fixPromptService)
        {
            ArgumentNullException.ThrowIfNull(agentProcessor);
            ArgumentNullException.ThrowIfNull(fileManager);
            ArgumentNullException.ThrowIfNull(agentThreadManager);
            ArgumentNullException.ThrowIfNull(agentManager);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(fixPromptService);

            AgentProcessor = agentProcessor;
            FileManager = fileManager;
            AgentThreadManager = agentThreadManager;
            AgentManager = agentManager;
            Logger = logger;
            FixPromptService = fixPromptService;
        }

        public virtual async Task InitializeAgentEnvironmentAsync(Dictionary<string, string> typeSpecFiles, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(typeSpecFiles);
            
            Logger.LogInformation("Initializing agent environment...");

            // Step 1: Upload files
            var (uploadedFileIds, vectorStoreId) = await FileManager.UploadFilesAsync(typeSpecFiles, cancellationToken);

            // Step 2: Update Agent Vector Store (this will trigger agent creation if needed)
            await AgentManager.UpdateAgentVectorStoreAsync(AgentId, vectorStoreId, cancellationToken);

            Logger.LogInformation("Agent environment initialized successfully with agent {AgentId}.", AgentId);
        }

        public virtual async Task<string> FixCodeAsync(List<Fix> fixes, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fixes);
            
            try
            {
                // Generate a new thread ID for FixCodeAsync
                var fixThreadId = await AgentThreadManager.CreateThreadAsync(cancellationToken);

                // Call AgentProcessor to fix the code
                var result = await AgentProcessor.FixCodeAsync(fixes, fixThreadId, FixPromptService, cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Code fix failed for agent {AgentId}", AgentId);
                throw;
            }
        }

        public virtual async Task<List<RuleError>> AnalyzeErrorsAsync(string errorLogs, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(errorLogs);
            
            try
            {
                // Generate a new thread ID for AnalyzeErrorsAsync
                var analyzeThreadId = await AgentThreadManager.CreateThreadAsync(cancellationToken);

                // Call AgentProcessor to analyze errors
                var errors = await AgentProcessor.AnalyzeErrorsAsync(errorLogs, analyzeThreadId, cancellationToken);

                return errors;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error analysis failed for agent {AgentId}", AgentId);
                throw;
            }
        }
    }
}