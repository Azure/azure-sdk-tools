// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Octokit;

namespace CreateMikLabelModel.Models
{
    public record TrainingDataItem(DateTimeOffset CreatedAt, long Identifier, string RepositoryName, string LabelName, string SegmentName, string Data)
    {
        public TrainingDataItem(string labelName, string segmentName, string repositoryName, Issue source) : this(source.CreatedAt, source.Id, repositoryName, labelName, segmentName, TrainingData.CreateTrainingData(labelName, repositoryName, source))
        {
        }

        public TrainingDataItem(string labelName, string segmentName, string repositoryName, PullRequestWithFiles source) : this(source.PullRequest.CreatedAt, source.PullRequest.Id, repositoryName, labelName, segmentName, TrainingData.CreateTrainingData(labelName, repositoryName, source))
        {
        }
    }
}
