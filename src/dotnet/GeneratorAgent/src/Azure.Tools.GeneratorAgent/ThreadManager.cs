using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    internal class ThreadManager
    {
        private readonly PersistentAgentsClient Client;
        private readonly ILogger<ThreadManager> Logger;
        private readonly AppSettings AppSettings;

        public ThreadManager(PersistentAgentsClient client, ILogger<ThreadManager> logger, AppSettings appSettings)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(appSettings);
            
            Client = client;
            Logger = logger;
            AppSettings = appSettings;
        }

        public async Task<string> CreateThreadAsync(CancellationToken cancellationToken = default)
        {
            var threadResponse = await Client.Threads.CreateThreadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var thread = threadResponse.Value;
            Logger.LogInformation("Created new thread with ID: {ThreadId}", thread.Id);
            return thread.Id;
        }

        public async Task<string> ReadResponseAsync(string threadId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(threadId);
            ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
            
            var messages = Client.Messages.GetMessagesAsync(threadId, order: ListSortOrder.Descending, cancellationToken: cancellationToken);
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

        public async Task ProcessAgentRunAsync(string threadId, string agentId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(threadId);
            ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
            ArgumentNullException.ThrowIfNull(agentId);
            ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
            
            var runResponse = await Client.Runs.CreateRunAsync(threadId, agentId, cancellationToken: cancellationToken).ConfigureAwait(false);
            var run = runResponse.Value;

            Logger.LogDebug("Created run {RunId} for thread {ThreadId}", run.Id, threadId);

            var maxWaitTime = AppSettings.AgentRunMaxWaitTime;
            var pollingInterval = AppSettings.AgentRunPollingInterval;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            RunStatus status;
            do
            {
                await Task.Delay(pollingInterval, cancellationToken).ConfigureAwait(false);
                var runUpdateResponse = await Client.Runs.GetRunAsync(threadId, run.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
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
