// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.AutoML;
using System;
using System.Collections.Generic;

namespace CreateMikLabelModel.ML
{
    public struct ExperimentModifier
    {
        public ExperimentModifier(TrainingDataFilePaths paths, bool forPrs)
        {
            // set all to defaults:
            ColumnSetup = (columnInformation, forPrs) =>
            {
                // Customize column information returned by InferColumns API
                columnInformation.CategoricalColumnNames.Clear();
                columnInformation.NumericColumnNames.Clear();
                columnInformation.IgnoredColumnNames.Clear();
                columnInformation.TextColumnNames.Clear();

                // NOTE: depending on how the data changes over time this might need to get updated too.
                // Only the Title and Description are needed, but since we are PreFeaturizing them we can
                // ignore them here.
                columnInformation.IgnoredColumnNames.Add("Title");
                columnInformation.IgnoredColumnNames.Add("Description");
                columnInformation.IgnoredColumnNames.Add("Author");
                columnInformation.IgnoredColumnNames.Add("IsPR");
                columnInformation.IgnoredColumnNames.Add("NumMentions");
                columnInformation.IgnoredColumnNames.Add("UserMentions");
                columnInformation.IgnoredColumnNames.Add("ID");
                columnInformation.IgnoredColumnNames.Add("CombinedID");

                if (forPrs)
                {
                    columnInformation.NumericColumnNames.Add("FileCount");
                    columnInformation.IgnoredColumnNames.Add("Files");
                    columnInformation.TextColumnNames.Add("FolderNames");
                    columnInformation.IgnoredColumnNames.Add("Folders");
                    columnInformation.IgnoredColumnNames.Add("FileExtensions");
                    columnInformation.TextColumnNames.Add("Filenames");
                }
            };

            TrainerSetup = (trainers) =>
            {
                trainers.Clear();
                if (forPrs)
                {
                    trainers.Add(MulticlassClassificationTrainer.SdcaMaximumEntropy);
                    trainers.Add(MulticlassClassificationTrainer.FastTreeOva);
                }
                else
                {
                    trainers.Add(MulticlassClassificationTrainer.SdcaMaximumEntropy);
                    // trainers.Add(MulticlassClassificationTrainer.LinearSupportVectorMachinesOva);
                    //trainers.Add(MulticlassClassificationTrainer.LightGbm);
                }
            };

            ExperimentTime = 300;
            LabelColumnName = "Label";
            ForPrs = forPrs;
            Paths = paths;
        }

        public ExperimentModifier(
            bool forPrs,
            uint experimentTime,
            string labelColumnName,
            TrainingDataFilePaths paths,
            Action<ColumnInformation, bool> columnSetup,
            Action<ICollection<MulticlassClassificationTrainer>> trainerSetup)
        {
            ForPrs = forPrs;
            ExperimentTime = experimentTime;
            LabelColumnName = labelColumnName;
            Paths = paths;
            ColumnSetup = columnSetup;
            TrainerSetup = trainerSetup;
        }

        public readonly uint ExperimentTime;
        public readonly string LabelColumnName;
        public readonly Action<ColumnInformation, bool> ColumnSetup;
        public readonly Action<ICollection<MulticlassClassificationTrainer>> TrainerSetup;
        public readonly bool ForPrs;
        public readonly TrainingDataFilePaths Paths;
    }
}
