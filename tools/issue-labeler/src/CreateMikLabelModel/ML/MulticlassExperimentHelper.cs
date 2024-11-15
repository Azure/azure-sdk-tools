// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using IssueLabeler.Shared;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;

namespace CreateMikLabelModel.ML
{
    public static class MulticlassExperimentHelper
    {
        public static ExperimentResult<MulticlassClassificationMetrics> RunAutoMLExperiment(
            ILogger logger, MLContext mlContext, MulticlassExperimentSettings experimentSettings,
            MulticlassExperimentProgressHandler progressHandler, IDataView dataView, ColumnInferenceResults columnInference)
        {
            new LoggingHelper(logger).ConsoleWriteHeader("=============== Running AutoML experiment ===============");
            logger.LogInformation($"Running AutoML multiclass classification experiment for {experimentSettings.MaxExperimentTimeInSeconds} seconds...");

            // Pre-featurize the title and description, and remove features that have less then 2.
            IEstimator<ITransformer> preFeaturizer =
                preFeaturizer = mlContext.Transforms.Text.FeaturizeText("TextFeatures",
                  new TextFeaturizingEstimator.Options(),
                  new[] { "Title", "Description" })
                  .Append(mlContext.Transforms.FeatureSelection.SelectFeaturesBasedOnCount("TextFeatures", "TextFeatures", 2))
                  .AppendCacheCheckpoint(mlContext);

            var experimentResult = mlContext.Auto()
                .CreateMulticlassClassificationExperiment(experimentSettings)
                .Execute(dataView, columnInference.ColumnInformation, progressHandler: progressHandler, preFeaturizer: preFeaturizer);

            logger.LogInformation(Environment.NewLine);
            logger.LogInformation($"num models created: {experimentResult.RunDetails.Count()}");

            // Get top few runs ranked by accuracy
            var topRuns = experimentResult.RunDetails
                .Where(r => r.ValidationMetrics != null && !double.IsNaN(r.ValidationMetrics.MicroAccuracy))
                .OrderByDescending(r => r.ValidationMetrics.MicroAccuracy)
                .Take(3)
                .ToArray();

            logger.LogInformation("Top models ranked by accuracy --");
            logger.LogInformation(CreateRow($"{"",-4} {"Trainer",-35} {"MicroAccuracy",14} {"MacroAccuracy",14} {"Duration",9}", Width));

            for (var i = 0; i < topRuns.Length; i++)
            {
                var run = topRuns[i];
                logger.LogInformation(CreateRow($"{i,-4} {run.TrainerName,-35} {run.ValidationMetrics?.MicroAccuracy ?? double.NaN,14:F4} {run.ValidationMetrics?.MacroAccuracy ?? double.NaN,14:F4} {run.RuntimeInSeconds,9:F1}", Width));
            }
            return experimentResult;
        }

        public static ExperimentResult<MulticlassClassificationMetrics> Train(
            ILogger logger, MLContext mlContext, MulticlassExperimentSettings experimentSettings,
            MulticlassExperimentProgressHandler progressHandler, TrainingDataFilePaths paths, TextLoader textLoader, ColumnInferenceResults columnInference)
        {
            var data = mlContext.Data.TrainTestSplit(textLoader.Load(paths.TrainPath, paths.ValidatePath), seed: 0);
            var experimentResult = RunAutoMLExperiment(logger, mlContext, experimentSettings, progressHandler, data.TrainSet, columnInference);

            EvaluateTrainedModelAndPrintMetrics(logger, mlContext, experimentResult.BestRun.Model, experimentResult.BestRun.TrainerName, data.TestSet);
            SaveModel(logger, mlContext, experimentResult.BestRun.Model, paths.ModelPath, data.TrainSet);
            return experimentResult;
        }

        public static ITransformer Retrain(ExperimentResult<MulticlassClassificationMetrics> experimentResult,
            string trainerName, MultiFileSource multiFileSource, string dataPath, string modelPath, TextLoader textLoader, ILogger logger, MLContext mlContext)
        {
            var dataView = textLoader.Load(dataPath);
            new LoggingHelper(logger).ConsoleWriteHeader("=============== Re-fitting best pipeline ===============");

            var combinedDataView = textLoader.Load(multiFileSource);
            var bestRun = experimentResult.BestRun;
            var refitModel = bestRun.Estimator.Fit(combinedDataView);

            EvaluateTrainedModelAndPrintMetrics(logger, mlContext, refitModel, trainerName, dataView);
            SaveModel(logger, mlContext, refitModel, modelPath, dataView);
            return refitModel;
        }

