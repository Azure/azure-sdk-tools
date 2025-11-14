// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools.Pipeline;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;

namespace Azure.Sdk.Tools.Cli.Tools.EngSys;

[McpServerToolType, Description("Processes and analyzes test results from TRX files")]
public class TestAnalysisTool(ITestHelper testHelper, ILogger<PipelineAnalysisTool> logger) : MCPTool()
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package, SharedCommandGroups.PackageTest];

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

    protected override Command GetCommand() =>
        new("results", "Analyze test results")
        {
            trxPathOpt,
            filterOpt,
            titlesOpt,
        };

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
            logger.LogError(ex, "Failed to process TRX file {TrxFilePath}", trxFilePath);
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
            logger.LogError(ex, "Failed to process TRX file {TrxFilePath}", trxFilePath);
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
            logger.LogError(ex, "Failed to process TRX file {TrxFilePath}", trxFilePath);
            return new() { ResponseError = $"Failed to process TRX file {trxFilePath}: {ex.Message}" };
        }
    }
}
