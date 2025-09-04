using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Azure.Tools.GeneratorAgent.Agent
{
    internal class AgentFileManager
    {
        private readonly PersistentAgentsClient Client;
        private readonly ILogger<AgentFileManager> Logger;
        private readonly AppSettings AppSettings;

        public AgentFileManager(PersistentAgentsClient client, ILogger<AgentFileManager> logger, AppSettings appSettings)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(appSettings);
            
            Client = client;
            Logger = logger;
            AppSettings = appSettings;
        }

        public async Task<(List<string> UploadedFileIds, string VectorStoreId)> UploadFilesAsync(Dictionary<string, string> files, CancellationToken cancellationToken)
        {
            var uploadTasks = files
                .Where(kvp => kvp.Key.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase))
                .Select(file => UploadSingleFileAsync(file.Key, file.Value, cancellationToken));

            var results = await Task.WhenAll(uploadTasks);
            
            var uploadedFileIds = results
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => id!)
                .ToList();

            await WaitForIndexingAsync(uploadedFileIds, cancellationToken);

            var vectorStoreId = await CreateVectorStoreAsync(uploadedFileIds, cancellationToken);

            Logger.LogInformation("Successfully uploaded {Count} TypeSpec files.", uploadedFileIds.Count);
            return (uploadedFileIds, vectorStoreId);
        }

        protected virtual async Task<string?> UploadSingleFileAsync(string fileName, string content, CancellationToken cancellationToken)
        {
            try
            {
                var txtFileName = Path.ChangeExtension(fileName, "txt");

                using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(content));

                var uploaded = await Client.Files.UploadFileAsync(
                    contentStream,
                    PersistentAgentFilePurpose.Agents,
                    txtFileName,
                    cancellationToken: cancellationToken
                ).ConfigureAwait(false);

                if (uploaded?.Value?.Id != null)
                {
                    Logger.LogDebug("Uploaded file: {FileName} -> {FileId}", fileName, uploaded.Value.Id);
                    return uploaded.Value.Id;
                }
                else
                {
                    Logger.LogWarning("Failed to upload file: {FileName}", fileName);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error uploading file: {FileName}", fileName);
                return null;
            }
        }

        protected virtual async Task WaitForIndexingAsync(List<string> uploadedFilesIds, CancellationToken ct)
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

        private async Task<string> CreateVectorStoreAsync(List<string> fileIds, CancellationToken ct)
        {
            return await CreateVectorStoreInternalAsync(fileIds, ct).ConfigureAwait(false);
        }

        protected virtual async Task<string> CreateVectorStoreInternalAsync(List<string> fileIds, CancellationToken ct)
        {
            var storeName = $"azc-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid()}";

            Logger.LogInformation("Creating vector store '{StoreName}' with {Count} files...", storeName, fileIds.Count);

            var store = await Client.VectorStores.CreateVectorStoreAsync(
                fileIds,
                name: storeName,
                cancellationToken: ct
            ).ConfigureAwait(false);

            Logger.LogInformation("Created vector store: {Name} ({Id})", store.Value.Name, store.Value.Id);

            Logger.LogDebug("Waiting {WaitTime}ms for vector store to be ready...", AppSettings.VectorStoreReadyWaitTime);
            await Task.Delay(AppSettings.VectorStoreReadyWaitTime, ct).ConfigureAwait(false);

            return store.Value.Id;
        }
    }
}
