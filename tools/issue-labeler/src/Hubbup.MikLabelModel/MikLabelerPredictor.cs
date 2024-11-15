// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using IssueLabeler.Shared;
using Microsoft.ML;
using Microsoft.ML.Data;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hubbup.MikLabelModel
{
    public class MikLabelerPredictor
    {
        private readonly PredictionEngine<GitHubIssue, GitHubIssuePrediction> _predictionEngine;
        private readonly PredictionEngine<GitHubPullRequest, GitHubIssuePrediction> _prPredictionEngine;
        private readonly Regex _regex = new Regex(@"@[a-zA-Z0-9_//-]+");
        private readonly DiffHelper _diffHelper = new DiffHelper();

        public MikLabelerPredictor(PredictionEngine<GitHubIssue, GitHubIssuePrediction> predictionEngine,
            PredictionEngine<GitHubPullRequest, GitHubIssuePrediction> prPredictionEngine)
        {
            _predictionEngine = predictionEngine;
            _prPredictionEngine = prPredictionEngine;
        }

        public LabelSuggestion PredictLabel(Issue issue, string[] filePaths = null)
        {
            var userMentions = issue.Body != null ? _regex.Matches(issue.Body).Select(x => x.Value).ToArray() : new string[0];

            List<ScoredLabel> labelPredictions;
            if (filePaths == null)
            {
                var aspnetIssue = new GitHubIssue
                {
                    ID = issue.Number,
                    Title = issue.Title,
                    Description = issue.Body,
                    IsPR = 0,
                    Author = issue.User.Login,
                    UserMentions = string.Join(' ', userMentions),
                    NumMentions = userMentions.Length,
                };
                var prediction = _predictionEngine.Predict(aspnetIssue);
                labelPredictions = GetBestThreePredictions(prediction, forPrs: false);
            }
            else
            {
                var segmentedDiff = _diffHelper.SegmentDiff(filePaths);
                var aspnetIssue = new GitHubPullRequest
                {
                    ID = issue.Number,
                    Title = issue.Title,
                    Description = issue.Body,
                    IsPR = 1,
                    Author = issue.User.Login,
                    UserMentions = string.Join(' ', userMentions),
                    NumMentions = userMentions.Length,
                    FileCount = filePaths.Length,
                    Files = string.Join(' ', segmentedDiff.FileDiffs),
                    Filenames = string.Join(' ', segmentedDiff.Filenames),
                    FileExtensions = string.Join(' ', segmentedDiff.Extensions),
                    FolderNames = _diffHelper.FlattenWithWhitespace(segmentedDiff.FolderNames),
                    Folders = _diffHelper.FlattenWithWhitespace(segmentedDiff.Folders)
                };
                var prediction = _prPredictionEngine.Predict(aspnetIssue);
                labelPredictions = GetBestThreePredictions(prediction, forPrs: true);
            }

            return new LabelSuggestion
            {
                LabelScores = labelPredictions,
            };
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
                _prPredictionEngine.OutputSchema[nameof(GitHubIssuePrediction.Score)].GetSlotNames(ref slotNames);
            }
            else
            {
                _predictionEngine.OutputSchema[nameof(GitHubIssuePrediction.Score)].GetSlotNames(ref slotNames);
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
