// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools.Pipeline;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.EngSys;

[McpServerToolType, Description("Processes and analyzes test results from TRX files")]
public class TestAnalysisTool(ITestHelper testHelper, ILogger<PipelineAnalysisTool> logger) : MCPTool()
{
    // Options
    private readonly Option<string> trxPathOpt = new("--trx-file")
    {
        Description = "Path to the TRX file for failed test runs",
        Required = true,
    };

    private readonly Option<string> filterOpt = new("--filter-title")
    {
        Description = "Test case title to filter results",
        Required = false,
    };

    private readonly Option<bool> titlesOpt = new("--titles")
    {
        Description = "Only return test case titles, not full details",
        Required = false,
    };

    protected override Command GetCommand()
    {
        var analyzeTestCommand = new Command("test-results", "Analyze test results");
        analyzeTestCommand.Options.Add(trxPathOpt);
        analyzeTestCommand.Options.Add(filterOpt);
        analyzeTestCommand.Options.Add(titlesOpt);
        return analyzeTestCommand;
    }

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var cmd = parseResult.CommandResult.Command.Name;
        var trxPath = parseResult.GetValue(trxPathOpt);
        var filterTitle = parseResult.GetValue(filterOpt);
        var titlesOnly = parseResult.GetValue(titlesOpt);

        if (titlesOnly)
        {
            var failed = await GetFailedTestCases(trxPath);
            return new ObjectCommandResponse() { Result = failed };
        }

        if (!string.IsNullOrEmpty(filterTitle))
        {
            return await GetFailedTestCaseData(trxPath, filterTitle);
        }

        return await GetFailedTestRunDataFromTrx(trxPath);
    }

    [McpServerTool(Name = "azsdk_get_failed_test_cases"), Description("Get titles of failed test cases from a TRX file")]
    public async Task<FailedTestRunListResponse> GetFailedTestCases(string trxFilePath)
    {
        try
        {
            return await testHelper.GetFailedTestCases(trxFilePath);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to process TRX file {trxFilePath}: {exception}", trxFilePath, ex.Message);
            logger.LogError("Stack Trace: {stackTrace}", ex.StackTrace);
            return new();
        }
    }

    [McpServerTool(Name = "azsdk_get_failed_test_case_data"), Description("Get details for a failed test from a TRX file")]
    public async Task<FailedTestRunResponse> GetFailedTestCaseData(string trxFilePath, string testCaseTitle)
    {
        try
        {
            var failedTestRuns = await testHelper.GetFailedTestRunDataFromTrx(trxFilePath);
            var testRun = failedTestRuns.Items.FirstOrDefault(run => run.TestCaseTitle.Equals(testCaseTitle, StringComparison.OrdinalIgnoreCase));
            if (testRun == null)
            {
                return new FailedTestRunResponse
                {
                    ResponseError = $"No failed test run found for test case title: {testCaseTitle}"
                };
            }
            return testRun;
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to process TRX file {trxFilePath}: {exception}", trxFilePath, ex.Message);
            logger.LogError("Stack Trace: {stackTrace}", ex.StackTrace);
            return new FailedTestRunResponse
            {
                ResponseError = $"Failed to process TRX file {trxFilePath}: {ex.Message}"
            };
        }
    }

    [McpServerTool(Name = "azsdk_get_failed_test_run_data"), Description("Get failed test run data from a TRX file")]
    public async Task<FailedTestRunListResponse> GetFailedTestRunDataFromTrx(string trxFilePath)
    {
        try
        {
            return await testHelper.GetFailedTestRunDataFromTrx(trxFilePath);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to process TRX file {trxFilePath}: {exception}", trxFilePath, ex.Message);
            logger.LogError("Stack Trace: {stackTrace}", ex.StackTrace);
            return new() { ResponseError = $"Failed to process TRX file {trxFilePath}: {ex.Message}" };
        }
    }
}
