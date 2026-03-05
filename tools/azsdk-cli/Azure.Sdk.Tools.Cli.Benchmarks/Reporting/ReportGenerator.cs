// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;
using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Reporting;

/// <summary>
/// Generates benchmark reports by sending log data and a template to an LLM.
/// Reuses the Copilot SDK pattern from LlmJudge.
/// </summary>
public class ReportGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Generates a markdown report from benchmark results.
    /// </summary>
    /// <param name="results">The benchmark results to include in the report.</param>
    /// <param name="runName">A name for this test run.</param>
    /// <param name="model">The model used to run the benchmarks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated markdown report.</returns>
    public async Task<string> GenerateAsync(
        IReadOnlyList<(BenchmarkScenario Scenario, BenchmarkResult Result)> results,
        string runName,
        string model,
        CancellationToken cancellationToken = default)
    {
        var reportData = BuildReportData(results, runName, model);
        var reportDataJson = JsonSerializer.Serialize(reportData, JsonOptions);

        var userPrompt = $"""
            Generate a benchmark report using the template and data below.
            Fill in every section of the template based on the provided data.
            
            ## Report Template
            
            {ReportTemplate.Template}
            
            ## Benchmark Data (JSON)
            
            ```json
            {reportDataJson}
            ```
            
            Generate the complete report now. Output only the filled-in markdown, no other text.
            """;

        return await CallLlmAsync(ReportTemplate.SystemPrompt, userPrompt, cancellationToken);
    }

    /// <summary>
    /// Generates a markdown report from existing log files.
    /// </summary>
    /// <param name="logDirectory">Directory containing benchmark-log.json files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated markdown report.</returns>
    public async Task<string> GenerateFromLogsAsync(
        string logDirectory,
        CancellationToken cancellationToken = default)
    {
        var logFiles = Directory.GetFiles(logDirectory, "benchmark-log.json", SearchOption.AllDirectories);

        if (logFiles.Length == 0)
        {
            throw new InvalidOperationException($"No benchmark-log.json files found in {logDirectory}");
        }

        var logsJson = new List<string>();
        foreach (var logFile in logFiles)
        {
            var content = await File.ReadAllTextAsync(logFile, cancellationToken);
            logsJson.Add(content);
        }

        var combinedData = $"[{string.Join(",\n", logsJson)}]";

        var userPrompt = $"""
            Generate a benchmark report using the template and data below.
            Fill in every section of the template based on the provided log data.
            The data comes from benchmark execution logs. Extract all relevant information.
            
            ## Report Template
            
            {ReportTemplate.Template}
            
            ## Benchmark Log Data (JSON Array)
            
            ```json
            {combinedData}
            ```
            
            Generate the complete report now. Output only the filled-in markdown, no other text.
            """;

        return await CallLlmAsync(ReportTemplate.SystemPrompt, userPrompt, cancellationToken);
    }

    /// <summary>
    /// Calls the LLM using the Copilot SDK to generate report content.
    /// Uses the same pattern as LlmJudge: temp directory, no tools, replace system message.
    /// </summary>
    private static async Task<string> CallLlmAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"report-gen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            using var client = new CopilotClient(new CopilotClientOptions
            {
                Cwd = tempDir
            });

            var sessionConfig = new SessionConfig
            {
                Model = ReportTemplate.DefaultReportModel,
                Streaming = false,
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Replace,
                    Content = systemPrompt
                },
                AvailableTools = [],
                McpServers = null,
                InfiniteSessions = new InfiniteSessionConfig { Enabled = false }
            };

            await using var session = await client.CreateSessionAsync(sessionConfig);

            var messageOptions = new MessageOptions { Prompt = userPrompt };
            await session.SendAndWaitAsync(messageOptions, TimeSpan.FromMinutes(5));

            var messages = await session.GetMessagesAsync();
            var lastAssistantMessage = messages
                .OfType<AssistantMessageEvent>()
                .LastOrDefault();

            return lastAssistantMessage?.Data?.Content ?? "Error: No response from LLM.";
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Builds a structured data object from benchmark results for the LLM.
    /// </summary>
    private static object BuildReportData(
        IReadOnlyList<(BenchmarkScenario Scenario, BenchmarkResult Result)> results,
        string runName,
        string model)
    {
        var scenarioData = results.Select((r, i) => new
        {
            Index = i + 1,
            r.Scenario.Name,
            r.Scenario.Description,
            r.Scenario.Tags,
            Prompt = r.Scenario.Prompt,
            Repo = r.Scenario.Repo.CloneUrl,
            r.Result.Passed,
            r.Result.Error,
            Duration = r.Result.Duration.ToString(),
            DurationSeconds = r.Result.Duration.TotalSeconds,
            ToolCalls = r.Result.ToolCalls.Select(tc => new
            {
                tc.ToolName,
                tc.Arguments,
                tc.DurationMs,
                tc.McpServerName,
                tc.Timestamp
            }),
            ToolCallCount = r.Result.ToolCalls.Count,
            Validation = r.Result.Validation != null ? new
            {
                r.Result.Validation.Passed,
                r.Result.Validation.PassedCount,
                r.Result.Validation.FailedCount,
                Results = r.Result.Validation.Results.Select(v => new
                {
                    v.ValidatorName,
                    v.Passed,
                    v.Message,
                    v.Details
                })
            } : null,
            r.Result.GitDiff
        }).ToList();

        return new
        {
            RunName = runName,
            Model = model,
            TestDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
            TotalScenarios = results.Count,
            TotalPassed = results.Count(r => r.Result.Passed),
            TotalFailed = results.Count(r => !r.Result.Passed),
            TotalDurationSeconds = results.Sum(r => r.Result.Duration.TotalSeconds),
            Scenarios = scenarioData
        };
    }
}
