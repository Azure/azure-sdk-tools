// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services.SLA;

public interface ISLAConfigProvider
{
    /// <summary>
    /// FQR SLA threshold in business days.
    /// </summary>
    int FqrThresholdBusinessDays { get; }

    /// <summary>
    /// Bug resolution SLA threshold in calendar days.
    /// </summary>
    int BugResolutionThresholdDays { get; }

    /// <summary>
    /// Question resolution SLA threshold in calendar days.
    /// </summary>
    int QuestionResolutionThresholdDays { get; }

    /// <summary>
    /// Default list of Azure SDK repos to query when no specific repo is provided.
    /// </summary>
    IReadOnlyList<string> DefaultRepos { get; }

    /// <summary>
    /// The GitHub org owner for Azure SDK repos.
    /// </summary>
    string RepoOwner { get; }

    /// <summary>
    /// Labels that identify customer-reported issues.
    /// </summary>
    IReadOnlyList<string> CustomerReportedLabels { get; }

    /// <summary>
    /// Label that identifies bug issues.
    /// </summary>
    string BugLabel { get; }

    /// <summary>
    /// Label that identifies question issues.
    /// </summary>
    string QuestionLabel { get; }

    /// <summary>
    /// Label that indicates an issue has been addressed.
    /// </summary>
    string IssueAddressedLabel { get; }
}
