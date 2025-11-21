// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.EngSys;

[McpServerToolType, Description("Analyzes log files for errors and issues")]
public class LogAnalysisTool(
    ILogAnalysisHelper logHelper,
    ILogger<LogAnalysisTool> logger
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.AzurePipelines,
        SharedCommandGroups.Log
    ];

    // Command names
    private const string AnalyzeCommandName = "analyze";

    // Options
    private readonly Option<string> filePathOpt = new("--file", "-f")
    {
        Description = "Path to the file to analyze",
        Required = true,
    };

    private readonly Option<string> keywordsOpt = new("--keywords", "-k")
    {
        Description = "Custom keywords to search for (comma-separated)",
        Required = false,
    };

    private readonly Option<bool> fullSearchOpt = new("--full")
    {
        Description = "Enable full keyword search from a catalog of terms",
        Required = false,
    };

    private readonly Option<int> contextLinesOpt = new("--context", "-c")
    {
        Description = "Number of context lines to include around matches",
        Required = false,
        DefaultValueFactory = _ => -1,
    };

    private const int DEFAULT_CONTEXT_LINES = 20;

    protected override Command GetCommand() => new McpCommand(AnalyzeCommandName, "Analyze a log file for errors and issues", "azsdk_analyze_log_file")
    {
        filePathOpt, keywordsOpt, fullSearchOpt, contextLinesOpt,
    };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var command = parseResult.CommandResult.Command.Name;

        switch (command)
        {
            case AnalyzeCommandName:
                var filePath = parseResult.GetValue(filePathOpt);
                var customKeywords = parseResult.GetValue(keywordsOpt);
                var fullSearch = parseResult.GetValue(fullSearchOpt);
                var contextLines = parseResult.GetValue(contextLinesOpt);

                var keywords = ParseCustomKeywords(customKeywords);
                var result = await AnalyzeLogFile(filePath, fullSearch, keywords, contextLines);
                return result;

            default:
                return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
        }
    }

    [McpServerTool(Name = "azsdk_analyze_log_file"), Description("Analyzes a log file for errors and issues")]
    public async Task<LogAnalysisResponse> AnalyzeLogFile(string filePath, bool fullSearch = false, List<string> customKeywords = null, int contextLines = -1)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return new LogAnalysisResponse
                {
                    ResponseError = "File path is required"
                };
            }

            if (!File.Exists(filePath))
            {
                return new LogAnalysisResponse
                {
                    ResponseError = $"File not found: {filePath}"
                };
            }

            logger.LogInformation("Analyzing file: {filePath}", filePath);

            // Shortcut search for red colored text in the log by default
            List<LogEntry> errors;
            if (!fullSearch)
            {
                customKeywords ??= [];
                customKeywords.Add("[31m");
                var before = contextLines >= 0 ? contextLines : 20;
                var after = contextLines >= 0 ? contextLines : 5;
                errors = await logHelper.AnalyzeLogContent(filePath, customKeywords, before, after);
            }
            else
            {
                var before = contextLines >= 0 ? contextLines : DEFAULT_CONTEXT_LINES;
                var after = contextLines >= 0 ? contextLines : DEFAULT_CONTEXT_LINES;
                errors = await logHelper.AnalyzeLogContent(filePath, customKeywords ?? null, before, after);
            }

            return new LogAnalysisResponse
            {
                Summary = string.Empty, // As requested, leaving empty
                SuggestedFix = string.Empty, // As requested, leaving empty
                Errors = errors
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing file: {filePath}", filePath);
            return new LogAnalysisResponse
            {
                ResponseError = $"Error analyzing file: {ex.Message}"
            };
        }
    }

    private static List<string>? ParseCustomKeywords(string? customKeywords)
    {
        if (string.IsNullOrWhiteSpace(customKeywords))
        {
            return null;
        }

        return customKeywords
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
