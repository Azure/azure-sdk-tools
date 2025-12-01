// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable 649 // We don't care about unsused fields here, because they are mapped with the input file.

using Microsoft.ML.Data;

namespace IssueLabeler.Shared
{
    public class GitHubPullRequest : GitHubIssue
    {
        public string? FileNames { get; set; }
        public string? FolderNames { get; set; }
    }

    public class GitHubIssue
    {
        public string? Label { get; set; }
        public string? CategoryLabel { get; set; }
        public string? ServiceLabel { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

        [NoColumn]
        public string[]? Labels { get; set; }
        public string[]? CategoryLabels { get; set; }
        public string[]? ServiceLabels { get; set; }
    }
}
