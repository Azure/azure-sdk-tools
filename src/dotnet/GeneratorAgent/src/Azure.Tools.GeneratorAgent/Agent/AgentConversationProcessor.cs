using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Tools.ErrorAnalyzers;

namespace Azure.Tools.GeneratorAgent.Agent
{
    /// <summary>
    /// Manages a single conversation thread with an AI agent.
    /// Each instance owns and manages its own thread lifecycle.
    /// </summary>
    internal class AgentConversationProcessor
    {
        private readonly PersistentAgentsClient Client;
        private readonly ILogger<AgentConversationProcessor> Logger;
        private readonly AppSettings AppSettings;
        private readonly string AgentId;
        private readonly string ThreadId;

        /// <summary>
        /// Creates a new conversation processor with its own thread.
        /// This should be used instead of the constructor.
        /// </summary>
        public static async Task<AgentConversationProcessor> CreateAsync(
            PersistentAgentsClient client,
            ILoggerFactory loggerFactory,
            AppSettings appSettings,
            string agentId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

            var logger = loggerFactory.CreateLogger<AgentConversationProcessor>();

            // Create the thread first
            var threadResponse = await client.Threads.CreateThreadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var thread = threadResponse.Value;
            logger.LogInformation("Created new conversation thread with ID: {ThreadId}", thread.Id);

            return new AgentConversationProcessor(client, logger, appSettings, agentId, thread.Id);
        }

        private AgentConversationProcessor(
            PersistentAgentsClient client,
            ILogger<AgentConversationProcessor> logger,
            AppSettings appSettings,
            string agentId,
            string threadId)
        {
            Client = client;
            Logger = logger;
            AppSettings = appSettings;
            AgentId = agentId;
            ThreadId = threadId;
        }

        public async Task<string> FixCodeAsync(List<Fix> fixes, FixPromptService fixPromptService, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Starting code fix process with {Count} fixes", fixes.Count);

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

                    // Send message to the conversation thread
                    await Client.Messages.CreateMessageAsync(ThreadId, MessageRole.User, prompt, cancellationToken: cancellationToken).ConfigureAwait(false);

                    // Process the agent run and wait for completion
                    await ProcessAgentRunAsync(cancellationToken).ConfigureAwait(false);

                    // Read the agent's response
                    var response = await ReadResponseAsync(cancellationToken).ConfigureAwait(false);

                    try
                    {
                        var agentResponse = AgentResponseParser.ParseResponse(response);
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
                Logger.LogError(ex, "Failed to complete code fix process. Processed {ProcessedCount}/{TotalCount} fixes", processedCount, fixes.Count);
                throw;
            }
        }

        public async Task<IEnumerable<RuleError>> AnalyzeErrorsAsync(string errorLogs, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(errorLogs);

            Logger.LogInformation("Starting AI-based error analysis");

            try
            {
                // Create analysis prompt using the template from AppSettings
                var analysisPrompt = string.Format(AppSettings.ErrorAnalysisPromptTemplate, errorLogs);

                // Send message to the conversation thread
                await Client.Messages.CreateMessageAsync(ThreadId, MessageRole.User, analysisPrompt, cancellationToken: cancellationToken).ConfigureAwait(false);

                // Process the agent run and wait for completion
                await ProcessAgentRunAsync(cancellationToken).ConfigureAwait(false);

                // Read the agent's response
                var response = await ReadResponseAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    var ruleErrors = AgentResponseParser.ParseErrors(response);
                    var materializedErrors = ruleErrors.ToList();
                    Logger.LogInformation("AI-based error analysis completed. Found {Count} errors.", materializedErrors.Count);
                    return materializedErrors;
                }
                catch (Exception parseEx)
                {
                    Logger.LogError(parseEx, "Failed to parse AI error analysis response. Response: {Response}", response);
                    return Enumerable.Empty<RuleError>();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "AI-based error analysis failed");
                throw;
            }
        }

        private async Task<string> ReadResponseAsync(CancellationToken cancellationToken)
        {
            var messages = Client.Messages.GetMessagesAsync(ThreadId, order: ListSortOrder.Descending, cancellationToken: cancellationToken);
            var assistantResponses = new List<string>();

            await foreach (var message in messages.ConfigureAwait(false))
            {
                Logger.LogDebug("Message role: {Role}", message.Role);

                if (message.Role != MessageRole.User)
                {
                    foreach (MessageTextContent content in message.ContentItems.OfType<MessageTextContent>())
                    {
                        Logger.LogDebug("Assistant message content: {Text}", content.Text);
                        assistantResponses.Add(content.Text);
                    }
                    break;
                }
            }

            if (assistantResponses.Count == 0)
            {
                throw new InvalidOperationException("No assistant response found in thread messages");
            }

            return string.Join("\n", assistantResponses);
        }

        private async Task ProcessAgentRunAsync(CancellationToken cancellationToken)
        {
            var runResponse = await Client.Runs.CreateRunAsync(ThreadId, AgentId, cancellationToken: cancellationToken).ConfigureAwait(false);
            var run = runResponse.Value;

            Logger.LogDebug("Created run {RunId} for thread {ThreadId}", run.Id, ThreadId);

            var maxWaitTime = AppSettings.AgentRunMaxWaitTime;
            var pollingInterval = AppSettings.AgentRunPollingInterval;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            RunStatus status;
            do
            {
                await Task.Delay(pollingInterval, cancellationToken).ConfigureAwait(false);
                var runUpdateResponse = await Client.Runs.GetRunAsync(ThreadId, run.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
                run = runUpdateResponse.Value;
                status = run.Status;

                Logger.LogDebug("Run {RunId} status: {Status} (elapsed: {Elapsed:F1}s)", run.Id, status, stopwatch.Elapsed.TotalSeconds);

                if (stopwatch.Elapsed > maxWaitTime)
                {
                    Logger.LogError("Agent run {RunId} timed out after {Elapsed:F1}s", run.Id, stopwatch.Elapsed.TotalSeconds);
                    throw new TimeoutException($"Agent run timed out after {stopwatch.Elapsed.TotalSeconds:F1} seconds");
                }
            }
            while (status == RunStatus.Queued || status == RunStatus.InProgress);

            if (status != RunStatus.Completed)
            {
                Logger.LogError("Agent run {RunId} failed with status: {Status}", run.Id, status);
                throw new InvalidOperationException($"Agent run failed with status: {status}");
            }
        }

    }
}
