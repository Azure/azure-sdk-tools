// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Text;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec;

/// <summary>
/// CLI tool for applying SDK customizations from various feedback sources (APIView comments, build errors, etc.).
/// Preprocesses input, classifies feedback, and generates a GitHub issue for TypeSpec customizations.
/// </summary>
[McpServerToolType]
[Description("Apply SDK customizations from various feedback sources")]
public class CustomizationFromFeedbackTool : MCPTool
{
    private readonly IAPIViewFeedbackCustomizationsHelpers _feedbackHelper;
    private readonly IGitHubService _gitHubService;
    private readonly IMicroagentHostService _microagentHost;
    private readonly ILogger<CustomizationFromFeedbackTool> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _model;

    private const string ToolName = "azsdk_from_feedback";
    private const string DefaultRepoOwner = "Azure";
    private const string DefaultRepoName = "azure-rest-api-specs";
    private const int MaxOrchestrationIterations = 2;

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec, SharedCommandGroups.TypeSpecClient];

    private readonly Option<string> apiViewUrlOption = new("--apiview-url")
    {
        Description = "APIView URL to fetch comments from",
        Required = false
    };

    private readonly Option<string> buildLogOption = new("--build-log")
    {
        Description = "Path to build log file",
        Required = false
    };

    private readonly Option<string> languageOption = new("--language")
    {
        Description = "Target SDK language (e.g., python, csharp, java)",
        Required = false
    };

    private readonly Option<string> serviceNameOption = new("--service-name")
    {
        Description = "Service name for the SDK (e.g., Azure.AI.Projects)",
        Required = false
    };

    private readonly Option<bool> createIssueOption = new("--create-issue")
    {
        Description = "Prompt to create a GitHub issue (by default, skips prompt for issue creation)",
        Required = false
    };

    public CustomizationFromFeedbackTool(
        ILogger<CustomizationFromFeedbackTool> logger,
        IAPIViewFeedbackCustomizationsHelpers feedbackHelper,
        IGitHubService gitHubService,
        IMicroagentHostService microagentHost,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _feedbackHelper = feedbackHelper;
        _gitHubService = gitHubService;
        _microagentHost = microagentHost;
        _model = configuration["AZURE_OPENAI_MODEL"] ?? "gpt-4o";
        _loggerFactory = loggerFactory;
    }

    protected override Command GetCommand() =>
        new McpCommand("from-feedback", "Apply SDK customizations from various feedback sources", ToolName)
        {
            apiViewUrlOption,
            buildLogOption,
            languageOption,
            serviceNameOption,
            createIssueOption
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var apiViewUrl = parseResult.GetValue(apiViewUrlOption);
        var buildLog = parseResult.GetValue(buildLogOption);
        var language = parseResult.GetValue(languageOption);
        var serviceName = parseResult.GetValue(serviceNameOption);
        var createIssue = parseResult.GetValue(createIssueOption);
        return await FromFeedbackAsync(apiViewUrl, buildLog, language, serviceName, createIssue, ct);
    }

    [McpServerTool(Name = ToolName), Description("Classify feedback from APIView or build errors and generate a GitHub issue for TypeSpec customizations. Provide either apiViewUrl or buildLog. Set createIssue=true to prompt for issue creation.")]
    public async Task<FromFeedbackResponse> FromFeedbackAsync(string? apiViewUrl = null, string? buildLog = null, string? language = null, string? serviceName = null, bool createIssue = false, CancellationToken ct = default)
    {
        try
        {
            // Create appropriate input strategy
            var feedbackInput = CreateFeedbackInput(apiViewUrl, buildLog, language);
            
            // Preprocess input to common format
            _logger.LogInformation("Preprocessing feedback input...");
            var feedbackContext = await feedbackInput.PreprocessAsync(ct);
            
            // Use language/service from context if not provided
            language ??= feedbackContext.Language;
            serviceName ??= feedbackContext.ServiceName;
            
            _logger.LogInformation("Feedback type: {Type}, Language: {Language}", feedbackContext.InputType, language);

            // Classify each feedback item individually
            var actionableFeedbackItems = new List<FeedbackItem>();
            
            foreach (var item in feedbackContext.FeedbackItems)
            {
                _logger.LogInformation("Classifying feedback item: {Id}", item.Id);
                
                var context = new OrchestrationContext(item.FormattedForPrompt, language);
                var classification = await ClassifyAsync(context, ct);
                
                _logger.LogInformation("Item {Id} classified as: {Classification}", item.Id, classification);
                
                if (classification == "PHASE_A")
                {
                    actionableFeedbackItems.Add(item);
                }
                else if (classification == "SUCCESS")
                {
                    _logger.LogInformation("Item {Id} is non-actionable (SUCCESS), skipping", item.Id);
                }
                else if (classification == "FAILURE")
                {
                    _logger.LogWarning("Item {Id} classified as FAILURE", item.Id);
                }
            }

            // Check if any items are actionable
            if (actionableFeedbackItems.Count == 0)
            {
                return new FromFeedbackResponse
                {
                    Classification = "SUCCESS",
                    Message = "No actionable customization feedback found. All feedback appears to be already addressed or informational."
                };
            }

            _logger.LogInformation("Found {Count} actionable feedback items out of {Total}", 
                actionableFeedbackItems.Count, feedbackContext.FeedbackItems.Count);

            // Generate the issue content with only actionable items
            var issueTitle = $"[SDK Customization] Apply TypeSpec client customizations - {feedbackContext.InputType}";
            var issueBody = GenerateIssueBody(feedbackContext, actionableFeedbackItems, language, serviceName);

            var response = new FromFeedbackResponse
            {
                Classification = "PHASE_A",
                IssueTitle = issueTitle,
                IssueBody = issueBody,
                RepoOwner = DefaultRepoOwner,
                RepoName = DefaultRepoName,
                Metadata = feedbackContext.Metadata
            };

            // For MCP usage: provide next action instructions for the agent
            // For CLI usage: prompt interactively
            if (createIssue)
            {
                response.NextAction = $"Use the mcp_github_create_issue tool to create a draft issue with owner='{DefaultRepoOwner}', repo='{DefaultRepoName}', title='{issueTitle}', and the issue body provided above.";
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process feedback");
            return new FromFeedbackResponse
            {
                ResponseError = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Classifies the feedback using the classification template.
    /// </summary>
    private async Task<string> ClassifyAsync(OrchestrationContext context, CancellationToken ct)
    {
        var prompt = new CommentClassificationTemplate(
            null, // serviceName - not available from APIView
            context.Language,
            context.ToClassifierInput(),
            context.Iteration,
            context.IsStalled()
        ).BuildPrompt();

        var result = await _microagentHost.RunAgentToCompletion(new Microagent<ClassificationResult>
        {
            Instructions = prompt,
            Model = _model,
            MaxToolCalls = 1,
            Tools = []
        }, ct);

        return result.Classification;
    }

    /// <summary>
    /// Creates the GitHub issue after user confirmation.
    /// </summary>
    [McpServerTool(Name = "azsdk_create_customization_issue"), Description("Create a GitHub issue for TypeSpec customizations after user confirmation")]
    public async Task<FromFeedbackResponse> CreateIssueAsync(string issueTitle, string issueBody, string repoOwner = DefaultRepoOwner, string repoName = DefaultRepoName)
    {
        try
        {
            _logger.LogInformation("Creating issue in {RepoOwner}/{RepoName}", repoOwner, repoName);

            var issue = await _gitHubService.CreateIssueAsync(repoOwner, repoName, issueTitle, issueBody);

            return new FromFeedbackResponse
            {
                Message = $"Issue created successfully: {issue.HtmlUrl}",
                IssueUrl = issue.HtmlUrl,
                IssueNumber = issue.Number
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create issue");
            return new FromFeedbackResponse
            {
                ResponseError = $"Failed to create issue: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Factory method to create appropriate feedback input strategy
    /// </summary>
    private IFeedbackInput CreateFeedbackInput(string? apiViewUrl, string? buildLog, string? language)
    {
        if (!string.IsNullOrEmpty(apiViewUrl))
        {
            return new APIViewFeedbackInput(
                apiViewUrl,
                _feedbackHelper,
                _loggerFactory.CreateLogger<APIViewFeedbackInput>());
        }

        if (!string.IsNullOrEmpty(buildLog))
        {
            return new BuildErrorFeedbackInput(
                buildLog,
                _loggerFactory.CreateLogger<BuildErrorFeedbackInput>(),
                language);
        }

        throw new ArgumentException("At least one input source must be provided: --apiview-url or --build-log");
    }

    private static string GenerateIssueBody(FeedbackContext feedbackContext, List<FeedbackItem> actionableItems, string? language, string? serviceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine($"Apply TypeSpec client customizations to address {feedbackContext.InputType} feedback.");
        sb.AppendLine();
        sb.AppendLine("## Context");
        sb.AppendLine();

        // Add source-specific context
        if (feedbackContext.Metadata.TryGetValue("APIViewUrl", out var apiViewUrl))
        {
            sb.AppendLine($"- **APIView**: {apiViewUrl}");
        }
        if (feedbackContext.Metadata.TryGetValue("BuildLogPath", out var buildLogPath))
        {
            sb.AppendLine($"- **Build Log**: {buildLogPath}");
        }
        if (!string.IsNullOrEmpty(feedbackContext.PackageName))
        {
            sb.AppendLine($"- **Package**: {feedbackContext.PackageName}");
        }
        if (!string.IsNullOrEmpty(serviceName))
        {
            sb.AppendLine($"- **Service**: {serviceName}");
        }
        if (!string.IsNullOrEmpty(language))
        {
            sb.AppendLine($"- **Language**: {language}");
        }
        sb.AppendLine("- **Reference**: [TypeSpec Client Customizations Guide](https://github.com/Azure/azure-rest-api-specs/blob/main/eng/common/knowledge/customizing-client-tsp.md)");
        sb.AppendLine();
        
        // Include only actionable feedback items
        sb.AppendLine("## Feedback to Address");
        sb.AppendLine();
        
        foreach (var item in actionableItems)
        {
            sb.AppendLine(item.FormattedForPrompt);
        }
        
        sb.AppendLine();
        sb.AppendLine("## Validation");
        sb.AppendLine();
        sb.AppendLine("After applying changes:");
        sb.AppendLine("1. Run `tsp compile .` - ensure no errors");
        sb.AppendLine("2. Regenerate SDK and verify changes");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("*Auto-generated by SDK customization tool*");

        return sb.ToString();
    }

    private class ClassificationResult
    {
        public string Classification { get; set; } = "FAILURE";
        public string Reason { get; set; } = "";
    }
}

public class FromFeedbackResponse : CommandResponse
{
    public string? Message { get; set; }
    public string? Classification { get; set; }
    public string? IssueTitle { get; set; }
    public string? IssueBody { get; set; }
    public string? RepoOwner { get; set; }
    public string? RepoName { get; set; }
    public string? NextAction { get; set; }
    public string? IssueUrl { get; set; }
    public int? IssueNumber { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }

    protected override string Format()
    {
        if (!string.IsNullOrEmpty(ResponseError))
        {
            return $"Error: {ResponseError}";
        }

        if (!string.IsNullOrEmpty(IssueUrl))
        {
            return $"Issue created: {IssueUrl}";
        }

        if (!string.IsNullOrEmpty(Message))
        {
            return Message;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# {IssueTitle}");
        sb.AppendLine();
        sb.AppendLine(IssueBody);

        if (!string.IsNullOrEmpty(NextAction))
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"**Next Action**: {NextAction}");
        }

        return sb.ToString();
    }
}
