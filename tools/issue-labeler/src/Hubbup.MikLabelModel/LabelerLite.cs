// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IssueLabeler.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Hubbup.MikLabelModel
{
    public class LabelerLite : ILabelerLite
    {
        private static Regex MentionsRegex { get; } = new Regex(@"@[a-zA-Z0-9_//-]+", RegexOptions.Compiled);

        private readonly ILogger<LabelerLite> _logger;
        private readonly IModelHolderFactoryLite _modelHolderFactory;
        private readonly IConfiguration _config;
        private const float defaultConfidenceThreshold = 0.60f;
        private const string defaultModel = "default model";

        public LabelerLite(
            ILogger<LabelerLite> logger,
            IModelHolderFactoryLite modelHolderFactory,
            IConfiguration config)
        {
            _logger = logger;
            _modelHolderFactory = modelHolderFactory;
            _config = config;
        }

       public async Task<List<string>> QueryLabelPrediction(
            int issueNumber,
            string title,
            string body,
            string issueUserLogin,
            string repositoryName,
            string repositoryOwnerName)
        {
            AssertNotNullOrEmpty(title, nameof(title));
            AssertNotNullOrEmpty(body, nameof(body));
            AssertNotNullOrEmpty(issueUserLogin, nameof(issueUserLogin));
            AssertNotNullOrEmpty(repositoryName, nameof(repositoryName));
            AssertNotNullOrEmpty(repositoryOwnerName, nameof(repositoryOwnerName));

            _logger.LogInformation($"Predict Labels started query for {repositoryOwnerName}/{repositoryName}#{issueNumber}");

            // Query raw predictions
            var issueModel = CreateIssue(issueNumber, title, body, issueUserLogin);
            var predictions = await GetPredictions(repositoryOwnerName, repositoryName, issueNumber, issueModel);

            // Determine the confidence threshold to use for filtering predictions
            float confidenceThreshold;

            if (!float.TryParse(_config["ConfidenceThreshold"], out confidenceThreshold))
            {
                confidenceThreshold = defaultConfidenceThreshold;
                _logger.LogInformation($"Prediction confidence default threshold of {confidenceThreshold} will be used as no value was configured. {repositoryOwnerName}/{repositoryName}#{issueNumber}");
            }
            else
            {
                _logger.LogInformation($"Prediction confidence threshold of {confidenceThreshold} will be used. {repositoryOwnerName}/{repositoryName}#{issueNumber}");
            }

            // Filter predictions based on the confidence threshold.
            var predictedLabels = new List<string>();

            foreach (var labelSuggestion in predictions)
            {
                var topChoice = labelSuggestion.LabelScores.OrderByDescending(x => x.Score).First();

                if (topChoice.Score >= confidenceThreshold)
                {
                    predictedLabels.Add(topChoice.LabelName);
                }
                else
                {
                    _logger.LogWarning($"Label prediction was below confidence level `{confidenceThreshold}` for Model:`{labelSuggestion.ModelConfigName ?? defaultModel}`: '{string.Join(", ", labelSuggestion.LabelScores.Select(x => $"{x.LabelName}:[{x.Score}]"))}'");
                }
            }

            _logger.LogInformation($"Predict Labels query for {repositoryOwnerName}/{repositoryName}#{issueNumber} suggested {predictedLabels.Count} labels.");
            return predictedLabels;
        }

        private async Task<List<LabelSuggestion>> GetPredictions(string owner, string repo, int number, GitHubIssue issueModel)
        {
            List<LabelSuggestion> predictions = new List<LabelSuggestion>();
            List<IPredictor> predictors = new List<IPredictor>();

            if (_config.TryGetConfigValue($"IssueModel.{repo.Replace("-", "_")}.BlobConfigNames", out var blobConfig))
            {
                var blobConfigs = blobConfig.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var blobConfigName in blobConfigs)
                {
                    // get a prediction for each model
                    var predictor = await _modelHolderFactory.GetPredictor(owner, repo, blobConfigName);
                    predictors.Add(predictor);
                }
            }
            else
            {
                // Add just the default predictor
                var predictor = await _modelHolderFactory.GetPredictor(owner, repo);
                predictors.Add(predictor);
            }

            foreach (var predictor in predictors)
            {
                var labelSuggestion = await predictor.Predict(issueModel);
                labelSuggestion.ModelConfigName = predictor.ModelName;
                if (labelSuggestion == null)
                {
                    _logger.LogCritical($"Failed: Unable to get prediction for {owner}/{repo}#{number}. ModelName:{predictor.ModelName}");
                    return null;
                }
                _logger.LogInformation($"Prediction results for {owner}/{repo}#{number}, Model:{labelSuggestion.ModelConfigName ?? defaultModel}: '{string.Join(",", labelSuggestion.LabelScores.Select(x => $"{x.LabelName}:{x.Score}"))}'");
                predictions.Add(labelSuggestion);
            }

            return predictions;
        }

        private static GitHubIssue CreateIssue(int number, string title, string body, string author)
        {
            var userMentions = MentionsRegex.Matches(body ?? string.Empty).Select(x => x.Value).ToArray();

            return new GitHubIssue()
            {
                ID = number,
                Title = title,
                Description = body,
                IsPR = 0,
                Author = author,
                UserMentions = string.Join(' ', userMentions),
                NumMentions = userMentions.Length
            };
        }

        private static void AssertNotNullOrEmpty(string value, string paramName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"{paramName} cannot be null or empty.", paramName);
            }
        }
    }
}
