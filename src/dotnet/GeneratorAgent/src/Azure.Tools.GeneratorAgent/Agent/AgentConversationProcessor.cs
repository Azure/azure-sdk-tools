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

            logger.LogDebug("Created new conversation thread with ID: {ThreadId}", thread.Id);

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

        public async Task<Result<string>> FixCodeAsync(List<Fix> fixes, FixPromptService fixPromptService, CancellationToken cancellationToken = default)
        {
            // Create single comprehensive prompt for all fixes
            var batchPrompt = fixPromptService.ConvertFixesToBatchPrompt(fixes);

            // Send message to the conversation thread
            await Client.Messages.CreateMessageAsync(ThreadId, MessageRole.User, batchPrompt, cancellationToken: cancellationToken).ConfigureAwait(false);

            // Process the agent run and wait for completion
            await ProcessAgentRunAsync(cancellationToken).ConfigureAwait(false);

            // Read the agent's response
            var responseResult = await ReadResponseAsync(cancellationToken).ConfigureAwait(false);
            if (responseResult.IsFailure)
            {
                return Result<string>.Failure(responseResult.Exception ?? new InvalidOperationException("Failed to read agent response"));
            }

            var response = responseResult.Value!;

            // Parse response - let exceptions bubble up to boundary
            var agentResponse = AgentResponseParser.ParseResponse(response);
            var finalUpdatedContent = agentResponse.Content;
            
            if (string.IsNullOrEmpty(finalUpdatedContent))
            {
                return Result<string>.Failure(new InvalidOperationException("No fixes were successfully applied"));
            }

            return Result<string>.Success(finalUpdatedContent);
        }

        public async Task<Result<IEnumerable<RuleError>>> AnalyzeErrorsAsync(string errorLogs, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(errorLogs);

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Starting AI-based error analysis");
            }

            // Create analysis prompt using the template from AppSettings
            var analysisPrompt = string.Format(AppSettings.ErrorAnalysisPromptTemplate, errorLogs);

            // Send message to the conversation thread
            await Client.Messages.CreateMessageAsync(ThreadId, MessageRole.User, analysisPrompt, cancellationToken: cancellationToken).ConfigureAwait(false);

            // Process the agent run and wait for completion
            await ProcessAgentRunAsync(cancellationToken).ConfigureAwait(false);

            // Read the agent's response
            var responseResult = await ReadResponseAsync(cancellationToken).ConfigureAwait(false);
            if (responseResult.IsFailure)
            {
                return Result<IEnumerable<RuleError>>.Failure(responseResult.Exception ?? new InvalidOperationException("Failed to read agent response"));
            }

            var response = responseResult.Value!;
            var ruleErrors = AgentResponseParser.ParseErrors(response);
            
            return Result<IEnumerable<RuleError>>.Success(ruleErrors.ToList());
        }

        private async Task<Result<string>> ReadResponseAsync(CancellationToken cancellationToken)
        {
            var messages = Client.Messages.GetMessagesAsync(ThreadId, order: ListSortOrder.Descending, cancellationToken: cancellationToken);
            var assistantResponses = new List<string>();

            await foreach (var message in messages.ConfigureAwait(false))
            {
                if (message.Role != MessageRole.User)
                {
                    foreach (MessageTextContent content in message.ContentItems.OfType<MessageTextContent>())
                    {
                        assistantResponses.Add(content.Text);
                    }
                    break;
                }
            }

            if (assistantResponses.Count == 0)
            {
                return Result<string>.Failure(new InvalidOperationException("No assistant response found in thread messages"));
            }

            return Result<string>.Success(string.Join("\n", assistantResponses));
        }

        private async Task ProcessAgentRunAsync(CancellationToken cancellationToken)
        {
            var runResponse = await Client.Runs.CreateRunAsync(ThreadId, AgentId, cancellationToken: cancellationToken).ConfigureAwait(false);
            var run = runResponse.Value;

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Created run {RunId} for thread {ThreadId}", run.Id, ThreadId);
            }

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

                // Only log status periodically to avoid noise
                if (Logger.IsEnabled(LogLevel.Debug) && stopwatch.Elapsed.TotalSeconds % 10 < 1)
                {
                    Logger.LogDebug("Run {RunId} status: {Status} (elapsed: {Elapsed:F1}s)", run.Id, status, stopwatch.Elapsed.TotalSeconds);
                }

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
