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
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec;

/// <summary>
/// CLI tool for applying SDK customizations from API review feedback.
/// Fetches consolidated APIView comments, classifies them, and generates a GitHub issue for TypeSpec customizations.
/// </summary>
[McpServerToolType]
[Description("Apply SDK customizations from API review feedback")]
public class CustomizationFromFeedbackTool : MCPTool
{
    private readonly IAPIViewFeedbackCustomizationsHelpers _feedbackHelper;
    private readonly IGitHubService _gitHubService;
    private readonly IMicroagentHostService _microagentHost;
    private readonly ILogger<CustomizationFromFeedbackTool> _logger;
    private readonly string _model;

    private const string ToolName = "azsdk_from_feedback";
    private const string DefaultRepoOwner = "Azure";
    private const string DefaultRepoName = "azure-rest-api-specs";
    private const int MaxOrchestrationIterations = 2;

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec, SharedCommandGroups.TypeSpecClient];

    private readonly Option<string> apiViewUrlOption = new("--apiview-url")
    {
        Description = "APIView URL to fetch enriched comments from",
        Required = true
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
        IConfiguration configuration)
    {
        _logger = logger;
        _feedbackHelper = feedbackHelper;
        _gitHubService = gitHubService;
        _microagentHost = microagentHost;
        _model = configuration["AZURE_OPENAI_MODEL"] ?? "gpt-4o";
    }

    protected override Command GetCommand() =>
        new McpCommand("from-feedback", "Apply SDK customizations from API review feedback", ToolName)
        {
            apiViewUrlOption,
            languageOption,
            serviceNameOption,
            createIssueOption
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var apiViewUrl = parseResult.GetValue(apiViewUrlOption);
        var language = parseResult.GetValue(languageOption);
        var serviceName = parseResult.GetValue(serviceNameOption);
        var createIssue = parseResult.GetValue(createIssueOption);
        return await FromFeedbackAsync(apiViewUrl!, language, serviceName, createIssue, ct);
    }

    [McpServerTool(Name = ToolName), Description("Classify APIView feedback and generate a GitHub issue for TypeSpec customizations. Set createIssue=true to prompt for issue creation.")]
    public async Task<FromFeedbackResponse> FromFeedbackAsync(string apiViewUrl, string? language = null, string? serviceName = null, bool createIssue = false, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Fetching consolidated comments from: {Url}", apiViewUrl);

            var comments = await _feedbackHelper.GetConsolidatedComments(apiViewUrl);

            if (comments.Count == 0)
            {
                return new FromFeedbackResponse
                {
                    CommentCount = 0,
                    Message = "No actionable comments found (all resolved or questions/discussion)"
                };
            }

            _logger.LogInformation("Retrieved {Count} consolidated comment(s)", comments.Count);

            // Format feedback for classifier - let the prompt decide what's actionable
            var feedback = FormatCommentsForPrompt(comments);
            var context = new OrchestrationContext(feedback, language);

            // Run classification loop
            while (context.Iteration <= MaxOrchestrationIterations)
            {
                _logger.LogInformation("=== Classification Iteration {Iteration} ===", context.Iteration);

                var classification = await ClassifyAsync(context, ct);
                _logger.LogInformation("Classification result: {Classification}", classification);

                if (classification == "SUCCESS")
                {
                    return new FromFeedbackResponse
                    {
                        CommentCount = comments.Count,
                        Classification = classification,
                        Message = "No actionable customization feedback found. All comments appear to be already addressed or informational."
                    };
                }

                if (classification == "FAILURE")
                {
                    if (context.Iteration < MaxOrchestrationIterations)
                    {
                        context.AddBuildError("Classification returned FAILURE, retrying...");
                        context.Iteration++;
                        continue;
                    }

                    return new FromFeedbackResponse
                    {
                        CommentCount = comments.Count,
                        Classification = classification,
                        ResponseError = "Classification failed after retries. Manual review required.",
                        Message = $"Context: {context.ToClassifierInput()}"
                    };
                }

                // Classification indicates work is needed (PHASE_A or similar)
                // Generate the issue content
                var issueTitle = "TESTING: [SDK Customization] Apply TypeSpec client customizations";
                var issueBody = GenerateIssueBody(comments, apiViewUrl, language, serviceName);

                var response = new FromFeedbackResponse
                {
                    CommentCount = comments.Count,
                    Classification = classification,
                    IssueTitle = issueTitle,
                    IssueBody = issueBody,
                    Comments = comments,
                    RepoOwner = DefaultRepoOwner,
                    RepoName = DefaultRepoName
                };

                // For MCP usage: provide next action instructions for the agent
                // For CLI usage: prompt interactively
                if (createIssue)
                {
                    response.NextAction = $"Use the mcp_github_create_issue tool to create a draft issue with owner='{DefaultRepoOwner}', repo='{DefaultRepoName}', title='{issueTitle}', and the issue body provided above.";
                }

                return response;
            }

            // Max iterations exceeded
            return new FromFeedbackResponse
            {
                CommentCount = comments.Count,
                ResponseError = $"Exceeded maximum classification iterations ({MaxOrchestrationIterations})",
                Message = $"Last context: {context.ToClassifierInput()}"
            };
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
    /// Formats consolidated comments for the classification prompt.
    /// </summary>
    private static string FormatCommentsForPrompt(List<ConsolidatedComment> comments)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## API Review Feedback\n");
        
        foreach (var comment in comments)
        {
            sb.AppendLine($"**Line {comment.LineNo}**: {comment.LineId}");
            if (!string.IsNullOrEmpty(comment.LineText))
            {
                sb.AppendLine($"Code: `{comment.LineText.Trim()}`");
            }
            sb.AppendLine($"Comment: {comment.Comment}");
            sb.AppendLine();
        }
        
        return sb.ToString();
    }

    private static string GenerateIssueBody(List<ConsolidatedComment> comments, string apiViewUrl, string? language, string? serviceName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("Apply TypeSpec client customizations to address API review feedback.");
        sb.AppendLine();
        sb.AppendLine("## Context");
        sb.AppendLine();
        sb.AppendLine($"- **APIView**: {apiViewUrl}");
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
        sb.AppendLine("## Feedback to Address");
        sb.AppendLine();
        sb.AppendLine("| Line | Code | Comment |");
        sb.AppendLine("|------|------|---------|");

        foreach (var comment in comments)
        {
            var lineId = string.IsNullOrEmpty(comment.LineId) ? "(general)" : comment.LineId;
            var lineText = string.IsNullOrEmpty(comment.LineText) ? "" : $"`{comment.LineText.Trim()}`";
            sb.AppendLine($"| {comment.LineNo} ({lineId}) | {lineText} | {comment.Comment} |");
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
    public int CommentCount { get; set; }
    public string? Message { get; set; }
    public string? Classification { get; set; }
    public string? IssueTitle { get; set; }
    public string? IssueBody { get; set; }
    public List<ConsolidatedComment>? Comments { get; set; }
    public string? RepoOwner { get; set; }
    public string? RepoName { get; set; }
    public string? NextAction { get; set; }
    public string? IssueUrl { get; set; }
    public int? IssueNumber { get; set; }

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
