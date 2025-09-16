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
            
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                bool hasAuthHeader = httpClient.DefaultRequestHeaders.Authorization != null;
                Logger.LogDebug("GitHub API client initialized. Authentication: {AuthStatus}", 
                    hasAuthHeader ? "Configured" : "Not configured - using rate-limited access");
            }
        }

        public async Task<Result<Dictionary<string, string>>> GetTypeSpecFilesAsync(CancellationToken cancellationToken = default)
        {
            // GitHub API endpoint for directory contents
            string apiUrl = $"https://api.github.com/repos/{AppSettings.AzureSpecRepository}/contents/{TypespecSpecDir}?ref={CommitId}";

            try
            {
                HttpResponseMessage response = await HttpClient.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = Logger.IsEnabled(LogLevel.Error)
                        ? $"GitHub API request failed: {response.StatusCode} {response.ReasonPhrase} for {apiUrl}"
                        : "GitHub API request failed";
                    return Result<Dictionary<string, string>>.Failure(new HttpRequestException(errorMessage));
                }

                string jsonContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                GitHubContent[]? contents = JsonSerializer.Deserialize<GitHubContent[]>(jsonContent, JsonOptions);

                if (contents == null)
                {
                    string errorMessage = Logger.IsEnabled(LogLevel.Error)
                        ? $"Failed to deserialize GitHub API response for '{AppSettings.AzureSpecRepository}/{TypespecSpecDir}' at commit '{CommitId}'. Please verify the repository path and commit ID are correct."
                        : "Failed to deserialize GitHub API response";
                    return Result<Dictionary<string, string>>.Failure(new InvalidOperationException(errorMessage));
                }

                Dictionary<string, string> typeSpecFiles = new(contents.Length);

                // Get all files 
                IEnumerable<GitHubContent> allFiles = contents
                    .Where(c => string.Equals(c.Type, "file", StringComparison.Ordinal) &&
                            !string.IsNullOrEmpty(c.DownloadUrl));

                // Download files
                foreach (GitHubContent file in allFiles)
                {
                    var downloadResult = await DownloadFileContentAsync(file.Name, file.DownloadUrl, cancellationToken).ConfigureAwait(false);
                    if (downloadResult.IsSuccess)
                    {
                        typeSpecFiles[downloadResult.Value.FileName] = downloadResult.Value.Content;
                        if (Logger.IsEnabled(LogLevel.Debug))
                        {
                            Logger.LogDebug("Downloaded file: {FileName} ({Size} characters)", 
                                downloadResult.Value.FileName, 
                                downloadResult.Value.Content.Length);
                        }
                    }
                    else
                    {
                        return Result<Dictionary<string, string>>.Failure(downloadResult.Exception ?? new InvalidOperationException($"Failed to download file {file.Name}"));
                    }
                }

                return Result<Dictionary<string, string>>.Success(typeSpecFiles);
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Result<Dictionary<string, string>>.Failure(new InvalidOperationException($"Unexpected error during GitHub API operation: {ex.Message}", ex));
            }
        }

        private async Task<Result<(string FileName, string Content)>> DownloadFileContentAsync(string fileName, string? downloadUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(downloadUrl))
            {
                string errorMsg = $"Download URL is null or empty for file {fileName}";
                return Result<(string, string)>.Failure(new InvalidOperationException(errorMsg));
            }

            try
            {
                HttpResponseMessage response = await HttpClient.GetAsync(downloadUrl, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = Logger.IsEnabled(LogLevel.Error) 
                        ? $"Failed to download file {fileName}: {response.StatusCode} {response.ReasonPhrase}"
                        : "Failed to download file";
                    return Result<(string, string)>.Failure(new HttpRequestException(errorMsg));
                }

                string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return Result<(string, string)>.Success((fileName, content));
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Result<(string, string)>.Failure(new InvalidOperationException($"Exception downloading file {fileName}: {ex.Message}", ex));
            }
        }
    }
}
