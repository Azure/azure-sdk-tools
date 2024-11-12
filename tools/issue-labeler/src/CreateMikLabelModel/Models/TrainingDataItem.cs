// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
