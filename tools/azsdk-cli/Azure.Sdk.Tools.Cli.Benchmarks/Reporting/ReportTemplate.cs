// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Reporting;

/// <summary>
/// Contains the report template used by the LLM to generate benchmark reports.
/// </summary>
public static class ReportTemplate
{
    /// <summary>
    /// The default model used for report generation.
    /// </summary>
    public const string DefaultReportModel = "claude-sonnet-4.5";

    /// <summary>
    /// System prompt that instructs the LLM on how to generate the report.
    /// </summary>
    public const string SystemPrompt = """
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

    private const string TemplateFileName = "report-template.md";

    /// <summary>
    /// Loads the markdown report template from the file on disk.
    /// The template file lives alongside this class in the Reporting/ directory.
    /// </summary>
    public static string LoadTemplate()
    {
        // Resolve relative to the source file location at build time
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        // At runtime the template is copied to the output directory
        var templatePath = Path.Combine(assemblyDir, "Reporting", TemplateFileName);

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException(
                $"Report template not found at '{templatePath}'. Ensure '{TemplateFileName}' is set as Content/CopyToOutputDirectory in the .csproj.");
        }

        return File.ReadAllText(templatePath);
    }
}
