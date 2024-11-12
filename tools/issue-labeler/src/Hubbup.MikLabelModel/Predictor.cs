// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using IssueLabeler.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hubbup.MikLabelModel
{
    public class Predictor : IPredictor
    {
        private static SemaphoreSlim sem = new SemaphoreSlim(1);
        private readonly ILogger logger;
        private readonly IModelHolder modelHolder;

        public string ModelName { get; set; }

        public Predictor(ILogger logger, IModelHolder modelHolder)
        {
            this.logger = logger;
            this.modelHolder = modelHolder;
        }

        public Task<LabelSuggestion> Predict(GitHubIssue issue)
        {
            return Predict(issue, modelHolder.IssuePredEngine, logger);
        }

        public Task<LabelSuggestion> Predict(GitHubPullRequest issue)
        {
            if (modelHolder.UseIssuesForPrsToo)
            {
                return Predict(issue, modelHolder.IssuePredEngine, logger);
            }
            return Predict(issue, modelHolder.PrPredEngine, logger);
        }

        private static async Task<LabelSuggestion> Predict<T>(
            T issueOrPr,
            PredictionEngine<T, GitHubIssuePrediction> predEngine,
            ILogger logger)
            where T : GitHubIssue
        {
            if (predEngine == null)
            {
                throw new InvalidOperationException("expected prediction engine loaded.");
            }
            GitHubIssuePrediction prediction;
            bool acquired = false;

            try
            {
                await sem.WaitAsync();
                acquired = true;
                prediction = predEngine.Predict(issueOrPr);
            }
            finally
            {
                if (acquired)
                {
                    sem.Release();
                }
            }

            VBuffer<ReadOnlyMemory<char>> slotNames = default;
            predEngine.OutputSchema[nameof(GitHubIssuePrediction.Score)].GetSlotNames(ref slotNames);

            float[] probabilities = prediction.Score;
            var labelPredictions = MikLabelerPredictor.GetBestThreePredictions(probabilities, slotNames);

            float maxProbability = probabilities.Max();
            logger.LogInformation($"MaxProbability: {maxProbability} for #{issueOrPr.ID} - '{issueOrPr.Title}'");
            return new LabelSuggestion
            {
                LabelScores = labelPredictions,
            };
        }
    }
}
