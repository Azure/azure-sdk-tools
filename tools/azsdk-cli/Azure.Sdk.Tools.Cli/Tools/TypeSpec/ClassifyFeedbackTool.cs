// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// TODO: temporary tool for local testing — remove before merging

using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec;

[McpServerToolType]
[Description("Classifies API review feedback as TSP_APPLICABLE, SUCCESS, or REQUIRES_MANUAL_INTERVENTION (temporary dev tool).")]
public class ClassifyFeedbackTool : MCPTool
{
    private const string ToolName = "azsdk_classify_feedback";

    private readonly IAPIViewFeedbackService _feedbackService;
    private readonly IFeedbackClassifierService _classifierService;
    private readonly ILogger<ClassifyFeedbackTool> _logger;

    private readonly Argument<string> _tspProjectPathArg = new("tsp-project-path")
    {
        Description = "Absolute path to the TypeSpec project directory",
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Option<string?> _apiViewUrlOption = new("--api-view-url")
    {
        Description = "APIView URL to fetch feedback from",
        DefaultValueFactory = _ => null
    };

    private readonly Option<string?> _plainTextOption = new("--plain-text")
    {
        Description = "Plain text feedback to classify",
        DefaultValueFactory = _ => null
    };

    private readonly Option<string?> _plainTextFileOption = new("--plain-text-file")
    {
        Description = "Path to a file containing plain text feedback",
        DefaultValueFactory = _ => null
    };

    private readonly Option<string?> _languageOption = new("--language")
    {
        Description = "SDK language (e.g. python, dotnet, java, js, go)",
        DefaultValueFactory = _ => null
    };

    private readonly Option<string?> _serviceNameOption = new("--service-name")
    {
        Description = "Azure service name for context",
        DefaultValueFactory = _ => null
    };

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec, SharedCommandGroups.TypeSpecClient];

    public ClassifyFeedbackTool(
        IAPIViewFeedbackService feedbackService,
        IFeedbackClassifierService classifierService,
        ILogger<ClassifyFeedbackTool> logger)
    {
        _feedbackService = feedbackService;
        _classifierService = classifierService;
        _logger = logger;
    }

    protected override Command GetCommand() =>
        new McpCommand("classify", "Classify API review feedback items.", ToolName)
        {
            _tspProjectPathArg,
            _apiViewUrlOption,
            _plainTextOption,
            _plainTextFileOption,
            _languageOption,
            _serviceNameOption
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var tspProjectPath = parseResult.GetValue(_tspProjectPathArg)!;
        var apiViewUrl = parseResult.GetValue(_apiViewUrlOption);
        var plainText = parseResult.GetValue(_plainTextOption);
        var plainTextFile = parseResult.GetValue(_plainTextFileOption);
        var language = parseResult.GetValue(_languageOption);
        var serviceName = parseResult.GetValue(_serviceNameOption);

        return await ClassifyAsync(tspProjectPath, apiViewUrl, plainText, plainTextFile, language, serviceName, ct);
    }

    [McpServerTool(Name = ToolName)]
    [Description("Classifies API review feedback items as TSP_APPLICABLE, SUCCESS, or REQUIRES_MANUAL_INTERVENTION.")]
    public async Task<FeedbackClassificationResponse> ClassifyAsync(
        string tspProjectPath,
        string? apiViewUrl = null,
        string? plainTextFeedback = null,
        string? plainTextFeedbackFile = null,
        string? language = null,
        string? serviceName = null,
        CancellationToken ct = default)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(plainTextFeedbackFile))
            {
                if (!File.Exists(plainTextFeedbackFile))
                {
                    throw new FileNotFoundException($"Feedback file not found: {plainTextFeedbackFile}");
                }
                plainTextFeedback = await File.ReadAllTextAsync(plainTextFeedbackFile, ct);
                _logger.LogInformation("Read {Len} chars from {File}", plainTextFeedback.Length, plainTextFeedbackFile);
            }

            if (string.IsNullOrEmpty(tspProjectPath) || !Directory.Exists(tspProjectPath))
            {
                throw new DirectoryNotFoundException($"TypeSpec project path does not exist: {tspProjectPath}");
            }

            List<FeedbackItem> feedbackItems = [];
            if (!string.IsNullOrWhiteSpace(apiViewUrl))
            {
                feedbackItems = await _feedbackService.GetFeedbackItemsAsync(apiViewUrl, ct);
                language ??= await _feedbackService.GetLanguageAsync(apiViewUrl, ct);
            }
            else if (!string.IsNullOrWhiteSpace(plainTextFeedback))
            {
                feedbackItems = [new FeedbackItem { Text = plainTextFeedback, Context = string.Empty }];
            }

            return await _classifierService.ClassifyItemsAsync(feedbackItems, globalContext: "", tspProjectPath, language, serviceName, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Classification failed");
            throw;
        }
    }
}
