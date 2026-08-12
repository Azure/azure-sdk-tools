// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

public interface ITestHelper
{
    /// <summary>
    /// Human-readable name for the format this parser handles (e.g., "TRX", "JUnit XML").
    /// </summary>
    string FormatName { get; }

    /// <summary>
    /// Returns true if this parser can handle the given file.
    /// </summary>
    Task<bool> CanParseAsync(string filePath, CancellationToken ct = default);

    Task<FailedTestRunListResponse> GetFailedTestCases(string filePath, string filterTitle = "", CancellationToken ct = default);
    Task<FailedTestRunResponse> GetFailedTestCaseData(string filePath, string testCaseTitle, CancellationToken ct);
    Task<FailedTestRunListResponse> GetFailedTestResults(string filePath, CancellationToken ct);
}
