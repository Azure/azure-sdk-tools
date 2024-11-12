﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
