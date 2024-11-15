// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using System;

namespace CreateMikLabelModel.ML
{
    /// <summary>
    /// Progress handler that AutoML will invoke after each model it produces and evaluates.
    /// </summary>
    public class MulticlassExperimentProgressHandler : IProgress<RunDetail<MulticlassClassificationMetrics>>
    {
        private readonly LoggingHelper _consoleHelper;
        private int _iterationIndex;

        public MulticlassExperimentProgressHandler(ILogger logger) => _consoleHelper = new LoggingHelper(logger);


        public void Report(RunDetail<MulticlassClassificationMetrics> iterationResult)
        {
            if (_iterationIndex++ == 0)
            {
                _consoleHelper.PrintMulticlassClassificationMetricsHeader();
            }

            if (iterationResult.Exception != null)
            {
                _consoleHelper.PrintIterationException(iterationResult.Exception);
            }
            else
            {
                _consoleHelper.PrintIterationMetrics(_iterationIndex, iterationResult.TrainerName,
                    iterationResult.ValidationMetrics, iterationResult.RuntimeInSeconds);
            }
        }
    }
}
