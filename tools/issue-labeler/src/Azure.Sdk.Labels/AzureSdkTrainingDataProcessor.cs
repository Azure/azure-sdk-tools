// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using CreateMikLabelModel;
using CreateMikLabelModel.Models;
using Octokit;

namespace Azure.Sdk.LabelTrainer
{
    internal class AzureSdkTrainingDataProcessor : TrainingDataProcessor
    {
        /// <summary>
        ///   Initializes a new instance of the <see cref="AzureSdkTrainingDataProcessor"/> class.
        /// </summary>
        ///
        /// <param name="logger">The logger to use for reporting information as items are prepared.</param>
        ///
        public AzureSdkTrainingDataProcessor(ILogger logger) : base(logger)
        {
        }

        /// <summary>
        ///   Prepares training data based on repository issues, transforming it into the
        ///   appropriate <see cref="TrainingDataItem" /> representation.
        /// </summary>
        ///
        /// <param name="trainingData">The raw training data, in the form of repository issues.</param>
        /// <param name="repositoryName">The name of the repository that was the source of the training data.</param>
        ///
        /// <returns>The set of <see cref="TrainingDataItem" /> instances prepared from the <paramref name="trainingData"/>.</returns>
        ///
        public override async IAsyncEnumerable<TrainingDataItem> PrepareData(
            IAsyncEnumerable<Issue> trainingData,
            string repositoryName)
        {
            var itemCount = 0;

            await foreach (var issue in trainingData)
            {
                if (issue.Labels.Count > 0)
                {
                    foreach (var label in issue.Labels)
                    {
                        var segment = GetSegment(label);

                        if (segment != null)
                        {
                            ++itemCount;
                            yield return new TrainingDataItem(label.Name, segment, repositoryName, issue);
                        }
                    }
                }
                else
                {
                    Logger.LogWarning($"Issue: { issue.Id } has no labels and should have been filtered.");
                }
            }

            Logger.LogInformation($"Prepared { itemCount } training set items from issue training data.");
        }

        /// <summary>
        ///   Prepares training data based on repository pull requests, transforming it into the
        ///   appropriate <see cref="TrainingDataItem" /> representation.
        /// </summary>
        ///
        /// <param name="trainingData">The raw training data, in the form of repository pull requests.</param>
        /// <param name="repositoryName">The name of the repository that was the source of the training data.</param>
        ///
        /// <returns>The set of <see cref="TrainingDataItem" /> instances prepared from the <paramref name="trainingData"/>.</returns>
        ///
        public override async IAsyncEnumerable<TrainingDataItem> PrepareData(
            IAsyncEnumerable<PullRequestWithFiles> trainingData,
            string repositoryName)
        {
            var itemCount = 0;

            await foreach (var pullRequest in trainingData)
            {
                if (pullRequest.PullRequest.Labels.Count > 0)
                {
                    foreach (var label in pullRequest.PullRequest.Labels)
                    {
                        var segment = GetSegment(label);

                        if (segment != null)
                        {
                            ++itemCount;
                            yield return new TrainingDataItem(label.Name, DefaultSegmentName, repositoryName, pullRequest);
                        }
                    }
                }
                else
                {
                    Logger.LogWarning($"Pull Request: { pullRequest.PullRequest.Id } has no labels and should have been filtered.");
                }
            }

            Logger.LogInformation($"Prepared { itemCount } training set items from pull request training data.");
        }

        /// <summary>
        ///   Gets the segment that the training data should be associated with.
        /// </summary>
        ///
        /// <param name="label">The label to consider.</param>
        ///
        /// <returns>The segment name.</returns>
        ///
        private string GetSegment(Label label) => label switch
        {
            null => null,
            _ when AzureSdkLabel.IsCategoryLabel(label) => "Category",
            _ when AzureSdkLabel.IsServiceLabel(label) => "Service",
            _ => null
        };
    }
}
