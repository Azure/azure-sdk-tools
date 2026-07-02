// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Xml;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Parses JUnit XML test result files used by Java (Maven Surefire), Python (pytest --junitxml),
/// JavaScript/TypeScript (vitest junit reporter), and Go (go-junit-report).
/// See: https://github.com/testmoapp/junitxml for the JUnit XML format specification.
/// </summary>
public class JUnitTestHelper : ITestHelper
{
    public string FormatName => "JUnit XML";

    public async Task<bool> CanParseAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            using var reader = XmlSafeLoader.CreateReader(filePath);
            while (await reader.ReadAsync())
            {
                ct.ThrowIfCancellationRequested();
                if (reader.NodeType == XmlNodeType.Element)
                {
                    return reader.LocalName is "testsuites" or "testsuite";
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch { }
        return false;
    }

    public async Task<FailedTestRunListResponse> GetFailedTestCases(string filePath, string filterTitle = "", CancellationToken ct = default)
    {
        var failedTestRuns = await GetFailedTestRunDataFromJUnit(filePath, ct);
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
        var failedTestRuns = await GetFailedTestRunDataFromJUnit(filePath, ct);
        return failedTestRuns.Items.FirstOrDefault(run => run.TestCaseTitle.Equals(testCaseTitle, StringComparison.OrdinalIgnoreCase))
            ?? new FailedTestRunResponse { ResponseError = $"No failed test run found for test case title: {testCaseTitle}" };
    }
    
    public Task<FailedTestRunListResponse> GetFailedTestResults(string filePath, CancellationToken ct)
    {
        return GetFailedTestRunDataFromJUnit(filePath, ct);
    }

    private async Task<FailedTestRunListResponse> GetFailedTestRunDataFromJUnit(string filePath, CancellationToken ct)
    {
        var failedTestRuns = new FailedTestRunListResponse();
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"JUnit XML file not found: {filePath}", filePath);
        }

        var doc = await XmlSafeLoader.LoadAsync(filePath, ct);

        // JUnit XML can have <testsuites> or <testsuite> as root, with <testcase> elements nested inside.
        // A test case has failed if it contains a <failure> or <error> child element.
        var testCases = doc.GetElementsByTagName("testcase");
        foreach (XmlNode testCase in testCases)
        {
            var failureNode = testCase.ChildNodes.OfType<XmlNode>()
                .FirstOrDefault(n => n.Name == "failure" || n.Name == "error");

            if (failureNode == null)
            {
                continue;
            }

            var name = testCase.Attributes?["name"]?.Value ?? "";
            var classname = testCase.Attributes?["classname"]?.Value ?? "";
            var testCaseTitle = string.IsNullOrEmpty(classname) ? name : $"{classname}.{name}";

            var errorMessage = failureNode.Attributes?["message"]?.Value ?? "";
            var stackTrace = failureNode.InnerText ?? "";
            var failureType = failureNode.Attributes?["type"]?.Value ?? failureNode.Name;

            failedTestRuns.Items.Add(new FailedTestRunResponse
            {
                TestCaseTitle = testCaseTitle,
                ErrorMessage = errorMessage,
                StackTrace = stackTrace,
                Outcome = failureType,
                Uri = filePath
            });
        }

        return failedTestRuns;
    }
}
