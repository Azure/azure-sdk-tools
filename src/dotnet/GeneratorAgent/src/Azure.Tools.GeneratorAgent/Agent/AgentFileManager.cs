using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Azure.Tools.GeneratorAgent.Agent
{
    internal class AgentFileManager
    {
        private const string UnknownStatus = "Unknown";
        private const string ProcessedStatus = "Processed";

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

        public async Task<string> UploadFilesAsync(Dictionary<string, string> files, CancellationToken cancellationToken)
        {
            var uploadTasks = files
                .Where(kvp => kvp.Key.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase))
                .Select(file => UploadSingleFileAsync(file.Key, file.Value, cancellationToken));

            var results = await Task.WhenAll(uploadTasks).ConfigureAwait(false);

            var failures = results.Where(r => r.IsFailure).ToList();
            if (failures.Any())
            {
                var firstFailure = failures.First();
                throw new InvalidOperationException($"Failed to upload files: {firstFailure.Exception?.Message}", firstFailure.Exception);
            }

            var uploadedFileIds = results.Select(result => result.Value!);

            await WaitForIndexingAsync(uploadedFileIds, cancellationToken).ConfigureAwait(false);

            var vectorStoreId = await CreateVectorStoreAsync(uploadedFileIds, cancellationToken).ConfigureAwait(false);

            return vectorStoreId;
        }

        protected virtual async Task<Result<string>> UploadSingleFileAsync(string fileName, string content, CancellationToken cancellationToken)
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
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Uploaded file: {FileName} -> {FileId}", fileName, uploaded.Value.Id);
                }
                return Result<string>.Success(uploaded.Value.Id);
            }
            else
            {
                var errorMsg = $"Failed to upload file: {fileName}";
                Logger.LogWarning(errorMsg);
                return Result<string>.Failure(new InvalidOperationException(errorMsg));
            }
        }

        protected virtual async Task WaitForIndexingAsync(IEnumerable<string> uploadedFilesIds, CancellationToken ct)
        {
            var maxWaitTime = AppSettings.IndexingMaxWaitTime;
            var pollingInterval = AppSettings.IndexingPollingInterval;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Logger.LogDebug("Waiting for {Count} files to be indexed... (timeout: {Timeout}s)",
                uploadedFilesIds.Count(), maxWaitTime.TotalSeconds);

            const int batchSize = 10;
            while (stopwatch.Elapsed < maxWaitTime)
            {
                var results = new List<(string FileId, PersistentAgentFileInfo? File)>();
                var totalCount = 0;

                var batch = new List<string>(batchSize);
                foreach (var fileId in uploadedFilesIds)
                {
                    batch.Add(fileId);
                    totalCount++;

                    if (batch.Count == batchSize)
                    {
                        var checkTasks = batch.Select(id => CheckFileStatusAsync(id, ct));
                        var batchResults = await Task.WhenAll(checkTasks).ConfigureAwait(false);
                        results.AddRange(batchResults);
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                {
                    var checkTasks = batch.Select(id => CheckFileStatusAsync(id, ct));
                    var batchResults = await Task.WhenAll(checkTasks).ConfigureAwait(false);
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
                            currentStatusCounts[UnknownStatus] = currentStatusCounts.GetValueOrDefault(UnknownStatus) + 1;
                        }
                    }
                    else
                    {
                        var status = result.File?.Status.ToString() ?? UnknownStatus;
                        if (currentStatusCounts != null)
                        {
                            currentStatusCounts[status] = currentStatusCounts.GetValueOrDefault(status) + 1;
                        }

                        if (!status.Equals(ProcessedStatus, StringComparison.OrdinalIgnoreCase))
                        {
                            allIndexed = false;
                            if (result.File != null)
                            {
                                pendingFiles?.Add($"{result.File.Filename}({status})");
                            }
                        }
                    }
                }

                if (Logger.IsEnabled(LogLevel.Debug) && currentStatusCounts != null)
                {
                    var statusSummary = string.Join(", ", currentStatusCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                    Logger.LogDebug("Indexing status: {StatusSummary} | Elapsed: {Elapsed:F1}s",
                        statusSummary, stopwatch.Elapsed.TotalSeconds);
                }

                if (allIndexed)
                {
                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug("All {Count} files indexed successfully in {Duration:F1}s",
                            totalCount, stopwatch.Elapsed.TotalSeconds);
                    }
                    return;
                }

                if (Logger.IsEnabled(LogLevel.Debug) && pendingFiles?.Count <= 3)
                {
                    Logger.LogDebug("Still waiting for: {PendingFiles}", string.Join(", ", pendingFiles));
                }

                await Task.Delay(pollingInterval, ct).ConfigureAwait(false);
            }

            var finalResults = await Task.WhenAll(uploadedFilesIds.Select(id => CheckFileStatusAsync(id, ct))).ConfigureAwait(false);

            var allProcessed = !finalResults.Any(r => r.File?.Status.ToString()?.Equals(ProcessedStatus, StringComparison.OrdinalIgnoreCase) != true);
            if (allProcessed)
            {

                Logger.LogDebug("All files indexed successfully in {Duration:F1}s (completed during final check)",
                    stopwatch.Elapsed.TotalSeconds);
                return;
            }

            var finalSummary = finalResults
                .Where(r => r.File != null)
                .GroupBy(r => r.File!.Status.ToString() ?? UnknownStatus)
                .ToDictionary(g => g.Key, g => g.Count());

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
            catch (Exception ex) when (!(ex is Azure.RequestFailedException))
            {
                Logger.LogWarning(ex, "Unexpected error checking status for file: {FileId}", fileId);
                return (fileId, null);
            }
        }

        protected virtual async Task<string> CreateVectorStoreAsync(IEnumerable<string> fileIds, CancellationToken ct)
        {
            var fileIdsList = fileIds.ToList();
            var storeName = $"azc-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid()}";

            Logger.LogDebug("Creating vector store '{StoreName}' with {Count} files...", storeName, fileIdsList.Count);

            var store = await Client.VectorStores.CreateVectorStoreAsync(
                fileIdsList,
                name: storeName,
                cancellationToken: ct
            ).ConfigureAwait(false);

            Logger.LogDebug("Vector store created with ID: {VectorStoreId}", store.Value.Id);

            await Task.Delay(AppSettings.VectorStoreReadyWaitTime, ct).ConfigureAwait(false);

            return store.Value.Id;
        }

        public virtual async Task UpdateFileInVectorStoreAsync(string vectorStoreId, string fileName, string content, CancellationToken cancellationToken = default)
        {
            var uploadResult = await UploadSingleFileAsync(fileName, content, cancellationToken).ConfigureAwait(false);
            if (uploadResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to upload updated file: {fileName}", uploadResult.Exception);
            }

            var newFileId = uploadResult.Value!;

            // Step 1: Find and remove the old version from vector store
            await foreach (var existingFile in Client.VectorStores.GetVectorStoreFilesAsync(vectorStoreId, cancellationToken: cancellationToken))
            {
                // Get file details to check the name
                var fileDetails = await Client.Files.GetFileAsync(existingFile.Id, cancellationToken).ConfigureAwait(false);

                // Compare with the expected filename (with .txt extension)
                var expectedFileName = Path.ChangeExtension(fileName, "txt");

                if (fileDetails.Value.Filename == expectedFileName)
                {
                    // Remove old file from vector store
                    await Client.VectorStores.DeleteVectorStoreFileAsync(vectorStoreId, existingFile.Id, cancellationToken).ConfigureAwait(false);
                    Logger.LogDebug("Removed old version of {FileName} from vector store", fileName);

                    // Delete the old file
                    await Client.Files.DeleteFileAsync(existingFile.Id, cancellationToken).ConfigureAwait(false);
                    Logger.LogDebug("Deleted old file with ID: {FileId}", existingFile.Id);
                    break;
                }
            }
            // Step 2: Add the new file to vector store
            await Client.VectorStores.CreateVectorStoreFileAsync(
                vectorStoreId: vectorStoreId,
                fileId: newFileId,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Step 3: Wait for the new file to be indexed
            await WaitForIndexingAsync(new[] { newFileId }, cancellationToken).ConfigureAwait(false);
            
            Logger.LogDebug("Successfully updated {FileName} in vector store {VectorStoreId}", fileName, vectorStoreId);
        }
    }
}
