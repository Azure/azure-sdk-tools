// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Xml;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

public interface ITestHelper
{
    Task<List<FailedTestRunResponse>> GetFailedTestCases(string trxFilePath, string filterTitle = "");
    Task<FailedTestRunResponse> GetFailedTestCaseData(string trxFilePath, string testCaseTitle);
    Task<List<FailedTestRunResponse>> GetFailedTestRunDataFromTrx(string trxFilePath);
}

public class TestHelper(IOutputService output, ILogger<TestHelper> logger) : ITestHelper
{
    private readonly IOutputService output = output;
    private readonly ILogger<TestHelper> logger = logger;

    public async Task<List<FailedTestRunResponse>> GetFailedTestCases(string trxFilePath, string filterTitle = "")
    {
        var failedTestRuns = await GetFailedTestRunDataFromTrx(trxFilePath);

        return failedTestRuns
                .Where(run => string.IsNullOrEmpty(filterTitle) || run.TestCaseTitle.Contains(filterTitle, StringComparison.OrdinalIgnoreCase))
                .Select(run => new FailedTestRunResponse
                {
                    TestCaseTitle = run.TestCaseTitle,
                    Uri = run.Uri
                })
                .ToList();
    }

    public async Task<FailedTestRunResponse> GetFailedTestCaseData(string trxFilePath, string testCaseTitle)
    {
        var failedTestRuns = await GetFailedTestRunDataFromTrx(trxFilePath);
        return failedTestRuns.FirstOrDefault(run => run.TestCaseTitle.Equals(testCaseTitle, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<FailedTestRunResponse>> GetFailedTestRunDataFromTrx(string trxFilePath)
    {
        var failedTestRuns = new List<FailedTestRunResponse>();
        if (!File.Exists(trxFilePath))
        {
            logger.LogError("TRX file not found: {trxFilePath}", trxFilePath);
            return failedTestRuns;
        }

        var xmlContent = await File.ReadAllTextAsync(trxFilePath);
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        var unitTestResults = doc.GetElementsByTagName("UnitTestResult");
        foreach (XmlNode resultNode in unitTestResults)
        {
            var outcome = resultNode.Attributes?["outcome"]?.Value ?? "";
            if (!string.Equals(outcome, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var testId = resultNode.Attributes?["testId"]?.Value ?? "";
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

            failedTestRuns.Add(new FailedTestRunResponse
            {
                TestCaseTitle = testName,
                ErrorMessage = errorMessage,
                StackTrace = stackTrace,
                Outcome = outcome,
                Uri = trxFilePath
            });
        }

        return failedTestRuns;
    }
}
