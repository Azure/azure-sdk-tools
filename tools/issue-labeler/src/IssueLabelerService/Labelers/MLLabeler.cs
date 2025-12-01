using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using IssueLabeler.Shared;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.ComponentModel;


namespace IssueLabelerService
{
    public class MLLabeler : ILabeler
    {
        private readonly ILogger<LabelerFactory> _logger;
        private static readonly ConcurrentDictionary<string, byte> InitializedRepositories = new(StringComparer.OrdinalIgnoreCase);
        private readonly RepositoryConfiguration _config;
        private IModelHolderFactoryLite _modelHolderFactory { get; }

        public MLLabeler(ILogger<LabelerFactory> logger, IModelHolderFactoryLite modelHolderFactory, RepositoryConfiguration config)
        {
            _logger = logger;
            _modelHolderFactory = modelHolderFactory;
            _config = config;
        }

        public async Task<Dictionary<string, string>> PredictLabels(IssuePayload issue)
        {
            // If the model needed for this request hasn't been initialized, do so now.
            if (!InitializedRepositories.ContainsKey(issue.RepositoryName))
            {
                _logger.LogInformation($"Models for {issue.RepositoryName} have not yet been initialized; loading prediction models.");
            
            try
                {
                    // The model factory is thread-safe and will manage its own concurrency.
                    await _modelHolderFactory.CreateModelHolders(issue.RepositoryName).ConfigureAwait(false);
                    InitializedRepositories.TryAdd(issue.RepositoryName, 1);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error initializing the label prediction models for {issue.RepositoryName}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                    throw;
                }
                finally
                {
                    _logger.LogInformation($"Model initialization is complete for {issue.RepositoryName}.");
                }
            }

            // Predict labels.
            _logger.LogInformation($"Predicting labels for {issue.RepositoryName} using the `{issue.RepositoryName}` model for issue #{issue.IssueNumber}.");

            try
            {
                // In order for labels to be valid for Azure SDK use, there must
                // be at least two of them, which corresponds to a Service (pink)
                // and Category (yellow).  If that is not met, then no predictions
                // should be returned.

                var ghIssue = new GitHubIssue()
                {
                    Title = issue.Title,
                    Description = issue.Body,
                };

                var categoryPredictor = await _modelHolderFactory.GetPredictor(issue.RepositoryName, LabelType.Category).ConfigureAwait(false);
                var servicePredictor = await _modelHolderFactory.GetPredictor(issue.RepositoryName, LabelType.Service).ConfigureAwait(false);

                var categorySuggestions = await categoryPredictor.Predict(ghIssue).ConfigureAwait(false);
                var serviceSuggestions = await servicePredictor.Predict(ghIssue).ConfigureAwait(false);

                if (categorySuggestions.Count == 0 || serviceSuggestions.Count == 0)
                {
                    _logger.LogInformation($"No labels were predicted for {issue.RepositoryName} using the `{issue.RepositoryName}` model for issue #{issue.IssueNumber}.");
                    throw new Exception($"No labels were predicted for {issue.RepositoryName} using the `{issue.RepositoryName}` model for issue #{issue.IssueNumber}.");
                }
                string topCategorySuggestion = categorySuggestions[0].LabelName;
                string topServiceSuggestion = serviceSuggestions[0].LabelName;
                _logger.LogInformation($"Labels were predicted for {issue.RepositoryName} using the `{issue.RepositoryName}` model for issue #{issue.IssueNumber}.  Using: [{topCategorySuggestion}, {topServiceSuggestion}].");
                
                return new Dictionary<string, string>
                {
                    { LabelType.Service, topServiceSuggestion }, 
                    { LabelType.Category, topCategorySuggestion }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error querying predictions for {issue.RepositoryName} using the `{issue.RepositoryName}` model for issue #{issue.IssueNumber}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                throw;
            }
        }
    }
}
