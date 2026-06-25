// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Xml;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Parses TRX (Visual Studio Test Results) files used by .NET (dotnet test).
/// Root element is TestRun with failed results in UnitTestResult outcome="Failed";.
/// </summary>
public class TrxTestHelper : ITestHelper
{
    public string FormatName => "TRX";

    public bool CanParse(string filePath)
    {
        if (Path.GetExtension(filePath).Equals(".trx", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return HasRootElement(filePath, "TestRun");
    }

    public async Task<FailedTestRunListResponse> GetFailedTestCases(string filePath, string filterTitle = "", CancellationToken ct = default)
    {
        var failedTestRuns = await GetFailedTestResults(filePath, ct);
        failedTestRuns.Items = failedTestRuns.Items
                .Where(run => string.IsNullOrEmpty(filterTitle) || run.TestCaseTitle.Contains(filterTitle, StringComparison.OrdinalIgnoreCase))
                .Select(run => new FailedTestRunResponse
                {
                    TestCaseTitle = run.TestCaseTitle,
                    Uri = run.Uri
                })
                .ToList();
        return failedTestRuns;
    }

    public async Task<FailedTestRunResponse> GetFailedTestCaseData(string filePath, string testCaseTitle, CancellationToken ct)
    {
        var failedTestRuns = await GetFailedTestResults(filePath, ct);
        return failedTestRuns.Items.FirstOrDefault(run => run.TestCaseTitle.Equals(testCaseTitle, StringComparison.OrdinalIgnoreCase))
            ?? new FailedTestRunResponse { ResponseError = $"No failed test run found for test case title: {testCaseTitle}" };
    }

    public async Task<FailedTestRunListResponse> GetFailedTestResults(string filePath, CancellationToken ct)
    {
        var failedTestRuns = new FailedTestRunListResponse();
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"TRX file not found: {filePath}", filePath);
        }

        var doc = await XmlSafeLoader.LoadAsync(filePath, ct);
        var unitTestResults = doc.GetElementsByTagName("UnitTestResult");
        foreach (XmlNode resultNode in unitTestResults)
        {
            var outcome = resultNode.Attributes?["outcome"]?.Value ?? "";
            if (!string.Equals(outcome, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var testName = resultNode.Attributes?["testName"]?.Value ?? "";
            var errorMessage = "";
            var stackTrace = "";

            var outputNode = resultNode.ChildNodes.OfType<XmlNode>().FirstOrDefault(n => n.Name == "Output");
            if (outputNode != null)
            {
                var errorInfoNode = outputNode.ChildNodes.OfType<XmlNode>().FirstOrDefault(n => n.Name == "ErrorInfo");
                if (errorInfoNode != null)
                {
                    var messageNode = errorInfoNode.ChildNodes.OfType<XmlNode>().FirstOrDefault(n => n.Name == "Message");
                    if (messageNode != null)
                    {
                        errorMessage = messageNode.InnerText ?? "";
                    }

                    var stackTraceNode = errorInfoNode.ChildNodes.OfType<XmlNode>().FirstOrDefault(n => n.Name == "StackTrace");
                    if (stackTraceNode != null)
                    {
                        stackTrace = stackTraceNode.InnerText ?? "";
                    }
                }
            }

            failedTestRuns.Items.Add(new FailedTestRunResponse
            {
                TestCaseTitle = testName,
                ErrorMessage = errorMessage,
                StackTrace = stackTrace,
                Outcome = outcome,
                Uri = filePath
            });
        }

        return failedTestRuns;
    }

    private static bool HasRootElement(string filePath, string elementName)
    {
        try
        {
            using var reader = XmlReader.Create(filePath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    return reader.LocalName == elementName;
                }
            }
        }
        catch { }
        return false;
    }
}
