// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.Data;

namespace IssueLabeler.Shared
{
    public class GitHubIssuePrediction
    {
        public string? PredictedCategoryLabel { get; set; }
        public string? PredictedServiceLabel { get; set; }

        [ColumnName("PredictedLabel")]
        public string? PredictedLabel { get; set; }

        public float[]? Score { get; set; }
    }
}
