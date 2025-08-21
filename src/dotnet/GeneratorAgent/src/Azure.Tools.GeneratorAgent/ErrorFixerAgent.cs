using System.Text;
using Azure.AI.Agents.Persistent;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    internal class ErrorFixerAgent : IAsyncDisposable
    {
        private readonly AppSettings AppSettings;
        private readonly ILogger<ErrorFixerAgent> Logger;
        private readonly PersistentAgentsClient Client;
        private readonly Lazy<PersistentAgent> Agent;
        private readonly FixPromptService FixPromptService;
        private readonly AgentResponseParser ResponseParser;
        private volatile bool Disposed = false;

        public ErrorFixerAgent(
            AppSettings appSettings,
            ILogger<ErrorFixerAgent> logger,
            PersistentAgentsClient client,
            FixPromptService fixPromptService,
            AgentResponseParser responseParser)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(fixPromptService);
            ArgumentNullException.ThrowIfNull(responseParser);

            AppSettings = appSettings;
            Logger = logger;
            Client = client;
            Agent = new Lazy<PersistentAgent>(() => CreateAgent());
            FixPromptService = fixPromptService;
            ResponseParser = responseParser;

        }

        internal virtual PersistentAgent CreateAgent()
        {
            Logger.LogInformation("Creating AZC Fixer agent...");

            var response = Client.Administration.CreateAgent(
                model: AppSettings.Model,
                name: AppSettings.AgentName,
                instructions: AppSettings.AgentInstructions,
                tools: new[] { new FileSearchToolDefinition() });

            var agent = response.Value;
            if (string.IsNullOrEmpty(agent?.Id))
            {
                throw new InvalidOperationException("Failed to create AZC Fixer agent");
            }

            Logger.LogInformation("Agent created successfully: {Name} ({Id})", agent.Name, agent.Id);
            return agent;
        }


        /// <summary>
        /// Processes a list of fixes by converting them to prompts and processing them sequentially,
        /// building upon the state maintained in the conversation thread.
        /// </summary>
        public async Task<string> FixCodeAsync(List<Fix> fixes, string threadId, CancellationToken cancellationToken = default)
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
                    var fixTypeName = fix switch
                    {
                        AgentPromptFix => nameof(AgentPromptFix),
                        _ => fix.GetType().Name // Fallback
                    };
                    
                    Logger.LogInformation("Processing fix {Current}/{Total}: {FixType}", 
                        i + 1, fixes.Count, fixTypeName);
                    try
                    {
                        // Apply single fix and get the updated TypeSpec content
                        // This maintains conversation state so the AI agent can see previous changes
                        finalUpdatedContent = await ProcessSingleFixWithStateAsync(fix, threadId, i + 1, cancellationToken).ConfigureAwait(false);
                        processedCount++;
                        
                        Logger.LogDebug("Successfully applied fix {Current}/{Total}. Content length: {Length}", 
                            i + 1, fixes.Count, finalUpdatedContent?.Length ?? 0);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to apply fix {Current}/{Total}: {FixType}", 
                            i + 1, fixes.Count, fixTypeName);
                        
                        // TODO: Add configuration for whether to stop on first failure or continue
                        throw; // For now, fail fast on any fix failure
                    }
                    
                    // Small delay between fixes to avoid overwhelming the AI service
                    if (i < fixes.Count - 1)
                    {
                        await Task.Delay(AppSettings.DelayBetweenFixesMs, cancellationToken).ConfigureAwait(false);
                    }
                }
                
                Logger.LogInformation("Successfully processed all {Count} fixes. Final content length: {Length}", 
                    fixes.Count, finalUpdatedContent?.Length ?? 0);
                
                return finalUpdatedContent ?? throw new InvalidOperationException("No fixes were successfully applied");
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                 Logger.LogError(ex, "Failed to complete code fix process for thread {ThreadId}. Processed {ProcessedCount}/{TotalCount} fixes", 
                    threadId, processedCount, fixes.Count);
                throw;
            }
        }

        /// <summary>
        /// Processes a single fix and returns the updated client.tsp content
        /// </summary>
        private async Task<string> ProcessSingleFixWithStateAsync(Fix fix, string threadId, int fixNumber, CancellationToken cancellationToken)
        {
            try
            {
                // Convert fix to structured prompt with violation details
                var prompt = FixPromptService.ConvertFixToPrompt(fix);
                
                // Send prompt to AI agent (maintains conversation history)
                await Client.Messages.CreateMessageAsync(threadId, MessageRole.User, prompt, cancellationToken: cancellationToken).ConfigureAwait(false);
                
                // Execute AI agent run to process the fix request
                await ProcessAgentRunAsync(threadId, cancellationToken).ConfigureAwait(false);
                
                // Read agent's response containing updated TypeSpec code
                var response = await ReadResponseAsync(threadId, cancellationToken).ConfigureAwait(false);
                
                // Parse response and extract corrected client.tsp content
                var agentResponse = ResponseParser.ParseResponse(response);
                
                Logger.LogInformation("Updated client.tsp content for fix #{FixNumber} ({Length} characters):\n{UpdatedContent}", 
                    fixNumber, agentResponse.Content.Length, agentResponse.Content);
                
                // Return corrected content for next fix to build upon
                return agentResponse.Content;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.LogError(ex, "Failed to process fix #{FixNumber}: {FixType}", fixNumber, fix.GetType().Name);
                throw;
            }
        }

        /// <summary>
        /// Processes an agent run and waits for completion
        /// </summary>
        private async Task ProcessAgentRunAsync(string threadId, CancellationToken cancellationToken)
        {
            var agent = Agent.Value;
            var runResponse = await Client.Runs.CreateRunAsync(threadId, agent.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
            var run = runResponse.Value;
            
            Logger.LogDebug("Created run {RunId} for thread {ThreadId}", run.Id, threadId);
            
            var maxWaitTime = AppSettings.AgentRunMaxWaitTime;
            var pollingInterval = AppSettings.AgentRunPollingInterval ?? TimeSpan.FromSeconds(5);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            RunStatus status;
            do
            {
                await Task.Delay(pollingInterval, cancellationToken).ConfigureAwait(false);
                var runUpdateResponse = await Client.Runs.GetRunAsync(threadId, run.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
                run = runUpdateResponse.Value;
                status = run.Status;
                
                Logger.LogDebug("Run {RunId} status: {Status} (elapsed: {Elapsed:F1}s)", 
                    run.Id, status, stopwatch.Elapsed.TotalSeconds);
                
                if (maxWaitTime.HasValue && stopwatch.Elapsed > maxWaitTime.Value)
                {
                    Logger.LogError("Agent run {RunId} timed out after {Elapsed:F1}s", 
                        run.Id, stopwatch.Elapsed.TotalSeconds);
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

        /// <summary>
        /// Reads the latest response from the agent thread
        /// </summary>
        private async Task<string> ReadResponseAsync(string threadId, CancellationToken cancellationToken)
        {
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

        public async Task<string> InitializeAgentEnvironmentAsync(
            Dictionary<string, string> typeSpecFiles, 
            CancellationToken ct = default)
        {
            var uploadedFilesIds = await UploadTspFromMemoryAsync(typeSpecFiles, ct);
            var uploadedFilesList = uploadedFilesIds.ToList();
            
            if (uploadedFilesList.Count == 0)
            {
                throw new InvalidOperationException("No TypeSpec files provided. Cannot proceed with AZC error fixing.");
            }

            await WaitForIndexingAsync(uploadedFilesList, ct);
            var vectorStoreId = await CreateVectorStoreAsync(uploadedFilesList, ct);
            await UpdateAgentVectorStoreAsync(vectorStoreId, ct);

            var threadResponse = await Client.Threads.CreateThreadAsync(cancellationToken: ct).ConfigureAwait(false);
            var thread = threadResponse.Value;
            Logger.LogInformation("Created new thread with ID: {ThreadId}", thread.Id);

            return thread.Id;
        }

        private async Task<IEnumerable<string>> UploadTspFromMemoryAsync(
            Dictionary<string, string> typeSpecFiles, 
            CancellationToken ct)
        {
            var relevantFiles = typeSpecFiles.Where(kvp => 
                kvp.Key.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase));

            var uploadTasks = relevantFiles.Select(file =>
                UploadSingleFileFromMemoryAsync(file.Key, file.Value, ct));

            var results = await Task.WhenAll(uploadTasks);
            var uploadedFilesIds = results.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!);

            Logger.LogInformation("Successfully uploaded TypeSpec files from memory");
            return uploadedFilesIds;
        }

        private async Task<string?> UploadSingleFileFromMemoryAsync(
            string fileName, 
            string content, 
            CancellationToken ct)
        {
            try
            {
                var txtFileName = Path.ChangeExtension(fileName, "txt");

                using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                
                var uploaded = await Client.Files.UploadFileAsync(
                    contentStream,
                    PersistentAgentFilePurpose.Agents,
                    txtFileName,
                    cancellationToken: ct
                ).ConfigureAwait(false);

                if (uploaded?.Value?.Id != null)
                {
                    Logger.LogDebug("Uploaded TypeSpec file from memory: {FileName} -> {FileId}",
                        fileName, uploaded.Value.Id);
                    return uploaded.Value.Id;
                }
                else
                {
                    Logger.LogWarning("Failed to upload file from memory: {FileName}", fileName);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error uploading file from memory: {FileName}", fileName);
                return null;
            }
        }

        private async Task WaitForIndexingAsync(List<string> uploadedFilesIds, CancellationToken ct)
        {
            var maxWaitTime = AppSettings.IndexingMaxWaitTime;
            var pollingInterval = AppSettings.IndexingPollingInterval;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Logger.LogInformation("Waiting for {Count} files to be indexed... (timeout: {Timeout}s, polling interval: {Interval}s)", 
                uploadedFilesIds.Count, maxWaitTime.TotalSeconds, pollingInterval.TotalSeconds);

            const int batchSize = 10;
            while (stopwatch.Elapsed < maxWaitTime)
            {
                var results = new List<(string FileId, PersistentAgentFileInfo? File)>();
                
                for (int i = 0; i < uploadedFilesIds.Count; i += batchSize)
                {
                    var batch = uploadedFilesIds.Skip(i).Take(batchSize);
                    var checkTasks = batch.Select(fileId => CheckFileStatusAsync(fileId, ct));
                    var batchResults = await Task.WhenAll(checkTasks);
                    results.AddRange(batchResults);
                }
                
                var allIndexed = true;
                var pendingFiles = Logger.IsEnabled(LogLevel.Debug) ? new List<string>() : null;
                var currentStatusCounts = Logger.IsEnabled(LogLevel.Information) ? new Dictionary<string, int>() : null;
                
                foreach ((string FileId, PersistentAgentFileInfo? File) result in results)
                {
                    if (result.File == null)
                    {
                        Logger.LogWarning("Could not retrieve status for file: {FileId}", result.FileId);
                        allIndexed = false;
                        pendingFiles?.Add(result.FileId);
                        if (currentStatusCounts != null)
                        {
                            currentStatusCounts["Unknown"] = currentStatusCounts.GetValueOrDefault("Unknown") + 1;
                        }
                    }
                    else
                    {
                        var status = result.File?.Status.ToString() ?? "Unknown";
                        if (currentStatusCounts != null)
                        {
                            currentStatusCounts[status] = currentStatusCounts.GetValueOrDefault(status) + 1;
                        }
                        
                        Logger.LogDebug("File {Filename} (ID: {FileId}) status: {Status}", 
                            result.File?.Filename, result.FileId, status);

                        if (!status.Equals("Processed", StringComparison.OrdinalIgnoreCase))
                        {
                            allIndexed = false;
                            if (result.File != null)
                            {
                                pendingFiles?.Add($"{result.File.Filename}({status})");
                            }
                        }
                    }
                }

                if (Logger.IsEnabled(LogLevel.Information) && currentStatusCounts != null)
                {
                    var statusSummary = string.Join(", ", currentStatusCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                    Logger.LogInformation("Indexing status summary: {StatusSummary} | Elapsed: {Elapsed:F1}s", 
                        statusSummary, stopwatch.Elapsed.TotalSeconds);
                }

                if (allIndexed)
                {
                    Logger.LogInformation("All {Count} files indexed successfully in {Duration:F1}s", 
                        uploadedFilesIds.Count, stopwatch.Elapsed.TotalSeconds);
                    return;
                }

                if (Logger.IsEnabled(LogLevel.Debug) && pendingFiles?.Count <= 3)
                {
                    Logger.LogDebug("Still waiting for: {PendingFiles}", string.Join(", ", pendingFiles));
                }

                await Task.Delay(pollingInterval, ct).ConfigureAwait(false);
            }

            var finalResults = await Task.WhenAll(uploadedFilesIds.Select(id => CheckFileStatusAsync(id, ct))).ConfigureAwait(false);
            
            var allProcessed = finalResults.All(r => r.File?.Status.ToString()?.Equals("Processed", StringComparison.OrdinalIgnoreCase) == true);
            if (allProcessed)
            {
                Logger.LogInformation("All {Count} files indexed successfully in {Duration:F1}s (completed during final check)", 
                    uploadedFilesIds.Count, stopwatch.Elapsed.TotalSeconds);
                return;
            }

            var finalSummary = finalResults
                .Where(r => r.File != null)
                .GroupBy(r => r.File!.Status.ToString() ?? "Unknown")
                .ToDictionary(g => g.Key ?? "Unknown", g => g.Count());
            
            var finalStatusSummary = string.Join(", ", finalSummary.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            
            throw new TimeoutException(
                $"Timeout after {maxWaitTime.TotalSeconds}s while waiting for file indexing to complete. " +
                $"Final status: {finalStatusSummary}. " +
                $"This may indicate the Azure AI service is experiencing delays or the files require more processing time.");
        }

        private async Task<(string FileId, PersistentAgentFileInfo? File)> CheckFileStatusAsync(string fileId, CancellationToken ct)
        {
            try
            {
                var file = await Client.Files.GetFileAsync(fileId, cancellationToken: ct).ConfigureAwait(false);
                return (fileId, file);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                Logger.LogWarning("File not found (404): {FileId} - it may have been deleted or not yet available", fileId);
                return (fileId, null);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status >= 400 && ex.Status < 500)
            {
                Logger.LogWarning("Client error ({StatusCode}) checking file {FileId}: {Message}", 
                    ex.Status, fileId, ex.Message);
                return (fileId, null);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status >= 500)
            {
                Logger.LogWarning("Server error ({StatusCode}) checking file {FileId}: {Message} - will retry", 
                    ex.Status, fileId, ex.Message);
                return (fileId, null);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Unexpected error checking status for file: {FileId}", fileId);
                return (fileId, null);
            }
        }

        private async Task UpdateAgentVectorStoreAsync(string vectorStoreId, CancellationToken ct)
        {
            var agent = Agent.Value;

            // Replace the FileSearchToolResource with a new one containing only the latest store
            var updated = await Client.Administration.UpdateAgentAsync(
                agent.Id,
                toolResources: new ToolResources
                {
                    FileSearch = new FileSearchToolResource
                    {
                        VectorStoreIds = { vectorStoreId }
                    }
                },
                cancellationToken: ct
            ).ConfigureAwait(false);

            Logger.LogInformation("Agent vector store updated to: {VectorStoreId}", vectorStoreId);
        }


        private async Task<string> CreateVectorStoreAsync(List<string> fileIds, CancellationToken ct)
        {
            var storeName = $"azc-{DateTime.Now:yyyyMMddHHmmss}";

            Logger.LogInformation("Creating vector store '{StoreName}' with {Count} files...", storeName, fileIds.Count);

            var store = await Client.VectorStores.CreateVectorStoreAsync(
                fileIds,
                name: storeName,
                cancellationToken: ct
            ).ConfigureAwait(false);

            Logger.LogInformation("Created vector store: {Name} ({Id})", store.Value.Name, store.Value.Id);

            Logger.LogDebug("Waiting 5 seconds for vector store to be ready...");
            await Task.Delay(5000, ct).ConfigureAwait(false);

            return store.Value.Id;
        }

        private async Task DeleteAgentsAsync(CancellationToken ct)
        {
            var deleteTasks = new List<Task>();

            await foreach (PersistentAgent agent in Client.Administration.GetAgentsAsync(cancellationToken: ct).ConfigureAwait(false))
            {
                var deleteTask = Client.Administration.DeleteAgentAsync(agent.Id, ct)
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
