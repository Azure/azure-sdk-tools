using System.Collections.Concurrent;
using Hubbup.MikLabelModel;
using Microsoft.Extensions.Logging;
using IssueLabeler.Shared;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;


namespace IssueLabelerService
{
    public class MLLabeler : ILabeler
    {
        private readonly ILogger<LabelerFactory> _logger;
        private static readonly ConcurrentDictionary<string, byte> CommonModelRepositories = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> InitializedRepositories = new(StringComparer.OrdinalIgnoreCase);
        private readonly RepositoryConfiguration _config;
        private IModelHolderFactoryLite _modelHolderFactory { get; }
        private ILabelerLite Labeler { get; }
        private string CommonModelRepositoryName { get; }

        public MLLabeler(ILogger<LabelerFactory> logger, IModelHolderFactoryLite modelHolderFactory, ILabelerLite labeler, RepositoryConfiguration config)
        {
            _logger = logger;
            _modelHolderFactory = modelHolderFactory;
            Labeler = labeler;

            CommonModelRepositoryName = config.CommonModelRepositoryName;

            // Initialize the set of repositories that use the common model.
            ConvertRepoStringList(config.ReposUsingCommonModel, CommonModelRepositories);

            _config = config;
        }

        public async Task<Dictionary<string, string>> PredictLabels(IssuePayload issue)
        {
            var predictionRepositoryName = TranslateRepoName(issue.RepositoryName);

            // If the model needed for this request hasn't been initialized, do so now.
            if (!InitializedRepositories.ContainsKey(predictionRepositoryName))
            {
                _logger.LogInformation($"Models for {predictionRepositoryName} have not yet been initialized; loading prediction models.");

                try
                {
                    var allBlobConfigNames = ExtractBlobConfigNames(predictionRepositoryName);

                    // The model factory is thread-safe and will manage its own concurrency.
                    await _modelHolderFactory.CreateModelHolders(issue.RepositoryOwnerName, predictionRepositoryName, allBlobConfigNames).ConfigureAwait(false);
                    InitializedRepositories.TryAdd(predictionRepositoryName, 1);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error initializing the label prediction models for {predictionRepositoryName}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                    throw;
                }
                finally
                {
                    _logger.LogInformation($"Model initialization is complete for {predictionRepositoryName}.");
                }
            }

            // Predict labels.
            _logger.LogInformation($"Predicting labels for {issue.RepositoryName} using the `{predictionRepositoryName}` model for issue #{issue.IssueNumber}.");

            try
            {
                // In order for labels to be valid for Azure SDK use, there must
                // be at least two of them, which corresponds to a Service (pink)
                // and Category (yellow).  If that is not met, then no predictions
                // should be returned.
                var predictions = await Labeler.QueryLabelPrediction(
                    issue.IssueNumber,
                    issue.Title,
                    issue.Body,
                    issue.IssueUserLogin,
                    predictionRepositoryName,
                    issue.RepositoryOwnerName);

                if (predictions.Count < 2)
                {
                    _logger.LogInformation($"No labels were predicted for {issue.RepositoryName} using the `{predictionRepositoryName}` model for issue #{issue.IssueNumber}.");
                    throw new Exception($"No labels were predicted for {issue.RepositoryName} using the `{predictionRepositoryName}` model for issue #{issue.IssueNumber}.");
                }

                _logger.LogInformation($"Labels were predicted for {issue.RepositoryName} using the `{predictionRepositoryName}` model for issue #{issue.IssueNumber}.  Using: [{predictions[0]}, {predictions[1]}].");
                
                return new Dictionary<string, string>
                {
                    { LabelType.Service, predictions[0] }, 
                    { LabelType.Category, predictions[1] }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error querying predictions for {issue.RepositoryName} using the `{predictionRepositoryName}` model for issue #{issue.IssueNumber}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                throw;
            }
        }

        private string TranslateRepoName(string repoName) =>
            CommonModelRepositories.ContainsKey(repoName)
            ? CommonModelRepositoryName
            : repoName;

        private void ConvertRepoStringList(string repos, ConcurrentDictionary<string, byte> dict)
        {
            if (!string.IsNullOrEmpty(repos))
            {
                foreach (var repo in repos.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    dict.TryAdd(repo, 1);
                }
            }
        }

        private string[] ExtractBlobConfigNames(string repoName)
        {
            // Use a switch expression to match the repository name to the corresponding BlobConfigNames property
            var blobConfigNames = repoName switch
            {
                "azure-sdk-for-java" => _config.IssueModelAzureSdkForJavaBlobConfigNames,
                "azure-sdk-for-net" => _config.IssueModelAzureSdkForNetBlobConfigNames,
                _ => _config.IssueModelAzureSdkBlobConfigNames,
            };

            // Split the BlobConfigNames into an array and return
            return blobConfigNames.Split(';', StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
