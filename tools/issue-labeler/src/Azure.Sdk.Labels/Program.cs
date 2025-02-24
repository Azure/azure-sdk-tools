// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CreateMikLabelModel;
using IssueLabeler.Shared;

namespace Azure.Sdk.LabelTrainer
{
    /// <summary>
    ///   Serves as the main entry point for the application.
    /// </summary>
    ///
    public static class Program
    {
        /// <summary>The file to write <see cref="Trace" /> output to; the current directory is assumed.</summary>
        private const string TraceLogFilename = "trace.log";

        /// <summary>
        ///   This utility will train a set of machine learning models intended to help with prediction of the
        ///   labels that should be added to GitHub items for basic categorization and routing.
        /// </summary>
        ///
        /// <param name="repository">The full path for the repository to train.</param>
        /// <param name="gitHubToken">The access token to use for interacting with GitHub.</param>
        /// <param name="dataFileDirectory">[OPTIONAL] The directory in which to keep the data files; if not specified, the current directory will be assumed.  If specified, the directory will be created if it does not exist.</param>
        ///
        /// <example>
        ///   <code>
        ///     dotnet run -- --repository "Azure/azure-sdk-for-net" --git-hub-token "[[ TOKEN ]]"
        ///   </code>
        /// </example>
        ///
        /// <example>
        ///   <code>
        ///     dotnet run -- --repository "Azure/azure-sdk-for-net" --git-hub-token "[[ TOKEN ]]" --data-file-directory "c:\data\training"
        ///   </code>
        /// </example>
        ///
        public static async Task<int> Main(string repository, string gitHubToken, string dataFileDirectory = default)
        {
            if ((string.IsNullOrEmpty(repository)) || (string.IsNullOrEmpty(gitHubToken)))
            {
                Console.WriteLine("");
                Console.WriteLine("The repository path and GitHub access token must be specified.");
                Console.WriteLine("");
                Console.WriteLine("Usage:");
                Console.WriteLine("\tdotnet run -- --repository \"all\" --git-hub-token \"[[ TOKEN ]]\"");
                Console.WriteLine("\tdotnet run -- --repository \"Azure/azure-sdk-for-net\" --git-hub-token \"[[ TOKEN ]]\"");
                Console.WriteLine("\tdotnet run -- --repository \"Azure/azure-sdk-for-js\" --git-hub-token \"[[ TOKEN ]]\" --data-file-directory \"c:\\data\\training\"");
                Console.WriteLine("");

                return -1;
            }

            // Ensure the path for training data.

            dataFileDirectory = string.IsNullOrEmpty(dataFileDirectory)
                ? Environment.CurrentDirectory
                : dataFileDirectory;

            if (!Directory.Exists(dataFileDirectory))
            {
                Directory.CreateDirectory(dataFileDirectory);
            }

            // Build the set of training data.

            var logger = new ConsoleLogger();

            var trainer = repository switch
            {
                "all" => new AzureSdkCombinedLabelModelTrainer(logger),
                _ => new LabelModelTrainer(repository, logger)
            };

            // Step 1: Download the common set of training items and use them to prepare a training data set.  This will include
            // all segments for the different label types needed.

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(new String('=', 80));
            Console.WriteLine("  Preparing training data");
            Console.WriteLine(new String('=', 80));
            Console.ResetColor();

            var filters = new AzureSdkTrainingDataFilters();
            var processor = new AzureSdkTrainingDataProcessor(logger);
            var trainingDataFiles = await trainer.QueryTrainingData(gitHubToken, dataFileDirectory, processor, filters).ConfigureAwait(false);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(new String('=', 80));
            Console.WriteLine("  Training data preparation complete.");
            Console.WriteLine(new String('=', 80));
            Console.ResetColor();

            // Each segment will produce an dedicated set of models for that specific label type; process each separately.

            foreach (var trainingSegment in trainingDataFiles)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(new String('=', 80));
                Console.WriteLine($"  Processing segment: { trainingSegment.Key }");
                Console.WriteLine(new String('=', 80));
                Console.ResetColor();

                // Step 2: Translate the training data into

                trainer.GenerateTrainingDatasets(trainingSegment.Value);
                Console.WriteLine();

                // Step 3: Train the model.

                trainer.TrainModels(trainingSegment.Value);
                Console.WriteLine();

                // Step 4: Test the model.

                trainer.TestModels(trainingSegment.Value);

                // Provide information on where the model files are.

                Console.WriteLine();
                Console.WriteLine();

                if (!trainingSegment.Value.Issues.SkipProcessing)
                {
                    Console.WriteLine($"Final issue model: '{ trainingSegment.Value.Issues.FinalModelPath }'");
                }

                if (!trainingSegment.Value.PullRequests.SkipProcessing)
                {
                    Console.WriteLine($"Final pull request model: '{ trainingSegment.Value.PullRequests.FinalModelPath }'");
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(new String('=', 80));
                Console.WriteLine($"  Segment: { trainingSegment.Key } complete.");
                Console.WriteLine(new String('=', 80));
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("==== Training complete ====");
            return 0;
        }
    }
}