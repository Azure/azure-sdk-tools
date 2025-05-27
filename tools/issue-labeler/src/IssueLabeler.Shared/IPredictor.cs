// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace IssueLabeler.Shared
{
    public interface IPredictor
    {
        Task<LabelSuggestion> Predict(GitHubIssue issue);
        Task<LabelSuggestion> Predict(GitHubPullRequest issue);
        public string ModelName { get; set; }
    }
}
