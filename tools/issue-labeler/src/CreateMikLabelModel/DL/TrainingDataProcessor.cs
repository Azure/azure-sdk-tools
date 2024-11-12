// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using CreateMikLabelModel.Models;
using Octokit;

namespace CreateMikLabelModel
{
    /// <summary>
    ///   The processor responsible for preparing training set items from the
    ///   raw repository data.
    /// </summary>
    ///
    public class TrainingDataProcessor
    {
        /// <summary>The name of the default training segment.</summary>
        public const string DefaultSegmentName = "Default";

        /// <summary>
        ///   The logger to use for reporting information as items are prepared.
        ///   </summary>
        ///
        protected ILogger Logger { get; init; }

        /// <summary>
        ///   Initializes a new instance of the <see cref="TrainingDataProcessor"/> class.
        /// </summary>
        ///
        /// <param name="logger">The logger to use for reporting information as items are prepared.</param>
        ///
        public TrainingDataProcessor(ILogger logger) => Logger = logger;

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
        public virtual async IAsyncEnumerable<TrainingDataItem> PrepareData(
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
                       ++itemCount;
                       yield return new TrainingDataItem(label.Name, DefaultSegmentName, repositoryName, issue);
                   }
               }
               else
               {
                   ++itemCount;
                   yield return new TrainingDataItem(null, DefaultSegmentName, repositoryName, issue);
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
        public virtual async IAsyncEnumerable<TrainingDataItem> PrepareData(
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
                        ++itemCount;
                        yield return new TrainingDataItem(label.Name, DefaultSegmentName, repositoryName, pullRequest);
                    }
                }
                else
                {
                    ++itemCount;
                    yield return new TrainingDataItem(null, DefaultSegmentName, repositoryName, pullRequest);
                }
            }

            Logger.LogInformation($"Prepared { itemCount } training set items from pull request training data.");
        }
    }
}
