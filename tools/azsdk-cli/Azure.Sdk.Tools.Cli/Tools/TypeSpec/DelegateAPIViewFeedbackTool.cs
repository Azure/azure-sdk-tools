// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec;

[Description("Delegate APIView feedback to GitHub Copilot coding agent for TypeSpec client customizations")]
public class DelegateAPIViewFeedbackTool : MCPTool
{
    private const string ToolName = "azsdk_tsp_delegate_apiview_feedback";
    
    private readonly IAPIViewFeedbackHelper _helper;
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<DelegateAPIViewFeedbackTool> _logger;

    private readonly Argument<string> _apiViewUrlArg = new("apiview-url")
    {
        Description = "APIView URL to fetch feedback from",
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Option<string?> _repoOption = new("--repo")
    {
        Description = "Target repository override (format: owner/repo). If not specified, derives from APIView metadata.",
        DefaultValueFactory = _ => null
    };

    private readonly Option<bool> _dryRunOption = new("--dry-run")
    {
        Description = "Preview issue content without creating it",
        DefaultValueFactory = _ => false
    };

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec];

    public DelegateAPIViewFeedbackTool(
        IAPIViewFeedbackHelper helper,
        IGitHubService gitHubService,
        ILogger<DelegateAPIViewFeedbackTool> logger)
    {
        _helper = helper;
        _gitHubService = gitHubService;
        _logger = logger;
    }