        public static ITransformer Retrain(ILogger logger, MLContext mlContext, ExperimentResult<MulticlassClassificationMetrics> experimentResult,
            ColumnInferenceResults columnInference, TrainingDataFilePaths paths, bool fixedBug = false)
        {
            new LoggingHelper(logger).ConsoleWriteHeader("=============== Re-fitting best pipeline ===============");

            var textLoader = mlContext.Data.CreateTextLoader(columnInference.TextLoaderOptions);
            var combinedDataView = textLoader.Load(new MultiFileSource(paths.TrainPath, paths.ValidatePath, paths.TestPath));
            var bestRun = experimentResult.BestRun;
            if (fixedBug)
            {
                // TODO: retry: below gave error but I thought it would work:
                //refitModel = MulticlassExperiment.Retrain(experimentResult,
                //    "final model",
                //    new MultiFileSource(paths.TrainPath, paths.ValidatePath, paths.FittedPath),
                //    paths.TestPath,
                //    paths.FinalPath, textLoader, mlContext);
                // but if failed before fixing this maybe the problem was in *EvaluateTrainedModelAndPrintMetrics*

            }
            var refitModel = bestRun.Estimator.Fit(combinedDataView);

            EvaluateTrainedModelAndPrintMetrics(logger, mlContext, refitModel, "production model", textLoader.Load(paths.TestPath));
            // Save the re-fit model to a.ZIP file
            SaveModel(logger, mlContext, refitModel, paths.FinalModelPath, textLoader.Load(paths.TestPath));

            logger.LogInformation($"The model is saved to {paths.FinalModelPath}");
            return refitModel;
        }

        private const int Width = 114;

        private static string CreateRow(string message, int width) => "|" + message.PadRight(width - 2) + "|";

        /// <summary>
        /// Evaluate the model and print metrics.
        /// </summary>
        private static void EvaluateTrainedModelAndPrintMetrics(ILogger logger, MLContext mlContext, ITransformer model, string trainerName, IDataView dataView)
        {
            logger.LogInformation("===== Evaluating model's accuracy with test data =====");
            var predictions = model.Transform(dataView);
            var metrics = mlContext.MulticlassClassification.Evaluate(predictions, labelColumnName: "Label", scoreColumnName: "Score");

            logger.LogInformation($"************************************************************");
            logger.LogInformation($"*    Metrics for {trainerName} multi-class classification model   ");
            logger.LogInformation($"*-----------------------------------------------------------");
            logger.LogInformation($"    MacroAccuracy = {metrics.MacroAccuracy:0.####}, a value between 0 and 1, the closer to 1, the better");
            logger.LogInformation($"    MicroAccuracy = {metrics.MicroAccuracy:0.####}, a value between 0 and 1, the closer to 1, the better");
            logger.LogInformation($"    LogLoss = {metrics.LogLoss:0.####}, the closer to 0, the better");
            for (int i = 0; i < metrics.PerClassLogLoss.Count; i++)
            {
                logger.LogInformation($"    LogLoss for class {i+1} = {metrics.PerClassLogLoss[i]:0.####}, the closer to 0, the better");
            }
            logger.LogInformation($"************************************************************");
        }

        private static void SaveModel(ILogger logger, MLContext mlContext, ITransformer model, string modelPath, IDataView dataview)
        {
            // Save the re-fit model to a.ZIP file
            var consoleHelper = new LoggingHelper(logger);
            consoleHelper.ConsoleWriteHeader("=============== Saving the model ===============");
            mlContext.Model.Save(model, dataview.Schema, modelPath);
            logger.LogInformation($"The model is saved to {modelPath}");
        }

