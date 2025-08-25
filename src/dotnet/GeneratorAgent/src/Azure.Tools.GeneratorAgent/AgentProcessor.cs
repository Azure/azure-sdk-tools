using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.GeneratorAgent
{
    internal class AgentProcessor
    {
        private readonly PersistentAgentsClient Client;
        private readonly ILogger<AgentProcessor> Logger;
        private readonly AgentResponseParser ResponseParser;
        private readonly AgentManager AgentManager;
        private readonly ThreadManager ThreadManager;
        private readonly AppSettings AppSettings;
        
        // Lazy property that gets the agent ID on demand
        private string AgentId => AgentManager.GetAgent().Id;

        public AgentProcessor(PersistentAgentsClient client, ILogger<AgentProcessor> logger, AgentResponseParser responseParser, AgentManager agentManager, ThreadManager threadManager, AppSettings appSettings)
        {
            Client = client;
            Logger = logger;
            ResponseParser = responseParser;
            AgentManager = agentManager;
            ThreadManager = threadManager;
            AppSettings = appSettings;
        }

        public async Task<string> FixCodeAsync(List<Fix> fixes, string threadId, FixPromptService fixPromptService, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Starting code fix process with {Count} fixes using thread {ThreadId}", fixes.Count, threadId);

            var finalUpdatedContent = (string?)null;
            var processedCount = 0;

            try
            {
                // Process each fix sequentially to maintain conversation context
                // Each fix builds upon the previous one's result
                for (var i = 0; i < fixes.Count; i++)
                {
                    var fix = fixes[i];
                    var prompt = fixPromptService.ConvertFixToPrompt(fix);

                    await Client.Messages.CreateMessageAsync(threadId, MessageRole.User, prompt, cancellationToken: cancellationToken).ConfigureAwait(false);
                    await ThreadManager.ProcessAgentRunAsync(threadId, AgentId, cancellationToken).ConfigureAwait(false);

                    var response = await ThreadManager.ReadResponseAsync(threadId, cancellationToken).ConfigureAwait(false);
                    var agentResponse = ResponseParser.ParseResponse(response);

                    finalUpdatedContent = agentResponse.Content;
                    processedCount++;
                }

                Logger.LogInformation("Successfully processed all {Count} fixes. Final content length: {Length}", fixes.Count, finalUpdatedContent?.Length ?? 0);
                return finalUpdatedContent ?? throw new InvalidOperationException("No fixes were successfully applied");
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.LogError(ex, "Failed to complete code fix process for thread {ThreadId}. Processed {ProcessedCount}/{TotalCount} fixes", threadId, processedCount, fixes.Count);
                throw;
            }
        }

        public async Task<List<RuleError>> AnalyzeErrorsAsync(string errorLogs, string threadId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(errorLogs);

            Logger.LogInformation("Starting AI-based error analysis for thread {ThreadId}", threadId);

            try
            {
                // Create analysis prompt using the template from AppSettings
                var analysisPrompt = string.Format(AppSettings.ErrorAnalysisPromptTemplate, errorLogs);

                await Client.Messages.CreateMessageAsync(threadId, MessageRole.User, analysisPrompt, cancellationToken: cancellationToken).ConfigureAwait(false);
                await ThreadManager.ProcessAgentRunAsync(threadId, AgentId, cancellationToken).ConfigureAwait(false);

                var response = await ThreadManager.ReadResponseAsync(threadId, cancellationToken).ConfigureAwait(false);
                var ruleErrors = ResponseParser.ParseErrors(response);

                Logger.LogInformation("AI-based error analysis completed. Found {Count} errors.", ruleErrors.Count);
                return ruleErrors;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to analyze errors with AI for thread {ThreadId}", threadId);
                return new List<RuleError>();
            }
        }

    }
}
