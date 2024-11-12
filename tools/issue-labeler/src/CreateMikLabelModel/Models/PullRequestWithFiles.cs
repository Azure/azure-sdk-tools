// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
