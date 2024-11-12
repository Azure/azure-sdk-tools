using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using IssueLabeler.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using System.ComponentModel;

namespace IssueLabeler.Shared
{
    public interface IModelHolder
    {
        bool IsPrEngineLoaded { get; }
        bool LoadRequested { get; }
        bool IsIssueEngineLoaded { get; }
        PredictionEngine<GitHubIssue, GitHubIssuePrediction> IssuePredEngine { get; }
        PredictionEngine<GitHubPullRequest, GitHubIssuePrediction> PrPredEngine { get; }
        Task LoadEnginesAsync();
        bool UseIssuesForPrsToo { get; }
    }

    // make singleton => bg service and the controller can access.....
    // IModelHolder.... holds the prediction engin.... -> is it loaded yet? then if so return suggestion
    public class ModelHolder : IModelHolder
    {
        private readonly string _prModelBlobName;
        private readonly string _issueModelBlobName;
        private readonly ILogger _logger;
        private int _loadRequested;
        private int timesIssueDownloaded = 0;
        private int timesPrDownloaded = 0;
        private Uri _blobContainerUri;
        public bool LoadRequested => _loadRequested != 0;
        public bool IsPrEngineLoaded => (PrPredEngine != null);
        public bool IsIssueEngineLoaded => (IssuePredEngine != null);
        public bool UseIssuesForPrsToo { get; private set; }
        public PredictionEngine<GitHubIssue, GitHubIssuePrediction> IssuePredEngine { get; private set; } = null;
        public PredictionEngine<GitHubPullRequest, GitHubIssuePrediction> PrPredEngine { get; private set; } = null;

        public ModelHolder(ILogger logger, IConfiguration configuration, string repo, string modelBlobConfigName = null)
        {
            // TODO: imagine there is an array of model holders, prefixes itself with owner/repo info.
            modelBlobConfigName = modelBlobConfigName ?? "BlobName";
            _logger = logger;
            _blobContainerUri = new Uri(new Uri(configuration["BlobAccountUri"]), configuration["BlobContainerName"]);

            // the following four configuration values are per repo values.
            var configSection = $"IssueModel.{repo.Replace("-", "_")}.{modelBlobConfigName}";
            if (string.IsNullOrEmpty(configuration[configSection]))
            {
                throw new ArgumentNullException($"repo: {repo}, missing config..");
            }
            _issueModelBlobName = configuration[configSection];

            configSection = $"PrModel:{repo}:{modelBlobConfigName}";
            if (!string.IsNullOrEmpty(configuration[configSection]))
            {
                // has both pr and issue config - allowed
                _prModelBlobName = configuration[configSection];
            }
            else
            {
                // has issue config only - allowed
                UseIssuesForPrsToo = true;

                configSection = $"PrModel:{repo}:{modelBlobConfigName}";

                if (!string.IsNullOrEmpty(configuration[configSection]))
                {
                    throw new ArgumentNullException($"repo: {repo}, missing config....");
                }
            }
            _loadRequested = 0;
        }

        public async Task LoadEnginesAsync()
        {
            _logger.LogInformation($"! {nameof(LoadEnginesAsync)} called.");
            Interlocked.Increment(ref _loadRequested);
            if (IsIssueEngineLoaded)
            {
                _logger.LogInformation($"! engines were already loaded.");
                return;
            }
            if (!IsIssueEngineLoaded)
            {
                _logger.LogInformation($"! loading {nameof(IssuePredEngine)}.");
                MLContext mlContext = new MLContext();
                BlobContainerClient container = new BlobContainerClient(_blobContainerUri, new DefaultAzureCredential());
                var condition = new BlobRequestConditions();
                var blockBlob = container.GetBlobClient(_issueModelBlobName);
                _logger.LogInformation($"Loading model from {_issueModelBlobName} from container {container.Uri}");
                using (var stream = new MemoryStream())
                {
                    stream.Position = 0;
                    await blockBlob.DownloadToAsync(stream, condition, new StorageTransferOptions() { });
                    _logger.LogInformation($"downloaded ml model");
                    var mlModel = mlContext.Model.Load(stream, out DataViewSchema _);
                    IssuePredEngine = mlContext.Model.CreatePredictionEngine<GitHubIssue, GitHubIssuePrediction>(mlModel);
                    PrPredEngine = mlContext.Model.CreatePredictionEngine<GitHubPullRequest, GitHubIssuePrediction>(mlModel);
                }
                _logger.LogInformation($"! {nameof(IssuePredEngine)} loaded.");
            }
        }
    }
}