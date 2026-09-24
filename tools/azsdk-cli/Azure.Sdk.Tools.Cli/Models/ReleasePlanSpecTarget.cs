// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

public class ReleasePlanSpecTarget
{
    public string TypeSpecProjectPath { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public string SpecCommitSHA { get; set; } = string.Empty;
    public string SpecPullRequestUrl { get; set; } = string.Empty;
    public string CommitUrl { get; set; } = string.Empty;
    public string SDKReleaseType { get; set; } = string.Empty;
    public string? ExpectedPreviousSpecCommitSHA { get; set; }
    public bool IsSpecMerged { get; set; }
    public List<string> AvailableApiVersions { get; set; } = [];
    public List<PackageInfo> Packages { get; set; } = [];

    public override string ToString() =>
        $"Project: {TypeSpecProjectPath}\nAPI version: {ApiVersion}\nSDK release type: {SDKReleaseType}\nSpec PR: {SpecPullRequestUrl}\n" +
        $"Spec commit SHA: {SpecCommitSHA}\nSpec commit: {CommitUrl}\nSpec merged: {IsSpecMerged}\n" +
        $"Expected previous spec commit: {ExpectedPreviousSpecCommitSHA ?? "not applicable (new plan)"}\n" +
        $"Packages: {string.Join(", ", Packages.Select(p => p.PackageName))}\nAvailable API versions: {string.Join(", ", AvailableApiVersions)}";
}
