using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.GeneratorAgent.Agent
{
    internal class AgentProcessor
    {
        private readonly PersistentAgentsClient Client;
        private readonly ILogger<AgentProcessor> Logger;
        private readonly AgentManager AgentManager;
        private readonly AgentThreadManager AgentThreadManager;
        private readonly AppSettings AppSettings;
        
        // Lazy property that gets the agent ID on demand
        private string AgentId => AgentManager.GetAgent().Id;

        public AgentProcessor(PersistentAgentsClient client, ILogger<AgentProcessor> logger, AgentManager agentManager, AgentThreadManager agentThreadManager, AppSettings appSettings)
        {
            Client = client;
            Logger = logger;
            AgentManager = agentManager;
            AgentThreadManager = agentThreadManager;
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
                    await AgentThreadManager.ProcessAgentRunAsync(threadId, AgentId, cancellationToken).ConfigureAwait(false);

                    var response = await AgentThreadManager.ReadResponseAsync(threadId, cancellationToken).ConfigureAwait(false);
                    
                    try
                    {
                        var agentResponse = AgentResponseParserHelpers.ParseResponse(response);
                        finalUpdatedContent = agentResponse.Content;
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to parse agent response for fix {FixIndex}/{TotalCount}. Response: {Response}", i + 1, fixes.Count, response);
                        throw;
                    }
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
                await AgentThreadManager.ProcessAgentRunAsync(threadId, AgentId, cancellationToken).ConfigureAwait(false);

                var response = await AgentThreadManager.ReadResponseAsync(threadId, cancellationToken).ConfigureAwait(false);
                
                try
                {
                    var ruleErrors = AgentResponseParserHelpers.ParseErrors(response);
                    Logger.LogInformation("AI-based error analysis completed. Found {Count} errors.", ruleErrors.Count);
                    return ruleErrors;
                }
                catch (Exception parseEx)
                {
                    Logger.LogError(parseEx, "Failed to parse AI error analysis response for thread {ThreadId}. Response: {Response}", threadId, response);
                    return new List<RuleError>();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to analyze errors with AI for thread {ThreadId}", threadId);
                return new List<RuleError>();
            }
        }

    }
}
