// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using CreateMikLabelModel;
using CreateMikLabelModel.ML;

namespace Azure.Sdk.LabelTrainer
{
    /// <summary>
    ///   Provides functionality related to training label models, including building and curating the
    ///   sets of data needed to do so.
    /// </summary>
    ///
    public  class AzureSdkCombinedLabelModelTrainer : LabelModelTrainer
    {
        /// <summary>The set of core Azure SDK language repositories which should be used for training the combined model.</summary>
        private static readonly string[] AzureSdkLanguageRepositories = new[]
        {
            "Azure/azure-sdk-for-net",
            "Azure/azure-sdk-for-java",
            "Azure/azure-sdk-for-python",
            "Azure/azure-sdk-for-js",
            "Azure/azure-sdk-for-go",
            "Azure/azure-sdk-for-cpp",
            "Azure/azure-sdk-for-rust",
        };

        /// <summary>
        ///   Initializes a new instance of the <see cref="AzureSdkCombinedLabelModelTrainer"/> class.
        /// </summary>
        ///
        /// <param name="logger">The logging implementation to use for emitting messages.</param>
        ///
        public AzureSdkCombinedLabelModelTrainer(ILogger logger) : base("Azure/azure-sdk", logger)
        {
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
        public override Task<IDictionary<string, TrainingDataSegment>> QueryTrainingData(
            string gitHubAccessToken,
            string trainingDataBasePath,
            TrainingDataProcessor processor = default,
            TrainingDataFilters filters = default) => QueryTrainingData(gitHubAccessToken, trainingDataBasePath, AzureSdkLanguageRepositories, processor, filters);
    }
}
