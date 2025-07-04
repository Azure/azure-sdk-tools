// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Xml;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Processes and analyzes test results from TRX files")]
public class TestAnalysisTool(ITestHelper testHelper, IOutputService output, ILogger<PipelineAnalysisTool> logger) : MCPTool()
{
    // Options
    private readonly Option<string> trxPathOpt = new(["--trx-file"], "Path to the TRX file for failed test runs") { IsRequired = true };
    private readonly Option<string> filterOpt = new(["--filter-title"], "Test case title to filter results");
    private readonly Option<bool> titlesOpt = new(["--titles"], "Only return test case titles, not full details");

    public override Command GetCommand()
    {
        var analyzeTestCommand = new Command("test-results", "Analyze test results") {
            trxPathOpt, filterOpt, titlesOpt
        };
        analyzeTestCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        return analyzeTestCommand;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        var cmd = ctx.ParseResult.CommandResult.Command.Name;
        var trxPath = ctx.ParseResult.GetValueForOption(trxPathOpt);
        var filterTitle = ctx.ParseResult.GetValueForOption(filterOpt);
        var titlesOnly = ctx.ParseResult.GetValueForOption(titlesOpt);

        if (titlesOnly)
        {
            var testTitles = await GetFailedTestCases(trxPath);
            ctx.ExitCode = ExitCode;
            output.Output(testTitles);
            return;
        }

        if (!string.IsNullOrEmpty(filterTitle))
        {
            var testCase = await GetFailedTestCaseData(trxPath, filterTitle);
            ctx.ExitCode = ExitCode;
            output.Output(testCase);
            return;
        }

        var testResult = await GetFailedTestRunDataFromTrx(trxPath);
        ctx.ExitCode = ExitCode;
        output.Output(testResult);
        return;
    }

    [McpServerTool, Description("Get titles of failed test cases from a TRX file")]
    public async Task<List<string>> GetFailedTestCases(string trxFilePath)
    {
        try
        {
            return await testHelper.GetFailedTestCases(trxFilePath);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to process TRX file {trxFilePath}: {exception}", trxFilePath, ex.Message);
            logger.LogError("Stack Trace: {stackTrace}", ex.StackTrace);
            SetFailure();
            return [];
        }
    }

    [McpServerTool, Description("Get details for a failed test from a TRX file")]
    public async Task<FailedTestRunResponse> GetFailedTestCaseData(string trxFilePath, string testCaseTitle)
    {
        try
        {
            var failedTestRuns = await testHelper.GetFailedTestRunDataFromTrx(trxFilePath);
            var testRun = failedTestRuns.FirstOrDefault(run => run.TestCaseTitle.Equals(testCaseTitle, StringComparison.OrdinalIgnoreCase));
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
            SetFailure();
            return new FailedTestRunResponse
            {
                ResponseError = $"Failed to process TRX file {trxFilePath}: {ex.Message}"
            };
        }
    }

    [McpServerTool, Description("Get failed test run data from a TRX file")]
    public async Task<List<FailedTestRunResponse>> GetFailedTestRunDataFromTrx(string trxFilePath)
    {
        try
        {
            return await testHelper.GetFailedTestRunDataFromTrx(trxFilePath);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to process TRX file {trxFilePath}: {exception}", trxFilePath, ex.Message);
            logger.LogError("Stack Trace: {stackTrace}", ex.StackTrace);
            SetFailure();
            return
            [
                new FailedTestRunResponse
                {
                    ResponseError = $"Failed to process TRX file {trxFilePath}: {ex.Message}"
                }
            ];
        }
    }
}
