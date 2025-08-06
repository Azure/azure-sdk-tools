using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Linq;

namespace Azure.Tools.GeneratorAgent
{
    internal class ErrorFixerAgent : IAsyncDisposable
    {
        private readonly AppSettings AppSettings;
        private readonly ILogger<ErrorFixerAgent> Logger;
        private readonly PersistentAgentsClient Client;
        private readonly Lazy<PersistentAgent> Agent;
        private readonly SemaphoreSlim ConcurrencyLimiter;
        private volatile bool Disposed = false;

        public ErrorFixerAgent(
            AppSettings appSettings,
            ILogger<ErrorFixerAgent> logger,
            PersistentAgentsClient client)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(client);

            AppSettings = appSettings;
            Logger = logger;
            Client = client;
            Agent = new Lazy<PersistentAgent>(() => CreateAgent());

            ConcurrencyLimiter = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        }

        internal virtual PersistentAgent CreateAgent()
        {
            Logger.LogInformation("Creating AZC Fixer agent...");

            Response<PersistentAgent> response = Client.Administration.CreateAgent(
                model: AppSettings.Model,
                name: AppSettings.AgentName,
                instructions: AppSettings.AgentInstructions,
                tools: new[] { new FileSearchToolDefinition() });

            PersistentAgent agent = response.Value;
            if (string.IsNullOrEmpty(agent?.Id))
            {
                throw new InvalidOperationException("Failed to create AZC Fixer agent");
            }

            Logger.LogInformation("Agent created successfully: {Name} ({Id})", agent.Name, agent.Id);
            return agent;
        }

