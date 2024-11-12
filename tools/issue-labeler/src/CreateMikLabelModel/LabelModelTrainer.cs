// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CreateMikLabelModel.ML;
using CreateMikLabelModel.Models;

namespace CreateMikLabelModel
{
    /// <summary>
    ///   Provides functionality related to training label models, including building anc curating the
    ///   sets of data needed to do so.
    /// </summary>
    ///
    public class LabelModelTrainer
    {
        private ILogger _logger;

        /// <summary>
        ///   The repository that the trainer is associated with.
        /// </summary>
        ///
        /// <value>
        ///   The full path of the repository, including the owner name.  For
        ///   example, "Azure/azure-sdk-for-net".
        /// </value>
        ///
        public string RepositoryPath { get; init; }

        /// <summary>
        ///   Initializes a new instance of the <see cref="LabelModelTrainer"/> class.
        /// </summary>
        ///
        /// <param name="repositoryPath">The repository path to associate the training with.</param>
        /// <param name="logger">The logging implementation to use for emitting messages.</param>
        ///
        public LabelModelTrainer(string repositoryPath, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrWhiteSpace(repositoryPath))
            {
                throw new ArgumentNullException(nameof(repositoryPath));
            }

            RepositoryPath = repositoryPath;
        }

        /// <summary>
        ///   Queries repository items for data to use for training the models.
        /// </summary>
        ///
        /// <param name="gitHubAccessToken">The access token to use for the GitHub API.</param>
        /// <param name="trainingDataBasePath">The base path to use for storing and querying training data.</param>
        /// <param name="processor">The processor for preparing training set items from repository issues and pull requests.</param>
        /// <param name="filters">The set of filters to apply to data when building the training set.  If not provided, training items will not be filtered.</param>
        ///
        /// <returns>The set of <see cref="TrainingDataSegment" /> that were produced.</returns>
        ///
        public virtual Task<IDictionary<string, TrainingDataSegment>> QueryTrainingData(
            string gitHubAccessToken,
            string trainingDataBasePath,
            TrainingDataProcessor processor = default,
            TrainingDataFilters filters = default) => QueryTrainingData(gitHubAccessToken, trainingDataBasePath, new[] { RepositoryPath }, processor, filters);

