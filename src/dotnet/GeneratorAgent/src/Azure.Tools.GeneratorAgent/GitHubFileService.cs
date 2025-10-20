using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// TypeSpec file service for GitHub repository access using GitHub API for fast file retrieval.
    /// </summary>
    internal class GitHubFileService
    {
        private readonly ILogger<GitHubFileService> Logger;
        private readonly AppSettings AppSettings;
        private readonly HttpClient HttpClient;

        // Static JSON options for better performance (reused across calls)
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public GitHubFileService(
            AppSettings appSettings,
            ILogger<GitHubFileService> logger,
            HttpClient httpClient)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(httpClient);

            AppSettings = appSettings;
            Logger = logger;
            HttpClient = httpClient;
            
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                bool hasAuthHeader = httpClient.DefaultRequestHeaders.Authorization != null;
                Logger.LogDebug("GitHub API client initialized. Authentication: {AuthStatus}", 
                    hasAuthHeader ? "Configured" : "Not configured - using rate-limited access");
            }
        }

        public async Task<Dictionary<string, string>> GetTypeSpecFilesAsync(string commitId, string typeSpecDir, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(commitId);
            ArgumentException.ThrowIfNullOrWhiteSpace(typeSpecDir);
            
            // GitHub API endpoint for directory contents
            string apiUrl = $"https://api.github.com/repos/{AppSettings.AzureSpecRepository}/contents/{typeSpecDir}?ref={commitId}";

            Logger.LogDebug("Fetching TypeSpec files from GitHub API: {ApiUrl}", apiUrl);

            using HttpResponseMessage response = await HttpClient.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                string errorMessage = $"GitHub API request failed: {response.StatusCode} {response.ReasonPhrase} for {apiUrl}";
                Logger.LogError(errorMessage);
                throw new HttpRequestException(errorMessage);
            }

            string jsonContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            
            GitHubContent[]? contents;
            try
            {
                contents = JsonSerializer.Deserialize<GitHubContent[]>(jsonContent, JsonOptions);
            }
            catch (JsonException ex)
            {
                string errorMessage = $"Failed to deserialize GitHub API response for '{AppSettings.AzureSpecRepository}/{typeSpecDir}' at commit '{commitId}'. Please verify the repository path and commit ID are correct.";
                Logger.LogError(ex, errorMessage);
                throw new InvalidOperationException(errorMessage, ex);
            }

            if (contents == null)
            {
                string errorMessage = $"GitHub API returned null content for '{AppSettings.AzureSpecRepository}/{typeSpecDir}' at commit '{commitId}'";
                Logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            Dictionary<string, string> typeSpecFiles = new(contents.Length);

            // Get all files 
            IEnumerable<GitHubContent> allFiles = contents
                .Where(c => string.Equals(c.Type, "file", StringComparison.Ordinal) &&
                        !string.IsNullOrEmpty(c.DownloadUrl));

            Logger.LogDebug("Found {FileCount} files to download", allFiles.Count());

            // Download files
            foreach (GitHubContent file in allFiles)
            {
                var (fileName, content) = await DownloadFileContentAsync(file.Name, file.DownloadUrl, cancellationToken).ConfigureAwait(false);
                typeSpecFiles[fileName] = content;
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Downloaded file: {FileName} ({Size} characters)", fileName, content.Length);
                }
            }

            return typeSpecFiles;
        }

        private async Task<(string FileName, string Content)> DownloadFileContentAsync(string fileName, string? downloadUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(downloadUrl))
            {
                throw new InvalidOperationException($"Download URL is null or empty for file {fileName}");
            }

            using HttpResponseMessage response = await HttpClient.GetAsync(downloadUrl, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to download file {fileName}: {response.StatusCode} {response.ReasonPhrase}");
            }

            string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (fileName, content);
        }
    }
}
