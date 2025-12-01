// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
namespace IssueLabeler.Shared;

using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML;

public interface IModelHolder
{
    bool IsPrEngineLoaded { get; }
    bool LoadRequested { get; }
    bool IsIssueEngineLoaded { get; }
    PredictionEngine<GitHubIssue, GitHubIssuePrediction> IssuePredEngine { get; }
    PredictionEngine<GitHubPullRequest, GitHubIssuePrediction> PrPredEngine { get; }
    Task LoadEnginesAsync();
    bool UseIssuesForPrsToo { get; }

    string modelType { get; }
}

// make singleton => bg service and the controller can access.....
// IModelHolder.... holds the prediction engin.... -> is it loaded yet? then if so return suggestion
public class ModelHolder : IModelHolder
{
    private readonly string _prModelBlobName;
    private readonly string _issueModelBlobName;
    private readonly ILogger _logger;
    private RepositoryConfiguration _config;
    private int _loadRequested;
    private Uri _blobContainerUri;
    public string modelType { get; private set; }
    public bool LoadRequested => _loadRequested != 0;
    public bool IsPrEngineLoaded => (PrPredEngine != null);
    public bool IsIssueEngineLoaded => (IssuePredEngine != null);
    public bool UseIssuesForPrsToo { get; private set; }
    public PredictionEngine<GitHubIssue, GitHubIssuePrediction> IssuePredEngine { get; private set; } = null;
    public PredictionEngine<GitHubPullRequest, GitHubIssuePrediction> PrPredEngine { get; private set; } = null;
    public ModelHolder(ILogger logger, RepositoryConfiguration configuration, string repo, string labelType)
    {
        _logger = logger;
        _blobContainerUri = new Uri(new Uri(configuration.BlobAccountUri), configuration.BlobContainerName);
        _config = configuration;
        modelType = labelType;
        

        // the following four configuration values are per repo values.
        var issueConfig = labelType == LabelType.Category ? _config.IssueModelForCategoryLabels : _config.IssueModelForServiceLabels;
        if (string.IsNullOrEmpty(issueConfig))
        {
            throw new ArgumentNullException($"repo: {repo}, missing config..");
        }
        _issueModelBlobName = issueConfig;
        var prConfig = labelType == LabelType.Category ? _config.PrModelForCategoryLabels : _config.PrModelForServiceLabels;
        if (!string.IsNullOrEmpty(prConfig))
        {
            // has both pr and issue config - allowed
            _prModelBlobName = prConfig;
        }
        else
        {
            // has issue config only - allowed
            UseIssuesForPrsToo = true;
            _prModelBlobName = string.Empty; // Set default value
        }
        _loadRequested = 0;
    }

    private async Task<ITransformer> LoadModelFromBlobAsync(MLContext mlContext, BlobContainerClient container, string blobName)
    {
        var condition = new BlobRequestConditions();
        var blockBlob = container.GetBlobClient(blobName);
        _logger.LogInformation($"Loading model from {blobName} from container {container.Uri}");
        using (var stream = new MemoryStream())
        {
            stream.Position = 0;
            await blockBlob.DownloadToAsync(stream, condition, new StorageTransferOptions() { });
            stream.Position = 0;
            _logger.LogInformation($"downloaded ml model");
            return mlContext.Model.Load(stream, out DataViewSchema _);
        }
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
            
            var mlModel = await LoadModelFromBlobAsync(mlContext, container, _issueModelBlobName);
            IssuePredEngine = mlContext.Model.CreatePredictionEngine<GitHubIssue, GitHubIssuePrediction>(mlModel);
            if (UseIssuesForPrsToo)
            {
                PrPredEngine = mlContext.Model.CreatePredictionEngine<GitHubPullRequest, GitHubIssuePrediction>(mlModel);
            }
            _logger.LogInformation($"! {nameof(IssuePredEngine)} loaded.");

            if (!IsPrEngineLoaded && !UseIssuesForPrsToo && _prModelBlobName != string.Empty)
            { 
                _logger.LogInformation($"! loading {nameof(PrPredEngine)}.");
                var prModel = await LoadModelFromBlobAsync(mlContext, container, _prModelBlobName);
                PrPredEngine = mlContext.Model.CreatePredictionEngine<GitHubPullRequest, GitHubIssuePrediction>(prModel);
                _logger.LogInformation($"! {nameof(PrPredEngine)} loaded.");
            }
        }
    }
}