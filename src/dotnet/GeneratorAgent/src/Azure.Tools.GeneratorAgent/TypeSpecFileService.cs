using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Service responsible for retrieving TypeSpec files from various sources (local or GitHub).
    /// </summary>
    internal class TypeSpecFileService
    {
        private readonly AppSettings AppSettings;
        private readonly ILogger<TypeSpecFileService> Logger;
        private readonly ILoggerFactory LoggerFactory;
        private readonly HttpClient HttpClient;
        private readonly ValidationContext ValidationContext;
        private readonly Func<ValidationContext, GitHubFilesService> GitHubServiceFactory;
        private GitHubFilesService? GitHubService;

        public TypeSpecFileService(
            AppSettings appSettings,
            ILogger<TypeSpecFileService> logger,
            ILoggerFactory loggerFactory,
            HttpClient httpClient,
            ValidationContext validationContext,
            Func<ValidationContext, GitHubFilesService> gitHubServiceFactory)
        {
            ArgumentNullException.ThrowIfNull(appSettings);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(validationContext);
            ArgumentNullException.ThrowIfNull(gitHubServiceFactory);

            AppSettings = appSettings;
            Logger = logger;
            LoggerFactory = loggerFactory;
            HttpClient = httpClient;
            ValidationContext = validationContext;
            GitHubServiceFactory = gitHubServiceFactory;
        }

        /// <summary>
        /// Gets TypeSpec files from either local directory or GitHub repository.
        /// Returns a dictionary where key is filename and value is file content.
        /// </summary>
        public async Task<Result<Dictionary<string, string>>> GetTypeSpecFilesAsync(
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ValidationContext.ValidatedCommitId))
            {
                // Local case
                return await GetLocalTypeSpecFilesAsync(ValidationContext.ValidatedTypeSpecDir, cancellationToken);
            }
            else
            {
                // GitHub case
                return await GetGitHubTypeSpecFilesAsync(cancellationToken);
            }
        }

        private async Task<Result<Dictionary<string, string>>> GetLocalTypeSpecFilesAsync(
            string typeSpecDir, 
            CancellationToken cancellationToken)
        {
            Logger.LogInformation("Reading TypeSpec files from local directory: {TypeSpecDir}", typeSpecDir);

            try
            {
                Dictionary<string, string> typeSpecFiles = new Dictionary<string, string>();
                
                string[] allFiles = Directory.GetFiles(typeSpecDir, "*.tsp", SearchOption.AllDirectories);

                foreach (string filePath in allFiles)
                {
                    string fileName = Path.GetFileName(filePath);
                    string content = await File.ReadAllTextAsync(filePath, cancellationToken);
                    typeSpecFiles[fileName] = content;
                    
                }

                Logger.LogInformation("Successfully read {Count} TypeSpec files from local directory", typeSpecFiles.Count);
                return Result<Dictionary<string, string>>.Success(typeSpecFiles);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogCritical(ex, "Error reading TypeSpec files from local directory: {TypeSpecDir}", typeSpecDir);
                throw;
            }
        }

        private async Task<Result<Dictionary<string, string>>> GetGitHubTypeSpecFilesAsync(
            CancellationToken cancellationToken)
        {
            Logger.LogInformation("Fetching TypeSpec files from GitHub: {TypeSpecDir} at commit {CommitId}", 
                ValidationContext.ValidatedTypeSpecDir, ValidationContext.ValidatedCommitId);

            try
            {
                GitHubService = GitHubServiceFactory(ValidationContext);

                Result<Dictionary<string, string>> result = await GitHubService.GetTypeSpecFilesAsync(cancellationToken);

                if (result.IsSuccess)
                {
                    Logger.LogInformation("Successfully fetched {Count} TypeSpec files from GitHub", result.Value!.Count);
                }

                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogCritical(ex, "Unexpected error fetching TypeSpec files from GitHub: {TypeSpecDir} at commit {CommitId}", 
                    ValidationContext.ValidatedTypeSpecDir, ValidationContext.ValidatedCommitId);
                throw;
            }
        }
    }
}
