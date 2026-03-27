// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services.SLA;

public class SLAConfigProvider : ISLAConfigProvider
{
    public int FqrThresholdBusinessDays => 3;

    public int BugResolutionThresholdDays => 90;

    public int QuestionResolutionThresholdDays => 14;

    public IReadOnlyList<string> DefaultRepos { get; } =
    [
        "azure-sdk-for-net",
        "azure-sdk-for-java",
        "azure-sdk-for-python",
        "azure-sdk-for-js",
        "azure-sdk-for-go",
        "azure-sdk-for-cpp",
        "azure-sdk-for-rust",
    ];

    public string RepoOwner => "Azure";

    public IReadOnlyList<string> CustomerReportedLabels { get; } = ["customer-reported"];

    public string BugLabel => "bug";

    public string QuestionLabel => "question";

    public string IssueAddressedLabel => "issue-addressed";
}
