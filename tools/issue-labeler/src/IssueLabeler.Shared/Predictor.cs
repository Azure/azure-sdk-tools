// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace IssueLabeler.Shared
{
    public class Predictor : IPredictor
    {
        private static SemaphoreSlim sem = new SemaphoreSlim(1);
        private readonly ILogger logger;
        private readonly IModelHolder modelHolder;
        private readonly float threshold;

        public Predictor(ILogger logger, RepositoryConfiguration config, IModelHolder modelHolder)
        {
            this.logger = logger;
            this.modelHolder = modelHolder;
            threshold = config.ScoreThreshold is not null ? float.Parse(config.ScoreThreshold) : 0.3f;
        }

        public Task<List<ScoredLabel>> Predict(GitHubIssue issue)
        {
            return Predict(issue, modelHolder.IssuePredEngine, logger, threshold);
        }

        public Task<List<ScoredLabel>> Predict(GitHubPullRequest pullRequest)
        {
            if (modelHolder.UseIssuesForPrsToo)
            {
                return Predict(pullRequest, modelHolder.IssuePredEngine, logger, threshold);
            }
            return Predict(pullRequest, modelHolder.PrPredEngine, logger, threshold);
        }

        private static async Task<List<ScoredLabel>> Predict<T>(
            T issueOrPr,
            PredictionEngine<T, GitHubIssuePrediction> predEngine,
            ILogger logger,
            float threshold)
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
            var labelPredictions = GetBestThreePredictions(probabilities, slotNames);

            float maxProbability = probabilities.Max();
            logger.LogInformation($"MaxProbability: {maxProbability} for #{issueOrPr.ID} - '{issueOrPr.Title}'");
            logger.LogInformation($"Top 3 suggested labels: '{string.Join(", ", labelPredictions.Select(x => $"{x.LabelName}:[{x.Score}]"))}'");
            return [.. labelPredictions.Where(x => x.Score >= threshold)];
            
        }

        public static List<ScoredLabel> GetBestThreePredictions(float[] scores, VBuffer<ReadOnlyMemory<char>> slotNames)
        {
            var topThreeScores = GetIndexesOfTopScores(scores, 3);

            return new List<ScoredLabel>
                {
                    new ScoredLabel {LabelName=slotNames.GetItemOrDefault(topThreeScores[0]).ToString(), Score = scores[topThreeScores[0]] },
                    new ScoredLabel {LabelName=slotNames.GetItemOrDefault(topThreeScores[1]).ToString(), Score = scores[topThreeScores[1]] },
                    new ScoredLabel {LabelName=slotNames.GetItemOrDefault(topThreeScores[2]).ToString(), Score = scores[topThreeScores[2]] },
                };
        }

        private List<ScoredLabel> GetBestThreePredictions(GitHubIssuePrediction prediction, bool forPrs)
        {
            var scores = prediction.Score;

            VBuffer<ReadOnlyMemory<char>> slotNames = default;
            if (forPrs)
            {
                modelHolder.PrPredEngine.OutputSchema[nameof(GitHubIssuePrediction.Score)].GetSlotNames(ref slotNames);
            }
            else
            {
                modelHolder.IssuePredEngine.OutputSchema[nameof(GitHubIssuePrediction.Score)].GetSlotNames(ref slotNames);
            }

            var topThreeScores = GetIndexesOfTopScores(scores, 3);

            return new List<ScoredLabel>
                {
                    new ScoredLabel {LabelName=slotNames.GetItemOrDefault(topThreeScores[0]).ToString(), Score = scores[topThreeScores[0]] },
                    new ScoredLabel {LabelName=slotNames.GetItemOrDefault(topThreeScores[1]).ToString(), Score = scores[topThreeScores[1]] },
                    new ScoredLabel {LabelName=slotNames.GetItemOrDefault(topThreeScores[2]).ToString(), Score = scores[topThreeScores[2]] },
                };
        }

        private static IReadOnlyList<int> GetIndexesOfTopScores(float[] scores, int n)
        {
            var indexedScores = scores
                .Zip(Enumerable.Range(0, scores.Length), (score, index) => new IndexedScore(index, score));

            var indexedScoresSortedByScore = indexedScores
                .OrderByDescending(indexedScore => indexedScore.Score);

            return indexedScoresSortedByScore
                .Take(n)
                .Select(indexedScore => indexedScore.Index)
                .ToList()
                .AsReadOnly();
        }

        private struct IndexedScore
        {
            public IndexedScore(int index, float score) => (Index, Score) = (index, score);

            public int Index { get; }
            public float Score { get; }
        }
    }
}