        public async Task FixCodeAsync(CancellationToken ct)
        {
            PersistentAgent agent = Agent.Value;

            // TODO: Implement the code fixing logic here
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task<string> InitializeAgentEnvironmentAsync(string typeSpecDir, CancellationToken ct = default)
        {
            List<string> uploadedFilesIds = await UploadTspAsync(typeSpecDir, ct);
            if (uploadedFilesIds.Count == 0)
                throw new InvalidOperationException("No TypeSpec files (*.tsp) found in the directory. Cannot proceed with AZC error fixing.");

            await WaitForIndexingAsync(uploadedFilesIds, ct);
            string vectorStoreId = await CreateVectorStoreAsync(uploadedFilesIds, ct);
            await UpdateAgentVectorStoreAsync(vectorStoreId, ct);

            PersistentAgentThread thread = await Client.Threads.CreateThreadAsync(cancellationToken: ct);
            Logger.LogInformation("Created new thread with ID: {ThreadId}", thread.Id);

            return thread.Id;
        }

        private async Task<List<string>> UploadTspAsync(string typeSpecDir, CancellationToken ct)
        {
            string[] tspFiles = Directory.GetFiles(typeSpecDir, "*.tsp", SearchOption.AllDirectories);

            IEnumerable<Task<string?>> uploadTasks = tspFiles.Select(async file =>
            {
                await ConcurrencyLimiter.WaitAsync(ct);
                try
                {
                    return await UploadSingleFileAsync(file, ct);
                }
                finally
                {
                    ConcurrencyLimiter.Release();
                }
            });

            string?[] results = await Task.WhenAll(uploadTasks);
            List<string> uploadedFilesIds = results.Where(id => !string.IsNullOrEmpty(id)).Cast<string>().ToList();

            Logger.LogInformation("Successfully uploaded {Count}/{Total} TypeSpec files as text files", uploadedFilesIds.Count, tspFiles.Length);
            return uploadedFilesIds;
        }

        private async Task<string?> UploadSingleFileAsync(string filePath, CancellationToken ct)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                // Read the .tsp file content and upload as .txt since .tsp is not supported
                string content = await File.ReadAllTextAsync(filePath, ct);
                string tempTxtPath = Path.GetTempFileName();
                string txtFileName = $"{fileName}.txt";

                try
                {
                    // Write content to a temporary .txt file
                    await File.WriteAllTextAsync(tempTxtPath, content, ct);

                    using FileStream fileStream = new FileStream(tempTxtPath, FileMode.Open, FileAccess.Read);
                    Response<PersistentAgentFileInfo>? uploaded = await Client.Files.UploadFileAsync(
                        fileStream,
                        PersistentAgentFilePurpose.Agents,
                        txtFileName,
                        cancellationToken: ct
                    );

                    if (uploaded?.Value?.Id != null)
                    {
                        Logger.LogDebug("Uploaded TypeSpec file as text: {OriginalFile} -> {FileName} -> {FileId}",
                            Path.GetFileName(filePath), txtFileName, uploaded.Value.Id);
                        return uploaded.Value.Id;
                    }
                    else
                    {
                        Logger.LogWarning("Failed to upload file: {FileName}", fileName);
                        return null;
                    }
                }
                finally
                {
                    // Clean up temporary file
                    if (File.Exists(tempTxtPath))
                    {
                        File.Delete(tempTxtPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error uploading file: {FilePath}", filePath);
                return null;
            }
        }

        private async Task WaitForIndexingAsync(List<string> uploadedFilesIds, CancellationToken ct)
        {
            TimeSpan maxWaitTime = TimeSpan.FromSeconds(180);
            TimeSpan pollingInterval = TimeSpan.FromSeconds(5);
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Logger.LogInformation("Waiting for {Count} files to be indexed... (timeout: {Timeout}s)", 
                uploadedFilesIds.Count, maxWaitTime.TotalSeconds);

            Dictionary<string, int> statusCounts = new Dictionary<string, int>();

            while (stopwatch.Elapsed < maxWaitTime)
            {
                List<Task<(string FileId, PersistentAgentFileInfo? File)>> checkTasks = new List<Task<(string FileId, PersistentAgentFileInfo? File)>>();

                foreach (string fileId in uploadedFilesIds)
                {
                    checkTasks.Add(CheckFileStatusAsync(fileId, ct));
                }

                (string FileId, PersistentAgentFileInfo? File)[] results = await Task.WhenAll(checkTasks);
                
                statusCounts.Clear();
                bool allIndexed = true;
                List<string> pendingFiles = new List<string>();
                
                foreach ((string FileId, PersistentAgentFileInfo? File) result in results)
                {
                    if (result.File == null)
                    {
                        Logger.LogWarning("Could not retrieve status for file: {FileId}", result.FileId);
                        allIndexed = false;
                        pendingFiles.Add(result.FileId);
                        statusCounts["Unknown"] = statusCounts.GetValueOrDefault("Unknown") + 1;
                    }
                    else
                    {
                        string status = result.File?.Status.ToString() ?? "Unknown";
                        statusCounts[status] = statusCounts.GetValueOrDefault(status) + 1;
                        
                        Logger.LogDebug("File {Filename} (ID: {FileId}) status: {Status}", 
                            result.File?.Filename, result.FileId, status);

                        if (!status.Equals("Processed", StringComparison.OrdinalIgnoreCase))
                        {
                            allIndexed = false;
                            pendingFiles.Add($"{result.File?.Filename}({status})");
                        }
                    }
                }

                string statusSummary = string.Join(", ", statusCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                Logger.LogInformation("Indexing status summary: {StatusSummary} | Elapsed: {Elapsed:F1}s", 
                    statusSummary, stopwatch.Elapsed.TotalSeconds);

                if (allIndexed)
                {
                    Logger.LogInformation("All {Count} files indexed successfully in {Duration:F1}s", 
                        uploadedFilesIds.Count, stopwatch.Elapsed.TotalSeconds);
                    return;
                }

                if (pendingFiles.Count <= 3)
                {
                    Logger.LogDebug("Still waiting for: {PendingFiles}", string.Join(", ", pendingFiles));
                }

                await Task.Delay(pollingInterval, ct);
            }

            (string FileId, PersistentAgentFileInfo? File)[] finalResults = await Task.WhenAll(uploadedFilesIds.Select(id => CheckFileStatusAsync(id, ct)));
            Dictionary<string, int> finalSummary = finalResults
                .Where(r => r.File != null)
                .GroupBy(r => r.File!.Status.ToString() ?? "Unknown")
                .ToDictionary(g => g.Key ?? "Unknown", g => g.Count());
            
            string finalStatusSummary = string.Join(", ", finalSummary.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            
            throw new TimeoutException(
                $"Timeout after {maxWaitTime.TotalSeconds}s while waiting for file indexing to complete. " +
                $"Final status: {finalStatusSummary}. " +
                $"This may indicate the Azure AI service is experiencing delays or the files require more processing time.");
        }

        private async Task<(string FileId, PersistentAgentFileInfo? File)> CheckFileStatusAsync(string fileId, CancellationToken ct)
        {
            try
            {
                PersistentAgentFileInfo file = await Client.Files.GetFileAsync(fileId, cancellationToken: ct);
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
            PersistentAgent agent = Agent.Value;

            // Replace the FileSearchToolResource with a new one containing only the latest store
            Response<PersistentAgent> updated = await Client.Administration.UpdateAgentAsync(
                agent.Id,
                toolResources: new ToolResources
                {
                    FileSearch = new FileSearchToolResource
                    {
                        VectorStoreIds = { vectorStoreId }
                    }
                },
                cancellationToken: ct
            );

            Logger.LogInformation("Agent vector store updated to: {VectorStoreId}", vectorStoreId);
        }


        private async Task<string> CreateVectorStoreAsync(List<string> fileIds, CancellationToken ct)
        {
            string storeName = $"azc-{DateTime.Now:yyyyMMddHHmmss}";

            Logger.LogInformation("Creating vector store '{StoreName}' with {Count} files...", storeName, fileIds.Count);

            var store = await Client.VectorStores.CreateVectorStoreAsync(
                fileIds,
                name: storeName,
                cancellationToken: ct
            );

            Logger.LogInformation("Created vector store: {Name} ({Id})", store.Value.Name, store.Value.Id);

            Logger.LogDebug("Waiting 5 seconds for vector store to be ready...");
            await Task.Delay(5000, ct);

            return store.Value.Id;
        }

        private async Task DeleteAgentsAsync(CancellationToken ct)
        {
            List<Task> deleteTasks = new List<Task>();

            await foreach (PersistentAgent agent in Client.Administration.GetAgentsAsync(cancellationToken: ct))
            {
                Task deleteTask = Client.Administration.DeleteAgentAsync(agent.Id, ct)
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
