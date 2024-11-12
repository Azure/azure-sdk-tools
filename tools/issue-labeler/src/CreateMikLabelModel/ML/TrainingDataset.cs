// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IssueLabeler.Shared;

namespace CreateMikLabelModel.ML
{
    internal static class TrainingDataset
    {
        private const int TrainingDataLineMinimum = 250;
        private const string DataSetBasicHeaders = "CombinedID\tID\tLabel\tTitle\tDescription\tAuthor\tIsPR\tNumMentions\tUserMentions";
        private const string DataSetFileHeaders = DataSetBasicHeaders + "\tFileCount\tFiles\tFilenames\tFileExtensions\tFolderNames\tFolders";

        private static readonly Regex UserMentionsExpression = new Regex(@"@[a-zA-Z0-9_//-]+", RegexOptions.Compiled);
        private static readonly DiffHelper DiffHelper = new DiffHelper();

        private static readonly Dictionary<string, int> TrainingDataIndexes = new()
        {
            { "CombinedID", 0 },
            { "ID", 1 },
            { "Label", 2 },
            { "Title", 3 },
            { "Description", 4 },
            { "Author", 5 },
            { "IsPR", 6 },
            { "FilePaths", 7 }
        };

        public static IEnumerable<string> ProcessIssueTrainingData(string trainingDataFilePath, bool includeFileColumns = false) =>
            ProcessTrainingData(trainingDataFilePath, includeFileColumns, line => line[TrainingDataIndexes["IsPR"]] != "1");

        public static IEnumerable<string> ProcessPullRequestTrainingData(string trainingDataFilePath, bool includeFileColumns = true) =>
            ProcessTrainingData(trainingDataFilePath, includeFileColumns, line => line[TrainingDataIndexes["IsPR"]] == "1");