        public static void TestPrediction(ILogger logger, MLContext mlContext, TrainingDataFilePaths files, bool forPrs, double threshold = 0.4)
        {
            var trainedModel = mlContext.Model.Load(files.FittedModelPath, out _);
            IEnumerable<(string knownLabel, GitHubIssuePrediction predictedResult, string issueNumber)> predictions = null;
            string Legend1 = $"(includes not labeling issues with confidence lower than threshold. (here {threshold * 100.0f:#,0.00}%))";
            const string Legend2 = "(includes items that could be labeled if threshold was lower.)";
            const string Legend3 = "(those incorrectly labeled)";
            if (forPrs)
            {
                var testData = GetPullRequests(mlContext, files.TestPath);
                logger.LogInformation($"{Environment.NewLine}Number of PRs tested: {testData.Length}");

                var prEngine = mlContext.Model.CreatePredictionEngine<GitHubPullRequest, GitHubIssuePrediction>(trainedModel);
                predictions = testData
                   .Select(x => (
                        knownLabel: x.Label,
                        predictedResult: prEngine.Predict(x),
                        issueNumber: x.ID.ToString()
                   ));
            }
            else
            {
                var testData = GetIssues(mlContext, files.TestPath);
                logger.LogInformation($"{Environment.NewLine}\tNumber of issues tested: {testData.Length}");

                var issueEngine = mlContext.Model.CreatePredictionEngine<GitHubIssue, GitHubIssuePrediction>(trainedModel);
                predictions = testData
                   .Select(x => (
                        knownLabel: x.Label,
                        predictedResult: issueEngine.Predict(x),
                        issueNumber: x.ID.ToString()
                   ));
            }

            var analysis =
                predictions.Select(x =>
                (
                    knownLabel: x.knownLabel,
                    predictedArea: x.predictedResult.Area,
                    maxScore: x.predictedResult.Score.Max(),
                    confidentInPrediction: x.predictedResult.Score.Max() >= threshold,
                    issueNumber: x.issueNumber
                ));

            var countSuccess = analysis.Where(x =>
                    (x.confidentInPrediction && x.predictedArea.Equals(x.knownLabel, StringComparison.Ordinal)) ||
                    (!x.confidentInPrediction && !x.predictedArea.Equals(x.knownLabel, StringComparison.Ordinal))).Count();

            var missedOpportunity = analysis
                .Where(x => !x.confidentInPrediction && x.knownLabel.Equals(x.predictedArea, StringComparison.Ordinal)).Count();

            var mistakes = analysis
                .Where(x => x.confidentInPrediction && !x.knownLabel.Equals(x.predictedArea, StringComparison.Ordinal))
                .Select(x => new { Pair = $"\tPredicted: {x.predictedArea}, Actual:{x.knownLabel}", IssueNumbers = x.issueNumber, MaxConfidencePercentage = x.maxScore * 100.0f })
                .GroupBy(x => x.Pair)
                .Select(x => new
                {
                    Count = x.Count(),
                    PerdictedVsActual = x.Key,
                    Items = x,
                })
                .OrderByDescending(x => x.Count);
            int remaining = predictions.Count() - countSuccess - missedOpportunity;

            logger.LogInformation($"{Environment.NewLine}\thandled correctly: {countSuccess}{Environment.NewLine}\t{Legend1}{Environment.NewLine}");
            logger.LogInformation($"{Environment.NewLine}\tmissed: {missedOpportunity}{Environment.NewLine}\t{Legend2}{Environment.NewLine}");
            logger.LogInformation($"{Environment.NewLine}\tremaining: {remaining}{Environment.NewLine}\t{Legend3}{Environment.NewLine}");

            foreach (var mismatch in mistakes.AsEnumerable())
            {
                logger.LogInformation($"{mismatch.PerdictedVsActual}, NumFound: {mismatch.Count}");
                var sampleIssues = string.Join(Environment.NewLine, mismatch.Items.Select(x => $"\t\tFor #{x.IssueNumbers} was {x.MaxConfidencePercentage:#,0.00}% confident"));
                logger.LogInformation($"{Environment.NewLine}{ sampleIssues }{Environment.NewLine}");
            }
        }

        public static GitHubIssue[] GetIssues(MLContext mlContext, string dataFilePath)
        {
            var dataView = mlContext.Data.LoadFromTextFile<GitHubIssue>(
                                            path: dataFilePath,
                                            hasHeader: true,
                                            separatorChar: '\t',
                                            allowQuoting: true,
                                            allowSparse: false);

            return mlContext.Data.CreateEnumerable<GitHubIssue>(dataView, false).ToArray();
        }

        public static GitHubPullRequest[] GetPullRequests(MLContext mlContext, string dataFilePath)
        {
            var dataView = mlContext.Data.LoadFromTextFile<GitHubPullRequest>(
                                            path: dataFilePath,
                                            hasHeader: true,
                                            separatorChar: '\t',
                                            allowQuoting: true,
                                            allowSparse: false);

            return mlContext.Data.CreateEnumerable<GitHubPullRequest>(dataView, false).ToArray();
        }
    }
}
