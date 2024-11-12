// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using IssueLabeler.Shared;
using Microsoft.ML;

namespace Hubbup.MikLabelModel
{
    //This "Labeler" class could be used in a different End-User application (Web app, other console app, desktop app, etc.)
    public class MikLabelerModel
    {
        private readonly PredictionEngine<GitHubIssue, GitHubIssuePrediction> _issuePredictionEngine;
        private readonly PredictionEngine<GitHubPullRequest, GitHubIssuePrediction> _prPredictionEngine;

        public MikLabelerModel((string modelPath, string prModelPath) paths)
        {
            var modelPath = paths.modelPath;
            var prModelPath = paths.prModelPath;
            var mlContext = new MLContext(seed: 1);

            // Load model from file
            var trainedModel = mlContext.Model.Load(modelPath, inputSchema: out _);
            var trainedPrModel = mlContext.Model.Load(prModelPath, inputSchema: out _);

            _issuePredictionEngine = mlContext.Model.CreatePredictionEngine<GitHubIssue, GitHubIssuePrediction>(trainedModel);
            _prPredictionEngine = mlContext.Model.CreatePredictionEngine<GitHubPullRequest, GitHubIssuePrediction>(trainedPrModel);
        }

        public MikLabelerPredictor GetPredictor()
        {
            // Create prediction engine related to the loaded trained model
            return new MikLabelerPredictor(_issuePredictionEngine, _prPredictionEngine);
        }
    }
}
