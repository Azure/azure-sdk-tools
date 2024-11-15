// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using CreateMikLabelModel;
using CreateMikLabelModel.Models;
using Octokit;

namespace Azure.Sdk.LabelTrainer
{
    internal class AzureSdkTrainingDataFilters : TrainingDataFilters
    {
        private static readonly string[] AzureSdkRequiredIssueLabelNames = new[] { "customer-reported" };
        private static readonly string[] AzureSdkRequiredPullRequestLabelNames = Array.Empty<string>();

        public AzureSdkTrainingDataFilters() : base(
            includeIssues: true,
            includePullRequests: false,
            requiredIssueLabelNames: AzureSdkRequiredIssueLabelNames,
            requiredPullRequestLabelNames: AzureSdkRequiredPullRequestLabelNames)
        {
        }

        public override bool PullRequestFilter(PullRequestWithFiles pullRequest) => false;

        public override bool IssueFilter(Issue issue)
        {
            var categoryCount = 0;
            var serviceCount = 0;

            if (RequiredIssueLabelNames.All(required => issue.Labels.Any(label => label.Name == required)))
            {
                foreach (var label in issue.Labels)
                {
                    if (AzureSdkLabel.IsServiceLabel(label))
                    {
                        ++serviceCount;
                    }

                    if (AzureSdkLabel.IsCategoryLabel(label))
                    {
                        ++categoryCount;
                    }
                }
            }

            // To be eligible for the training set, the issue must have all of the required
            // labels and exactly one service label and one category label.  Issues that have
            // multiples, even if valid, aren't appropriate for training purposes.

            return (categoryCount == 1 && serviceCount == 1);
        }

        public override bool LabelFilter(Label label) =>
            AzureSdkLabel.IsServiceLabel(label) || AzureSdkLabel.IsCategoryLabel(label);
    }
}
