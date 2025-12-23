// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace IssueLabeler.Shared
{
    public interface IPredictor
    {
        Task<List<ScoredLabel>> Predict(GitHubIssue issue);
        Task<List<ScoredLabel>> Predict(GitHubPullRequest issue);
    }
}
