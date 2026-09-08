// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Mock.Handlers.EngSys;

/// <summary>Mock handler for azsdk_analyze_log_file. Returns a single fake build error.</summary>
public class AnalyzeLogFileHandler : IMockToolHandler
{
    public string ToolName => "azsdk_analyze_log_file";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new LogAnalysisResponse
    {
        Errors =
        [
            new LogEntry
            {
                File = "src/Contoso.Widgets/Generated/WidgetsClient.cs",
                Line = 42,
                Message = "error CS0246: The type or namespace name 'WidgetOptions' could not be found"
            }
        ]
    };
}

/// <summary>Mock handler for azsdk_get_failed_test_cases.</summary>
public class GetFailedTestCasesHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_failed_test_cases";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new FailedTestRunListResponse
    {
        Items =
        [
            new FailedTestRunResponse
            {
                RunId = 100001,
                TestCaseTitle = "Contoso.Widgets.Tests.WidgetClientLiveTests.GetWidget",
                Outcome = "Failed",
                ErrorMessage = "Expected status 200, got 404.",
                StackTrace = "at Contoso.Widgets.Tests.WidgetClientLiveTests.GetWidget()"
            }
        ]
    };
}

/// <summary>Mock handler for azsdk_get_failed_test_case_data.</summary>
public class GetFailedTestCaseDataHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_failed_test_case_data";
    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var testCaseTitle = arguments?.GetValueOrDefault("testCaseTitle")?.ToString()
            ?? "Contoso.Widgets.Tests.WidgetClientLiveTests.GetWidget";
        var isVersionParserFixture = testCaseTitle.Contains(
            "TryGetServiceVersion_ParsesAllServiceVersions",
            StringComparison.OrdinalIgnoreCase);

        return new FailedTestRunResponse
        {
            RunId = 100001,
            TestCaseTitle = testCaseTitle,
            Outcome = "Failed",
            ErrorMessage = isVersionParserFixture
                ? "TryGetServiceVersion returned false for 2026-10-06 (V2026_10_06) and 2026-12-06 (V2026_12_06)."
                : "Expected status 200, got 404.",
            StackTrace = isVersionParserFixture
                ? "at Azure.Storage.Queues.Tests.QueueClientOptionsTests.TryGetServiceVersion_ParsesAllServiceVersions()"
                : "at Contoso.Widgets.Tests.WidgetClientLiveTests.GetWidget()",
            Uri = "https://dev.azure.com/azure-sdk/internal/_test/cases?id=100001"
        };
    }
}

/// <summary>Mock handler for azsdk_get_failed_test_run_data.</summary>
public class GetFailedTestRunDataHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_failed_test_run_data";
    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var failedTestRunsPath = arguments?.GetValueOrDefault("failedTestRunsPath")?.ToString();
        var isVersionParserFixture = failedTestRunsPath?.Contains("6455504", StringComparison.OrdinalIgnoreCase) == true;

        return new FailedTestRunListResponse
        {
            Items =
            [
                new FailedTestRunResponse
                {
                    RunId = 100001,
                    TestCaseTitle = isVersionParserFixture
                        ? "Azure.Storage.Queues.Tests.QueueClientOptionsTests.TryGetServiceVersion_ParsesAllServiceVersions"
                        : "Contoso.Widgets.Tests.WidgetClientLiveTests.GetWidget",
                    Outcome = "Failed",
                    ErrorMessage = isVersionParserFixture
                        ? "TryGetServiceVersion returned false for 2026-10-06 (V2026_10_06) and 2026-12-06 (V2026_12_06)."
                        : "Expected status 200, got 404.",
                    StackTrace = isVersionParserFixture
                        ? "at Azure.Storage.Queues.Tests.QueueClientOptionsTests.TryGetServiceVersion_ParsesAllServiceVersions()"
                        : "at Contoso.Widgets.Tests.WidgetClientLiveTests.GetWidget()"
                }
            ]
        };
    }
}

/// <summary>Mock handler for azsdk_cleanup_ai_agents.</summary>
public class CleanupAiAgentsHandler : IMockToolHandler
{
    public string ToolName => "azsdk_cleanup_ai_agents";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new DefaultCommandResponse
    {
        Message = "AI agents cleaned up (mock)",
        Result = new { agentsDeleted = 3, threadsDeleted = 5 }
    };
}