        public static void WriteDataset(
            TrainingDataFilePaths filePaths,
            string[] dataLines)
        {
            if (dataLines.Length < TrainingDataLineMinimum)
            {
                throw new ApplicationException($"At least { TrainingDataLineMinimum } training items are needed to create a training dataset; only { dataLines.Length - 1 } are available.");
            }

            var trainingSetCount = (int)Math.Floor(dataLines.Length * 0.8);
            var validateSetCount = (int)Math.Floor(dataLines.Length * 0.1);
            var currentCount = 0;
            var currentIndex = 1;

            FileStream datasetFile;
            StreamWriter datasetWriter;

            // Create the training set.

            using (datasetFile = File.Open(Path.GetFullPath(filePaths.TrainPath), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            using (datasetWriter = new StreamWriter(datasetFile))
            {
                // Write the header.

                datasetWriter.WriteLine(dataLines[0]);

                // Write the lines that belong in the set.

                while (currentCount < trainingSetCount)
                {
                    datasetWriter.WriteLine(dataLines[currentIndex]);

                    ++currentIndex;
                    ++currentCount;
                }
            }

            // Create the validate set.

            currentCount = 0;

            using (datasetFile = File.Open(Path.GetFullPath(filePaths.ValidatePath), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            using (datasetWriter = new StreamWriter(datasetFile))
            {
                // Write the header.

                datasetWriter.WriteLine(dataLines[0]);

                // Write the lines that belong in the set.

                while (currentCount < validateSetCount)
                {
                    datasetWriter.WriteLine(dataLines[currentIndex]);

                    ++currentIndex;
                    ++currentCount;
                }
            }

            // Create the test set using all remaining data.

            using (datasetFile = File.Open(Path.GetFullPath(filePaths.TestPath), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            using (datasetWriter = new StreamWriter(datasetFile))
            {
                // Write the header.

                datasetWriter.WriteLine(dataLines[0]);

                // Write the lines that belong in the set.

                while (currentIndex < dataLines.Length)
                {
                    datasetWriter.WriteLine(dataLines[currentIndex]);
                    ++currentIndex;
                }
            }
        }

        private static IEnumerable<string> ProcessTrainingData(
            string trainingDataFilePath,
            bool includeFileColumns,
            Func<string[], bool> lineFilter)
        {
            using var dataFileStream = File.Open(Path.GetFullPath(trainingDataFilePath), FileMode.Open, FileAccess.Read, FileShare.Read);
            using var dataFileReader = new StreamReader(dataFileStream);

            // Read and validate the training data headers

            var dataHeaders = dataFileReader.ReadLine();

            if (!ValidateTrainingDataHeaders(dataHeaders))
            {
                throw new ApplicationException("The training data file was not in the expected format.");
            }

            // Emit the headers.

            yield return (includeFileColumns) ? DataSetFileHeaders : DataSetBasicHeaders;

            // Process each line of training data.

            var lineCount = 0;
            var lineBuilder = new StringBuilder();
            var line = dataFileReader.ReadLine();

            while (line != null)
            {
                var dataElements = line.Split('\t');

                // Only process the line if it is accepted by the filter.

                if (lineFilter(dataElements))
                {
                    if (!byte.TryParse(dataElements[TrainingDataIndexes["IsPR"]], out var isPrBit))
                    {
                        throw new ApplicationException($"Malformed training data for line '{ lineCount + 1 }'. The 'IsPR' flag could not be parsed.");
                    }

                    if ((isPrBit < 0) || (isPrBit > 1))
                    {
                        throw new ApplicationException($"Malformed training data for line '{ lineCount + 1 }'. The 'IsPR' flag has an invalid value: '{ isPrBit }'  It should be either 0 or 1.");
                    }

                    var mentions = GetUserMentions(dataElements[TrainingDataIndexes["Description"]]);

                    lineBuilder
                        .Append(dataElements[TrainingDataIndexes["CombinedID"]])
                        .Append('\t')
                        .Append(dataElements[TrainingDataIndexes["ID"]])
                        .Append('\t')
                        .Append(dataElements[TrainingDataIndexes["Label"]])
                        .Append('\t')
                        .Append(dataElements[TrainingDataIndexes["Title"]])
                        .Append('\t')
                        .Append(dataElements[TrainingDataIndexes["Description"]])
                        .Append('\t')
                        .Append(dataElements[TrainingDataIndexes["Author"]])
                        .Append('\t')
                        .Append(isPrBit)
                        .Append('\t')
                        .Append(mentions.Length)
                        .Append('\t')
                        .Append(string.Join(' ', mentions));

                    if (includeFileColumns)
                    {
                        var filePaths = TrainingData.SplitFilePaths(dataElements[TrainingDataIndexes["FilePaths"]] ?? string.Empty)
                            .Where(path => !string.IsNullOrWhiteSpace(path))
                            .ToArray();

                        AddFileInformationToLine(lineBuilder, filePaths, (isPrBit == 1));
                    }

                    // Emit the current line.

                    yield return lineBuilder.ToString();
                }

                // Reset state for the next iteration.

                lineBuilder.Clear();
                line = dataFileReader.ReadLine();

                ++lineCount;
            }
        }

        private static string[] GetUserMentions(string description) =>
            UserMentionsExpression
                .Matches(description)
                .Select(match => match.Value)
                .ToArray();

        private static void AddFileInformationToLine(
            StringBuilder lineBuilder,
            string[] filePaths,
            bool isPullRequest)
        {
            // If the line is not being added for a pull request or there were no files, then file
            // information will not be included.  Add empty placeholder slugs and take no further action.

            if ((!isPullRequest) || filePaths.Length == 0)
            {
                lineBuilder
                    .Append('\t')
                    .Append(0)
                    .Append('\t', 5);

                return;
            }

            var segmentedDiff = DiffHelper.SegmentDiff(filePaths);

            lineBuilder
                .Append('\t')
                .Append(string.Join(' ', filePaths))
                .Append('\t')
                .Append(string.Join(' ', segmentedDiff.Filenames))
                .Append('\t')
                .Append(string.Join(' ', segmentedDiff.Extensions))
                .Append('\t')
                .Append(string.Join(' ', segmentedDiff.FolderNames))
                .Append('\t')
                .Append(string.Join(' ', segmentedDiff.Folders));
        }

        private static bool ValidateTrainingDataHeaders(string headerLine)
        {
            var index = 0;

            foreach (var header in headerLine.Split('\t'))
            {
                if (TrainingDataIndexes[header] != index)
                {
                    return false;
                }

                ++index;
            }

            return true;
        }

    }
}
