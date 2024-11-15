// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML;
using Microsoft.ML.AutoML;
using System.IO;

namespace CreateMikLabelModel.ML
{
    public static class MulticlassExperimentSettingsHelper
    {
        public static (ColumnInferenceResults columnInference, MulticlassExperimentSettings experimentSettings) SetupExperiment(
            ILogger logger, MLContext mlContext, ExperimentModifier st, TrainingDataFilePaths paths, bool forPrs)
        {
            var columnInference = InferColumns(logger, mlContext, paths.TrainPath, st.LabelColumnName);
            var columnInformation = columnInference.ColumnInformation;
            st.ColumnSetup(columnInformation, forPrs);

            var experimentSettings = new MulticlassExperimentSettings();
            st.TrainerSetup(experimentSettings.Trainers);
            experimentSettings.MaxExperimentTimeInSeconds = st.ExperimentTime;

            var cts = new System.Threading.CancellationTokenSource();
            experimentSettings.CancellationToken = cts.Token;

            // Set the cache directory to null.
            // This will cause all models produced by AutoML to be kept in memory
            // instead of written to disk after each run, as AutoML is training.
            // (Please note: for an experiment on a large dataset, opting to keep all
            // models trained by AutoML in memory could cause your system to run out
            // of memory.)
            experimentSettings.CacheDirectoryName = Path.GetTempPath();
            experimentSettings.OptimizingMetric = MulticlassClassificationMetric.MicroAccuracy;
            return (columnInference, experimentSettings);
        }

        /// <summary>
        /// Infer columns in the dataset with AutoML.
        /// </summary>
        private static ColumnInferenceResults InferColumns(ILogger logger, MLContext mlContext, string dataPath, string labelColumnName)
        {
            new LoggingHelper(logger).ConsoleWriteHeader("=============== Inferring columns in dataset ===============");
            var columnInference = mlContext.Auto().InferColumns(dataPath, labelColumnName, groupColumns: false);
            return columnInference;
        }
    }

}