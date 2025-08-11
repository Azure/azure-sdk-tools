// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Analyzes log files for errors and issues")]
public class LogAnalysisTool : MCPTool
{
    private readonly ILogAnalysisHelper logHelper;
    private readonly IOutputService output;
    private readonly ILogger<LogAnalysisTool> logger;

    private const int DEFAULT_CONTEXT_LINES = 20;

    // Command names
    private const string AnalyzeCommandName = "analyze";

    // Options
    private readonly Option<string> filePathOpt = new(["--file", "-f"], "Path to the file to analyze") { IsRequired = true };
    private readonly Option<string> keywordsOpt = new(["--keywords", "-k"], "Custom keywords to search for (comma-separated)");
    private readonly Option<bool> fullSearchOpt = new(["--full"], "Enable full keyword search from a catalog of terms");
    private readonly Option<int> contextLinesOpt = new(["--context", "-c"], () => -1, "Number of context lines to include around matches");

    public LogAnalysisTool(
        ILogAnalysisHelper logHelper,
        IOutputService output,
        ILogger<LogAnalysisTool> logger
    ) : base()
    {
        this.logHelper = logHelper;
        this.output = output;
        this.logger = logger;

        CommandHierarchy =
        [
            SharedCommandGroups.Log
        ];
    }

    public override Command GetCommand()
    {
        var analyzeCommand = new Command(AnalyzeCommandName, "Analyze a log file for errors and issues")
        {
            filePathOpt, keywordsOpt, fullSearchOpt, contextLinesOpt
        };

        analyzeCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        return analyzeCommand;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        var command = ctx.ParseResult.CommandResult.Command.Name;

        switch (command)
        {
            case AnalyzeCommandName:
                var filePath = ctx.ParseResult.GetValueForOption(filePathOpt);
                var customKeywords = ctx.ParseResult.GetValueForOption(keywordsOpt);
                var fullSearch = ctx.ParseResult.GetValueForOption(fullSearchOpt);
                var contextLines = ctx.ParseResult.GetValueForOption(contextLinesOpt);

                var keywords = ParseCustomKeywords(customKeywords);
                var result = await AnalyzeLogFile(filePath, fullSearch, keywords, contextLines);

                ctx.ExitCode = ExitCode;
                output.Output(result);
                break;

            default:
                logger.LogError("Unknown command: {command}", command);
                SetFailure();
                break;
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
            SetFailure();
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