    protected override Command GetCommand() =>
        new McpCommand("delegate-apiview-feedback", "Delegate APIView feedback to coding agent for TypeSpec customizations", ToolName)
        {
            _apiViewUrlArg,
            _repoOption,
            _dryRunOption
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var apiViewUrl = parseResult.GetValue(_apiViewUrlArg);
        var repoOverride = parseResult.GetValue(_repoOption);
        var dryRun = parseResult.GetValue(_dryRunOption);

        try
        {
            _logger.LogInformation("Fetching APIView feedback from {Url}", apiViewUrl);

            // Get consolidated comments and metadata
            var comments = await _helper.GetConsolidatedComments(apiViewUrl!);
            var metadata = await _helper.GetMetadata(apiViewUrl!);

            if (comments == null || comments.Count == 0)
            {
                return new DefaultCommandResponse
                {
                    Message = "No actionable comments found in APIView"
                };
            }

            _logger.LogInformation("Found {Count} actionable comment(s) for {Package} ({Language})",
                comments.Count, metadata.PackageName, metadata.Language);

            // Detect commit SHA, TypeSpec project path, and target repo
            _logger.LogInformation("Detecting commit SHA and TypeSpec project path");
            var (commitSha, tspProjectPath, detectedRepo) = await _helper.DetectShaAndTspPath(metadata);

            // Use override repo if specified, otherwise use detected repo, fallback to default
            var targetRepo = !string.IsNullOrEmpty(repoOverride) ? repoOverride : detectedRepo ?? "Azure/azure-rest-api-specs";
            var repoParts = targetRepo.Split('/');
            var owner = repoParts[0];
            var repoName = repoParts[1];
            
            if (!string.IsNullOrEmpty(commitSha))
            {
                _logger.LogInformation("Detected commit SHA: {CommitSha}", commitSha);
            }
            if (!string.IsNullOrEmpty(tspProjectPath))
            {
                _logger.LogInformation("Detected TypeSpec project path: {TspProjectPath}", tspProjectPath);
            }
            _logger.LogInformation("Target repository: {Owner}/{Repo} (detected: {Detected}, override: {Override})", 
                owner, repoName, detectedRepo, repoOverride);

            // Build prompt using template
            var template = new APIViewFeedbackIssueTemplate(
                metadata.PackageName,
                metadata.Language,
                apiViewUrl!,
                comments,
                commitSha,
                tspProjectPath);
            
            var prompt = template.BuildPrompt();
            var title = $"Address APIView feedback for {metadata.PackageName} ({metadata.Language})";

            // Dry run check
            if (dryRun)
            {
                return new DefaultCommandResponse
                {
                    Message = $"=== DRY RUN ===\n\nTarget: {owner}/{repoName}\nTitle: {title}\n\n--- Issue Body ---\n{prompt}"
                };
            }

            // Create issue and assign to Copilot
            _logger.LogInformation("Creating issue in {Owner}/{Repo} and assigning to Copilot", owner, repoName);
            var assignees = new List<string> { "copilot-swe-agent[bot]" };
            var issue = await _gitHubService.CreateIssueAsync(owner, repoName, title, prompt, assignees);

            return new DefaultCommandResponse
            {
                Message = $"✓ Issue created: {issue.HtmlUrl}\n✓ Copilot assigned. Watch for the 👀 reaction and draft PR."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to address APIView feedback");
            return new DefaultCommandResponse
            {
                Message = $"Error: {ex.Message}"
            };
        }
    }

    [Description("Delegate APIView feedback to GitHub Copilot coding agent for TypeSpec client customizations")]
    public async Task<DefaultCommandResponse> DelegateAPIViewFeedbackAsync(
        string apiViewUrl,
        string repo = "Azure/azure-rest-api-specs",
        bool dryRun = false,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Fetching APIView feedback from {Url}", apiViewUrl);

            // Get consolidated comments and metadata
            var comments = await _helper.GetConsolidatedComments(apiViewUrl);
            var metadata = await _helper.GetMetadata(apiViewUrl);

            if (comments == null || comments.Count == 0)
            {
                return new DefaultCommandResponse
                {
                    Message = "No actionable comments found in APIView"
                };
            }

            // Detect commit SHA, TypeSpec project path, and target repo
            _logger.LogInformation("Detecting commit SHA and TypeSpec project path");
            var (commitSha, tspProjectPath, detectedRepo) = await _helper.DetectShaAndTspPath(metadata);

            // Use override repo if specified, otherwise use detected repo, fallback to default
            var targetRepo = !string.IsNullOrEmpty(repo) ? repo : detectedRepo ?? "Azure/azure-rest-api-specs";
            var repoParts = targetRepo.Split('/');
            var owner = repoParts[0];
            var repoName = repoParts[1];
            
            if (!string.IsNullOrEmpty(commitSha))
            {
                _logger.LogInformation("Detected commit SHA: {CommitSha}", commitSha);
            }
            if (!string.IsNullOrEmpty(tspProjectPath))
            {
                _logger.LogInformation("Detected TypeSpec project path: {TspProjectPath}", tspProjectPath);
            }

            // Build issue content using template
            var template = new APIViewFeedbackIssueTemplate(
                metadata.PackageName, 
                metadata.Language, 
                apiViewUrl, 
                comments,
                commitSha,
                tspProjectPath);
            var prompt = template.BuildPrompt();
            var title = $"Address APIView feedback for {metadata.PackageName} ({metadata.Language})";

            // Dry-run mode: preview issue
            if (dryRun)
            {
                return new DefaultCommandResponse
                {
                    Message = $"=== DRY RUN ===\n\nTarget: {owner}/{repoName}\nTitle: {title}\n\n--- Issue Body ---\n{prompt}"
                };
            }

            // Create issue and assign to Copilot
            _logger.LogInformation("Creating issue in {Owner}/{Repo} and assigning to Copilot", owner, repoName);
            var assignees = new List<string> { "copilot-swe-agent[bot]" };
            var issue = await _gitHubService.CreateIssueAsync(owner, repoName, title, prompt, assignees);

            return new DefaultCommandResponse
            {
                Message = $"✓ Issue created: {issue.HtmlUrl}\n✓ Copilot assigned. Watch for the 👀 reaction and draft PR."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to address APIView feedback");
            return new DefaultCommandResponse
            {
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
