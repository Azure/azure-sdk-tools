// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Diagnostics;

namespace CreateMikLabelModel.ML
{
    public class MLHelper
    {
        private readonly MLContext _mLContext;
        private readonly ILogger _logger;

        public MLHelper(ILogger logger)
        {
            _mLContext = new MLContext(seed: 0);
            _logger = logger;
        }

        public void Test(TrainingDataFilePaths files, bool forPrs)
        {
            MulticlassExperimentHelper.TestPrediction(_logger, _mLContext, files, forPrs: forPrs);
        }

        public void Train(TrainingDataFilePaths files, bool forPrs)
        {
            var stopWatch = Stopwatch.StartNew();

            var st = new ExperimentModifier(files, forPrs);
            Train(st);

            stopWatch.Stop();
            _logger.LogInformation($"Done creating model in {stopWatch.ElapsedMilliseconds}ms");
        }

        private void Train(ExperimentModifier settings)
        {
            var setup = MulticlassExperimentSettingsHelper.SetupExperiment(_logger, _mLContext, settings, settings.Paths, settings.ForPrs);

            // Start experiment
            var textLoader = _mLContext.Data.CreateTextLoader(setup.columnInference.TextLoaderOptions);
            var paths = settings.Paths;

            // train once:
            var experimentResult = MulticlassExperimentHelper.Train(
                _logger, _mLContext, setup.experimentSettings, new MulticlassExperimentProgressHandler(_logger), paths, textLoader, setup.columnInference);

            // train twice
            _ = MulticlassExperimentHelper.Retrain(experimentResult,
                "refit model",
                new MultiFileSource(paths.TrainPath, paths.ValidatePath),
                paths.ValidatePath,
                paths.FittedModelPath, textLoader, _logger, _mLContext);

            // final train:
            _ = MulticlassExperimentHelper.Retrain(_logger, _mLContext, experimentResult, setup.columnInference, paths);
        }
    }
}
