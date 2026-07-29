// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.EngSys;

[McpServerToolType, Description("Processes and analyzes test results from test result files (TRX, JUnit XML, etc.)")]
public class TestAnalysisTool(ITestResultParserResolver parserResolver, ILogger<TestAnalysisTool> logger) : MCPTool()
{
    // MCP Tool Names
    private const string GetFailedTestCasesToolName = "azsdk_get_failed_test_cases";
    private const string GetFailedTestCaseDataToolName = "azsdk_get_failed_test_case_data";
    private const string GetFailedTestRunDataToolName = "azsdk_get_failed_test_run_data";

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package, SharedCommandGroups.PackageTest];

    // Options
    private readonly Option<string> testResultsPathOpt = new("--test-results-file")
    {
        Description = "Path to the test results file (TRX, JUnit XML, etc.)",
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
        new McpCommand("results", "Analyze test results", GetFailedTestCasesToolName)
        {
            testResultsPathOpt,
            filterOpt,
            titlesOpt,
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var cmd = parseResult.CommandResult.Command.Name;
        var testResultsPath = parseResult.GetValue(testResultsPathOpt);
        var filterTitle = parseResult.GetValue(filterOpt);
        var titlesOnly = parseResult.GetValue(titlesOpt);

        if (titlesOnly)
        {
            var failed = await GetFailedTestCases(testResultsPath, ct);
            return new ObjectCommandResponse() { Result = failed };
        }

        if (!string.IsNullOrEmpty(filterTitle))
        {
            return await GetFailedTestCaseData(testResultsPath, filterTitle, ct);
        }

        return await GetFailedTestResults(testResultsPath, ct);
    }

    [McpServerTool(Name = GetFailedTestCasesToolName), Description("Get list of all failed test case titles (names only) from a test result file. Use this to quickly see which tests failed without details.")]
    public async Task<FailedTestRunListResponse> GetFailedTestCases(string failedTestRunsPath, CancellationToken ct = default)
    {
        try
        {
            var parser = await parserResolver.ResolveAsync(failedTestRunsPath, ct);
            return await parser.GetFailedTestCases(failedTestRunsPath, ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process test result file {FilePath}", failedTestRunsPath);
            return new() { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = GetFailedTestCaseDataToolName), Description("Get detailed information (error messages, stack traces, output) for a specific failed test case by title from a test result file. Use this to debug a particular test failure.")]
    public async Task<FailedTestRunResponse> GetFailedTestCaseData(string failedTestRunsPath, string testCaseTitle, CancellationToken ct = default)
    {
        try
        {
            var parser = await parserResolver.ResolveAsync(failedTestRunsPath, ct);
            var failedTestRuns = await parser.GetFailedTestResults(failedTestRunsPath, ct);
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
            logger.LogError(ex, "Failed to process test result file {FilePath}", failedTestRunsPath);
            return new FailedTestRunResponse { ResponseError = ex.Message };
        }
    }

    [McpServerTool(Name = GetFailedTestRunDataToolName), Description("Get complete details for all failed test cases from a test result file. Returns full data including error messages, stack traces, and output for every failed test. Use this for comprehensive analysis.")]
    public async Task<FailedTestRunListResponse> GetFailedTestResults(string failedTestRunsPath, CancellationToken ct = default)
    {
        try
        {
            var parser = await parserResolver.ResolveAsync(failedTestRunsPath, ct);
            return await parser.GetFailedTestResults(failedTestRunsPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process test result file {FilePath}", failedTestRunsPath);
            return new() { ResponseError = ex.Message };
        }
    }
}
