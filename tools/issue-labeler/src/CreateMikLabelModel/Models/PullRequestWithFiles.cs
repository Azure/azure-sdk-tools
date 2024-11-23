// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Octokit;

namespace CreateMikLabelModel.Models
{
    public class PullRequestWithFiles
    {
        public PullRequest PullRequest { get; init; }
        public string[] FilePaths { get; init; }
        public PullRequestWithFiles(PullRequest pullRequest, string[] filePaths) => (PullRequest, FilePaths) = (pullRequest, filePaths);
    }
}
