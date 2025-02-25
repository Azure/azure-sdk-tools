// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;

namespace CreateMikLabelModel.ML
{
    public readonly struct TrainingDataFilePaths
    {
        public TrainingDataFilePaths(string folder, string commonPrefix, bool forPrs, bool skip) : this(folder, commonPrefix, string.Empty, forPrs, skip)
        {
        }

        public TrainingDataFilePaths(string folder, string commonPrefix, string modelPrefix, bool forPrs, bool skip)
        {
            Folder = folder;
            SkipProcessing = skip;
            InputPath = Path.Combine(Folder, commonPrefix + "-IssueAndPrData.tsv");
            var prefix = forPrs ? "-only-prs" : "-only-issues";

            TrainPath = Path.Combine(Folder, commonPrefix + prefix + "-part1.tsv");
            ValidatePath = Path.Combine(Folder, commonPrefix + prefix + "-part2.tsv");
            TestPath = Path.Combine(Folder, commonPrefix + prefix + "-part3.tsv");
            ModelPath = Path.Combine(Folder, commonPrefix + prefix + modelPrefix + "-model.zip");
            FittedModelPath = Path.Combine(Folder, commonPrefix + prefix + modelPrefix + "-fitted-model.zip");
            FinalModelPath = Path.Combine(Folder, commonPrefix + prefix + modelPrefix + "-final-model.zip");
        }

        public readonly string Folder;
        public readonly bool SkipProcessing;
        public readonly string TrainPath;
        public readonly string ValidatePath;
        public readonly string TestPath;
        public readonly string ModelPath;
        public readonly string FittedModelPath;
        public readonly string FinalModelPath;
        public readonly string InputPath;
    }
}
