// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

public class ReleasePlanSpecTarget
{
    public string TypeSpecProjectPath { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public string SpecCommitSHA { get; set; } = string.Empty;
    public string SpecPullRequestUrl { get; set; } = string.Empty;
    // Internal optimistic-concurrency check, captured by the tool before metadata is read.
    public string? ExpectedTargetRevision { get; set; }
    public List<PackageInfo> Packages { get; set; } = [];
}
