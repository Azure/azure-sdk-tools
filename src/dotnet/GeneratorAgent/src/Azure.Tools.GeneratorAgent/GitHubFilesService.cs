using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// TypeSpec file service for GitHub repository access using GitHub API for fast file retrieval.
    /// </summary>
    internal class GitHubFilesService
    {
        private readonly ILogger<GitHubFilesService> Logger;
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

        public GitHubFilesService(
            AppSettings appSettings,
            ILogger<GitHubFilesService> logger,
            ValidationContext validationContext,
            HttpClient httpClient)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(validationContext);
            ArgumentNullException.ThrowIfNull(httpClient);

            AppSettings = appSettings;
            Logger = logger;

            CommitId = validationContext.ValidatedCommitId;
            TypespecSpecDir = validationContext.ValidatedTypeSpecDir;

            HttpClient = httpClient;
            
            // Log GitHub authentication status
            bool hasAuthHeader = httpClient.DefaultRequestHeaders.Authorization != null;
            Logger.LogInformation("GitHub API client initialized. Authentication: {AuthStatus}", 
                hasAuthHeader ? "Configured" : "Not configured - using rate-limited access");
        }

        public async Task<Result<Dictionary<string, string>>> GetTypeSpecFilesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Result<Dictionary<string, string>> apiResult = await FetchTypeSpecFilesViaApiAsync(cancellationToken).ConfigureAwait(false);
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
                    string errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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

                IEnumerable<GitHubContent> tspFiles = contents
                    .Where(c => string.Equals(c.Type, "file", StringComparison.Ordinal) && 
                               c.Name.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase) &&
                               !string.IsNullOrEmpty(c.DownloadUrl));

                IEnumerable<Task<(string FileName, string Content)>> downloadTasks = tspFiles
                    .Select(tspFile => DownloadFileContentAsync(tspFile.Name, tspFile.DownloadUrl, cancellationToken));

                if (!downloadTasks.Any())
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
                    else
                    {
                        Logger.LogWarning("Failed to download content for file: {FileName}", fileName);
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

        private async Task<(string FileName, string Content)> DownloadFileContentAsync(string fileName, string? downloadUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(downloadUrl))
            {
                Logger.LogWarning("Download URL is null or empty for file {FileName}", fileName);
                return (fileName, string.Empty);
            }

            try
            {
                HttpResponseMessage response = await HttpClient.GetAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    return (fileName, content);
                }

                Logger.LogWarning("Failed to download file {FileName} from {Url}: {StatusCode} {ReasonPhrase}", 
                    fileName, downloadUrl, response.StatusCode, response.ReasonPhrase);
                return (fileName, string.Empty);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogCritical(ex, "Error downloading file {FileName} from {Url}", fileName, downloadUrl);
                return (fileName, string.Empty);
            }
        }
    }
}
