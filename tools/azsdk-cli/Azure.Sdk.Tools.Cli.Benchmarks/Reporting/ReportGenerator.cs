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
    public async Task<string> GenerateAsync(
        IReadOnlyList<(BenchmarkScenario Scenario, BenchmarkResult Result)> results,
        string runName,
        string model,
        CancellationToken cancellationToken = default)
    {
        var reportDataJson = JsonSerializer.Serialize(BuildReportData(results, runName, model), JsonOptions);
        var prompt = BuildPrompt(reportDataJson, "Benchmark Data (JSON)");
        return await CallLlmAsync(ReportTemplate.SystemPrompt, prompt);
    }

    /// <summary>
    /// Generates a markdown report from existing log files.
    /// </summary>
    public async Task<string> GenerateFromLogsAsync(
        string logDirectory,
        CancellationToken cancellationToken = default)
    {
        var logFiles = Directory.GetFiles(logDirectory, "benchmark-log.json", SearchOption.AllDirectories);

        if (logFiles.Length == 0)
        {
            throw new InvalidOperationException($"No benchmark-log.json files found in {logDirectory}");
        }

        var logsJson = await Task.WhenAll(
            logFiles.Select(f => File.ReadAllTextAsync(f, cancellationToken)));

        var combinedData = $"[{string.Join(",\n", logsJson)}]";
        var prompt = BuildPrompt(combinedData, "Benchmark Log Data (JSON Array)");
        return await CallLlmAsync(ReportTemplate.SystemPrompt, prompt);
    }

    /// <summary>
    /// Builds the user prompt from data and the template file.
    /// </summary>
    private static string BuildPrompt(string dataJson, string dataLabel)
    {
        var template = ReportTemplate.LoadTemplate();
        return $"""
            Generate a benchmark report using the template and data below.
            Fill in every section of the template based on the provided data.
            
            ## Report Template
            
            {template}
            
            ## {dataLabel}
            
            ```json
            {dataJson}
            ```
            
            Generate the complete report now. Output only the filled-in markdown, no other text.
            """;
    }

    /// <summary>
    /// Calls the LLM using the Copilot SDK to generate report content.
    /// </summary>
    private static async Task<string> CallLlmAsync(string systemPrompt, string userPrompt)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"report-gen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            using var client = new CopilotClient(new CopilotClientOptions { Cwd = tempDir });

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
            await session.SendAndWaitAsync(new MessageOptions { Prompt = userPrompt }, TimeSpan.FromMinutes(5));

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
        return new
        {
            RunName = runName,
            Model = model,
            TestDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
            TotalScenarios = results.Count,
            TotalPassed = results.Count(r => r.Result.Passed),
            TotalFailed = results.Count(r => !r.Result.Passed),
            TotalDurationSeconds = results.Sum(r => r.Result.Duration.TotalSeconds),
            Scenarios = results.Select((r, i) => new
            {
                Index = i + 1,
                r.Scenario.Name,
                r.Scenario.Description,
                r.Scenario.Tags,
                r.Scenario.Prompt,
                Repo = r.Scenario.Repo.CloneUrl,
                r.Result
            }).ToList()
        };
    }
}
