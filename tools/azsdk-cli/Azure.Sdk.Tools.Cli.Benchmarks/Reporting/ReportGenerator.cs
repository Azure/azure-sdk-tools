// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;
using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Reporting;

/// <summary>
/// Generates benchmark reports by sending log data and a report template to an LLM.
/// Reuses the Copilot SDK pattern from LlmJudge.
/// </summary>
public class ReportGenerator
{
    private const string DefaultModel = "claude-sonnet-4.5";
    private const string TemplateFileName = "report-template.md";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string SystemPrompt = """
        You are a benchmark report generator. You will receive JSON data from benchmark test runs
        and a report template. Your job is to analyze the data and fill in the template accurately.

        Rules:
        - Be precise with numbers and statistics. Do not hallucinate data.
        - If data is missing or incomplete, clearly indicate "N/A" or "Data not available".
        - For narrative sections, base your analysis strictly on the data provided.
        - Use the exact template structure provided. Do not add or remove sections.
        - For tool call analysis, group by tool name and MCP server when applicable.
        - For areas of improvement, only cite issues that are directly evidenced in the data.
        """;

    private readonly string _template;

    public ReportGenerator()
    {
        _template = LoadTemplate();
    }

    /// <summary>
    /// Generates a markdown report from benchmark results.
    /// </summary>
    /// <param name="results">The benchmark results to report on.</param>
    /// <param name="runName">Name for this benchmark run.</param>
    /// <param name="agentModel">The model used by the agent during the benchmark run (for metadata only).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string> GenerateAsync(
        IReadOnlyList<(BenchmarkScenario Scenario, BenchmarkResult Result)> results,
        string runName,
        string agentModel,
        CancellationToken cancellationToken = default)
    {
        var dataJson = JsonSerializer.Serialize(BuildReportData(results, runName, agentModel), JsonOptions);
        return await CallLlmAsync(BuildPrompt(dataJson, "Benchmark Data (JSON)"), cancellationToken);
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
        return await CallLlmAsync(BuildPrompt(combinedData, "Benchmark Log Data (JSON Array)"), cancellationToken);
    }

    private string BuildPrompt(string dataJson, string dataLabel)
    {
        return $"""
            Generate a benchmark report using the template and data below.
            Fill in every section of the template based on the provided data.
            
            ## Report Template
            
            {_template}
            
            ## {dataLabel}
            
            ```json
            {dataJson}
            ```
            
            Generate the complete report now. Output only the filled-in markdown, no other text.
            """;
    }

    private static async Task<string> CallLlmAsync(string userPrompt, CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"report-gen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            using var client = new CopilotClient(new CopilotClientOptions { Cwd = tempDir });

            var sessionConfig = new SessionConfig
            {
                Model = DefaultModel,
                Streaming = false,
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Replace,
                    Content = SystemPrompt
                },
                AvailableTools = [],
                McpServers = null,
                InfiniteSessions = new InfiniteSessionConfig { Enabled = false }
            };

            await using var session = await client.CreateSessionAsync(sessionConfig, cancellationToken);
            await session.SendAndWaitAsync(new MessageOptions { Prompt = userPrompt }, TimeSpan.FromMinutes(5), cancellationToken);

            var messages = await session.GetMessagesAsync(cancellationToken);
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

    private static object BuildReportData(
        IReadOnlyList<(BenchmarkScenario Scenario, BenchmarkResult Result)> results,
        string runName,
        string agentModel)
    {
        return new
        {
            RunName = runName,
            Model = agentModel,
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

    private static string LoadTemplate()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var templatePath = Path.Combine(assemblyDir, "Reporting", TemplateFileName);

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException(
                $"Report template not found at '{templatePath}'. Ensure '{TemplateFileName}' is set as Content/CopyToOutputDirectory in the .csproj.");
        }

        return File.ReadAllText(templatePath);
    }
}
