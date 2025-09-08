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
        private readonly string CommitId;
        private readonly string TypespecSpecDir;

        // Static JSON options for better performance (reused across calls)
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public GitHubFileService(
            AppSettings appSettings,
            ILogger<GitHubFileService> logger,
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

        public async Task<Dictionary<string, string>> GetTypeSpecFilesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // GitHub API endpoint for directory contents
                string apiUrl = $"https://api.github.com/repos/{AppSettings.AzureSpecRepository}/contents/{TypespecSpecDir}?ref={CommitId}";

                HttpResponseMessage response = await HttpClient.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);
                
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogCritical("GitHub API request failed: {StatusCode} {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                    throw new HttpRequestException($"GitHub API request failed: {response.StatusCode} {response.ReasonPhrase}");
                }

                string jsonContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                GitHubContent[]? contents = JsonSerializer.Deserialize<GitHubContent[]>(jsonContent, JsonOptions);

                if (contents == null)
                {
                    Logger.LogCritical("Failed to deserialize GitHub API response from {ApiUrl}. " +
                        "Response content: {JsonContent}", apiUrl, jsonContent);
                    throw new InvalidOperationException($"Failed to deserialize GitHub API response for " +
                        $"'{AppSettings.AzureSpecRepository}/{TypespecSpecDir}' at commit '{CommitId}'. " +
                        $"Please verify the repository path and commit ID are correct.");
                }

                Dictionary<string, string> typeSpecFiles = new(contents.Length);

                // Get all files 
                IEnumerable<GitHubContent> allFiles = contents
                    .Where(c => string.Equals(c.Type, "file", StringComparison.Ordinal) &&
                            !string.IsNullOrEmpty(c.DownloadUrl));

                IEnumerable<Task<(string FileName, string Content)>> downloadTasks = allFiles
                    .Select(file => DownloadFileContentAsync(file.Name, file.DownloadUrl, cancellationToken));

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

                return typeSpecFiles;
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
                throw new InvalidOperationException($"Download URL is null or empty for file {fileName}");
            }

            HttpResponseMessage response = await HttpClient.GetAsync(downloadUrl, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to download file {fileName}: {response.StatusCode} {response.ReasonPhrase}");
            }

            string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (fileName, content);
        }
    }
}
