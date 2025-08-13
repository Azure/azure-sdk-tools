// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Octokit;

namespace Azure.Sdk.Tools.Cli.Models
{
    public class PullRequestDetails
    {
        public int pullRequestNumber { get; set; } = 0;
        public string Author { get; set; } = string.Empty;
        public List<Label> Labels { get; set; } = [];
        public string Status { get; set; } = string.Empty;
        public string AssignedTo {  get; set; } = string.Empty;
        public string Url {  get; set; } = string.Empty;
        public bool IsMerged { get; set; } = false;
        public bool IsMergeable { get; set; } = false;
        public List<string> Checks { get; set; } = [];
        public List<string> Comments { get; set; } = [];
        public List<ApiviewData> ApiViews { get; set; } = [];
    }
}
