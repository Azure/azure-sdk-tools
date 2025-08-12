using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// TypeSpec file service for GitHub repository access using GitHub API for fast file retrieval.
    /// </summary>
    internal class GitHubFilesRetreiveService : IDisposable
    {
        private readonly ILogger<GitHubFilesRetreiveService> Logger;
        private readonly AppSettings AppSettings;
        private readonly HttpClient HttpClient;
        private readonly string CommitId;
        private readonly string TypespecSpecDir;

        // Static JSON options for better performance (reused across calls)
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public GitHubFilesRetreiveService(
            AppSettings appSettings,
            ILogger<GitHubFilesRetreiveService> logger,
            ValidationContext validationContext)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(validationContext);

            AppSettings = appSettings;
            Logger = logger;

            CommitId = validationContext.ValidatedCommitId;
            TypespecSpecDir = validationContext.ValidatedTypeSpecDir;

            // Initialize HttpClient with User-Agent header and timeout for GitHub API
            HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(2) // Reasonable timeout for file downloads
            };
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "AzureSDK-TypeSpecGenerator/1.0");
        }

        public async Task<Result<Dictionary<string, string>>> GetTypeSpecFilesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Result<Dictionary<string, string>> apiResult = await FetchTypeSpecFilesViaApiAsync(cancellationToken);
                return apiResult;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogCritical(ex, "Unexpected error while fetching TypeSpec files from GitHub repository {Repository}", 
                    AppSettings.AzureSpecRepository);
                throw;
            }
        }

        private async Task<Result<Dictionary<string, string>>> FetchTypeSpecFilesViaApiAsync(CancellationToken cancellationToken)
        {
            try
            {
                // GitHub API endpoint for directory contents
                string apiUrl = $"https://api.github.com/repos/{AppSettings.AzureSpecRepository}/contents/{TypespecSpecDir}?ref={CommitId}";

                HttpResponseMessage response = await HttpClient.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("GitHub API request failed: {StatusCode} {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                    throw new HttpRequestException($"GitHub API request failed: {response.StatusCode} {response.ReasonPhrase}");
                }

                string jsonContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                GitHubContent[]? contents = JsonSerializer.Deserialize<GitHubContent[]>(jsonContent, JsonOptions);

                if (contents == null)
                {
                    throw new InvalidOperationException("Failed to deserialize GitHub API response");
                }

                Dictionary<string, string> typeSpecFiles = new(contents.Length);

                IEnumerable<GitHubContent> tspFiles = contents.Where(c => 
                    string.Equals(c.Type, "file", StringComparison.Ordinal) && 
                    c.Name.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase));

                // Download each .tsp file content in parallel
                Task<(string FileName, string Content)>[] downloadTasks = tspFiles
                    .Where(tspFile => !string.IsNullOrEmpty(tspFile.DownloadUrl))
                    .Select(tspFile => DownloadFileContentAsync(tspFile.Name, tspFile.DownloadUrl!, cancellationToken))
                    .ToArray();

                if (downloadTasks.Length == 0)
                {
                    Logger.LogWarning("No valid .tsp files found with download URLs");
                    throw new InvalidOperationException("No valid .tsp files found with download URLs");
                }

                (string FileName, string Content)[] downloadedFiles = await Task.WhenAll(downloadTasks).ConfigureAwait(false);

                foreach ((string fileName, string content) in downloadedFiles)
                {
                    if (!string.IsNullOrEmpty(content))
                    {
                        typeSpecFiles[fileName] = content;
                        Logger.LogDebug("Downloaded file: {FileName} ({Size} characters)", fileName, content.Length);
                    }
                }

                return Result<Dictionary<string, string>>.Success(typeSpecFiles);
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Error in GitHub API TypeSpec files fetch");
                throw;
            }
        }

        private async Task<(string FileName, string Content)> DownloadFileContentAsync(string fileName, string downloadUrl, CancellationToken cancellationToken)
        {
            try
            {
                HttpResponseMessage response = await HttpClient.GetAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    return (fileName, content);
                }

                Logger.LogWarning("Failed to download file {FileName}: {StatusCode} {ReasonPhrase}", 
                    fileName, response.StatusCode, response.ReasonPhrase);
                return (fileName, string.Empty);
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Error downloading file {FileName}", fileName);
                return (fileName, string.Empty);
            }
        }

        public void Dispose()
        {
            HttpClient?.Dispose();
        }
    }
}