        /// <summary>
        ///   Queries repository items for data to use for training the models.
        /// </summary>
        ///
        /// <param name="gitHubAccessToken">The access token to use for the GitHub API.</param>
        /// <param name="trainingDataBasePath">The base path to use for storing and querying training data.</param>
        /// <param name="trainingRepositoryGroup">The group of repositories to include in this training set.</param>
        /// <param name="processor">The processor for preparing training set items from repository issues and pull requests.</param>
        /// <param name="filters">The set of filters to apply to data when building the training set.  If not provided, training items will not be filtered.</param>
        ///
        /// <returns>The set of <see cref="TrainingDataSegment" /> that were produced.</returns>
        ///
        public virtual async Task<IDictionary<string, TrainingDataSegment>> QueryTrainingData(
            string gitHubAccessToken,
            string trainingDataBasePath,
            string[] trainingRepositoryGroup,
            TrainingDataProcessor processor = default,
            TrainingDataFilters filters = default)
        {
            if (gitHubAccessToken is { Length: 0 })
            {
                throw new ArgumentException("GitHub access token is required.", nameof(gitHubAccessToken));
            }

            if (trainingDataBasePath is { Length: 0 })
            {
                throw new ArgumentException("The base path for storing training data is required.", nameof(gitHubAccessToken));
            }

            if ((!Directory.Exists(trainingDataBasePath)) || (!ValidateWriteAccess(trainingDataBasePath)))
            {
                throw new ArgumentException("Either the directory does not exist or cannot be written to.", nameof(trainingDataBasePath));
            }

            if (trainingRepositoryGroup is { Length: 0 })
            {
                throw new ArgumentException("The repository group is required and should contain at least one item.", nameof(trainingRepositoryGroup));
            }

            // If no explicit processor or filters were requested, accept all items as valid for the training set.

            processor ??= new TrainingDataProcessor(_logger);
            filters ??= new TrainingDataFilters();

            _logger.LogInformation($"Preparing the training set for '{ RepositoryPath }'.");

            var stopWatch = Stopwatch.StartNew();
            var trainingSetItemCount = 0;
            var repositoryInformation = RepositoryInformation.Parse(RepositoryPath);
            var trainingItemClient = new TrainingDataClient(gitHubAccessToken, _logger);
            var trainingItems = new Dictionary<string, List<TrainingDataItem>>();

            try
            {
                // Process issues, if they are to be included.

                if (filters.IncludeIssues)
                {
                    await foreach (var trainingItem in processor.PrepareData(trainingItemClient.GetIssuesAsync(trainingRepositoryGroup, filters), repositoryInformation.Name))
                    {
                        if (!trainingItems.ContainsKey(trainingItem.SegmentName))
                        {
                            trainingItems.Add(trainingItem.SegmentName, new List<TrainingDataItem>());
                        }

                        trainingItems[trainingItem.SegmentName].Add(trainingItem);
                        ++trainingSetItemCount;
                    }
                }

                // Process pull requests, if they are to be included.

                if (filters.IncludePullRequests)
                {
                    await foreach (var trainingItem in processor.PrepareData(trainingItemClient.GetPullRequestsAsync(trainingRepositoryGroup, filters), repositoryInformation.Name))
                    {
                        if (!trainingItems.ContainsKey(trainingItem.SegmentName))
                        {
                            trainingItems.Add(trainingItem.SegmentName, new List<TrainingDataItem>());
                        }

                        trainingItems[trainingItem.SegmentName].Add(trainingItem);
                        ++trainingSetItemCount;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("The training set was not able to be successfully prepared.", ex);
            }

            stopWatch.Stop();
            _logger.LogInformation($"Done downloading data for training items in {stopWatch.Elapsed.TotalSeconds:0.00} seconds.");

            // With the data downloaded and prepared, write the training set data for each segment.

            _logger.LogInformation($"Writing out training data files for '{ RepositoryPath }'.");

            stopWatch.Restart();
            var trainingFiles = new Dictionary<string, TrainingDataSegment>(trainingItems.Keys.Count);

            try
            {
                foreach (var segment in trainingItems)
                {
                    var segmentFiles = CreateTrainingFilesForSegment(repositoryInformation, segment.Key, trainingDataBasePath, filters);
                    trainingFiles.Add(segment.Key, segmentFiles);

                    using var outputWriter = new StreamWriter(segmentFiles.Issues.InputPath);
                    TrainingData.WriteHeader(outputWriter);
                    TrainingData.WriteTrainingItems(segment.Value, outputWriter);
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("The training data files were not able to be successfully written.", ex);
            }

            stopWatch.Stop();
            _logger.LogInformation($"Done writing training data files in {stopWatch.Elapsed.TotalSeconds:0.00} seconds.");

            // Return the segments and associated files.

            return trainingFiles;
        }

        /// <summary>
        ///   Generates the training datasets for issues and pull requests, writing out
        ///   the necessary files to the paths specified by the <paramref name="trainingFiles" />.
        /// </summary>
        ///
        /// <param name="trainingFiles">The locations of the files, both input and output, associated with training datasets.</param>
        ///
        public void GenerateTrainingDatasets(TrainingDataSegment trainingFiles)
        {
            // Generate the dataset for issues.

            _logger.LogInformation("Generating the datasets for issues...");

            var stopWatch = Stopwatch.StartNew();

            if (!trainingFiles.Issues.SkipProcessing)
            {
                var issueData = TrainingDataset.ProcessIssueTrainingData(trainingFiles.Issues.InputPath).ToArray();

                // There is always a header line present; if there are no other lines, then there was no
                // issue data.

                if (issueData.Length > 1)
                {
                    TrainingDataset.WriteDataset(trainingFiles.Issues, issueData);
                    _logger.LogInformation($"{ issueData.Length } issues were included in the datasets.");
                }
                else
                {
                    _logger.LogInformation("No issue data was available for use in the datasets.");
                }
            }
            else
            {
                _logger.LogInformation("Issues were configured to be excluded from the datasets; no issue data was used.");
            }

            stopWatch.Stop();
            _logger.LogInformation($"Issue datasets are complete in {stopWatch.Elapsed.TotalSeconds:0.00} seconds.");

            // Generate the dataset for pull requests.

            _logger.LogInformation("Generating the datasets for pull requests...");
            stopWatch.Restart();

            if (!trainingFiles.PullRequests.SkipProcessing)
            {
                var pullRequestData = TrainingDataset.ProcessPullRequestTrainingData(trainingFiles.PullRequests.InputPath).ToArray();

                // There is always a header line present; if there are no other lines, then there was no
                // pull request data.

                if (pullRequestData.Length > 1)
                {
                    TrainingDataset.WriteDataset(trainingFiles.PullRequests, pullRequestData);
                    _logger.LogInformation($"{ pullRequestData.Length } pull requests were included in the datasets.");
                }
                else
                {
                    _logger.LogInformation("No pull request data was available for use in the datasets.");
                }
            }
            else
            {
                _logger.LogInformation("Pull requests were configured to be excluded from the datasets; no pull request data was used.");
            }

            stopWatch.Stop();
            _logger.LogInformation($"Pull request datasets are complete in {stopWatch.Elapsed.TotalSeconds:0.00} seconds.");
        }

        /// <summary>
        ///   Trains the machine learning models, using the previously prepared training datasets
        ///   identified by the paths specified in the specified<paramref name="trainingFiles" />.
        /// </summary>
        ///
        /// <param name="trainingFiles">The locations of the files for training datasets to be used for training the ML models.</param>
        ///
        public void TrainModels(TrainingDataSegment trainingFiles)
        {
            var mlHelper = new MLHelper(_logger);
            var stopWatch = Stopwatch.StartNew();

            if (!trainingFiles.Issues.SkipProcessing)
            {
                _logger.LogInformation("Training the models for issues...");
                mlHelper.Train(trainingFiles.Issues, false);
            }
            else
            {
                _logger.LogInformation("Issues were configured to be excluded from the training; no issue data trained.");
            }

            if (!trainingFiles.PullRequests.SkipProcessing)
            {
                _logger.LogInformation("Training the models for pull requests...");
                mlHelper.Train(trainingFiles.PullRequests, true);
            }
            else
            {
                _logger.LogInformation("Pull requests were configured to be excluded from the training; no pull request data was trained.");
            }

            stopWatch.Stop();
            _logger.LogInformation($"Model training complete in {stopWatch.Elapsed.TotalSeconds:0.00} seconds.");
        }

        /// <summary>
        ///   Tests the previously trained machine learning models identified by the paths specified in
        ///   the specified<paramref name="trainingFiles" />.
        /// </summary>
        ///
        /// <param name="trainingFiles">The locations of the files for training datasets to be used for training the ML models.</param>
        ///
        public void TestModels(TrainingDataSegment trainingFiles)
        {
            var mlHelper = new MLHelper(_logger);
            var stopWatch = Stopwatch.StartNew();

            if (!trainingFiles.Issues.SkipProcessing)
            {
                _logger.LogInformation("Testing the models for issues...");
                mlHelper.Test(trainingFiles.Issues, false);
            }

            if (!trainingFiles.PullRequests.SkipProcessing)
            {
                _logger.LogInformation("Testing the models for pull requests...");
                mlHelper.Test(trainingFiles.PullRequests, true);
            }

            stopWatch.Stop();
            _logger.LogInformation($"Model testing complete in {stopWatch.Elapsed.TotalSeconds:0.00} seconds.");
        }

        private static bool ValidateWriteAccess(string path)
        {
            try
            {
                using var file = File.Create(Path.Combine(path, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose);
                file.Close();

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static TrainingDataSegment CreateTrainingFilesForSegment(
            RepositoryInformation repository,
            string segmentName,
            string trainingDataBasePath,
            TrainingDataFilters filters)
        {
            var prefix = $"{ repository.Owner }-{ repository.Name }-{segmentName }";

            return new TrainingDataSegment(
                new TrainingDataFilePaths(trainingDataBasePath, prefix, forPrs: false, skip: !filters.IncludeIssues),
                new TrainingDataFilePaths(trainingDataBasePath, prefix, forPrs: true, skip: !filters.IncludePullRequests));
        }
    }
}
