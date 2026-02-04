// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.CodeCustomization;

[McpServerToolType, Description("Address APIView feedback by delegating to GitHub Copilot coding agent")]
public class AddressFeedbackTool : MCPTool
{
    private const string ToolName = "azsdk_review_address_feedback";
    
    private readonly IAPIViewFeedbackCustomizationsHelpers _helper;
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<AddressFeedbackTool> _logger;

    private readonly Argument<string> _apiViewUrlArg = new("apiview-url")
    {
        Description = "APIView URL to fetch feedback from",
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Option<string> _repoOption = new("--repo")
    {
        Description = "Target repository (format: owner/repo)",
        DefaultValueFactory = _ => "Azure/azure-rest-api-specs"
    };

    private readonly Option<bool> _dryRunOption = new("--dry-run")
    {
        Description = "Preview issue content without creating it",
        DefaultValueFactory = _ => false
    };

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Review];

    public AddressFeedbackTool(
        IAPIViewFeedbackCustomizationsHelpers helper,
        IGitHubService gitHubService,
        ILogger<AddressFeedbackTool> logger)
    {
        _helper = helper;
        _gitHubService = gitHubService;
        _logger = logger;
    }

    protected override Command GetCommand() =>
        new McpCommand("address-feedback", "Delegate APIView feedback to coding agent", ToolName)
        {
            _apiViewUrlArg,
            _repoOption,
            _dryRunOption
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var apiViewUrl = parseResult.GetValue(_apiViewUrlArg);
        var repo = parseResult.GetValue(_repoOption);
        var dryRun = parseResult.GetValue(_dryRunOption);

        try
        {
            // Validate APIView URL
            if (!IsValidApiViewUrl(apiViewUrl!))
            {
                return new DefaultCommandResponse
                {
                    Message = "Invalid APIView URL. Expected format: https://apiview.dev/... or https://apiview-staging.dev/..."
                };
            }

            // Validate repo format
            var repoParts = repo!.Split('/');
            if (repoParts.Length != 2)
            {
                return new DefaultCommandResponse
                {
                    Message = $"Invalid repository format: '{repo}'. Expected format: owner/repo"
                };
            }
            var owner = repoParts[0];
            var repoName = repoParts[1];

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

            // Detect commit SHA and TypeSpec project path
            _logger.LogInformation("Detecting commit SHA and TypeSpec project path");
            var (commitSha, tspProjectPath) = await _helper.DetectShaAndTspPath(metadata, owner, repoName);
            
            if (!string.IsNullOrEmpty(commitSha))
            {
                _logger.LogInformation("Detected commit SHA: {CommitSha}", commitSha);
            }
            if (!string.IsNullOrEmpty(tspProjectPath))
            {
                _logger.LogInformation("Detected TypeSpec project path: {TspProjectPath}", tspProjectPath);
            }

            // Build prompt using template
            var template = new APIViewFeedbackIssueTemplate(
                metadata.PackageName,
                metadata.Language,
                apiViewUrl!,
                comments,
                commitSha,
                tspProjectPath);
            
            var prompt = template.BuildPrompt();
            var title = $"[SDK Customization] Apply TypeSpec client customizations for {metadata.PackageName} ({metadata.Language})";

            // Dry run check
            if (dryRun)
            {
                return new DefaultCommandResponse
                {
                    Message = $"=== DRY RUN ===\n\nTarget: {owner}/{repoName}\nTitle: {title}\n\n--- Issue Body ---\n{prompt}\n\n--- Next step: Assign issue to Copilot coding agent ---"
                };
            }

            // Create issue
            _logger.LogInformation("Creating issue in {Owner}/{Repo}", owner, repoName);
            var issue = await _gitHubService.CreateIssueAsync(owner, repoName, title, prompt);

            return new DefaultCommandResponse
            {
                Message = $"✓ Issue created: {issue.HtmlUrl}\n\n⚠️ Next step: Assign Copilot to this issue to start the coding agent.\n   In the issue, click 'Assignees' and select 'Copilot'."
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

    [McpServerTool(Name = ToolName), Description("Address APIView feedback by delegating to GitHub Copilot coding agent")]
    public async Task<DefaultCommandResponse> AddressFeedbackAsync(
        string apiViewUrl,
        string repo = "Azure/azure-rest-api-specs",
        bool dryRun = false,
        CancellationToken ct = default)
    {
        try
        {
            // Validate APIView URL
            if (!IsValidApiViewUrl(apiViewUrl))
            {
                return new DefaultCommandResponse
                {
                    Message = "Invalid APIView URL. Expected format: https://apiview.dev/... or https://apiview-staging.dev/..."
                };
            }

            // Validate repo format
            var repoParts = repo.Split('/');
            if (repoParts.Length != 2)
            {
                return new DefaultCommandResponse
                {
                    Message = $"Invalid repository format: '{repo}'. Expected format: owner/repo"
                };
            }
            var owner = repoParts[0];
            var repoName = repoParts[1];

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

            // Detect commit SHA and TypeSpec project path
            _logger.LogInformation("Detecting commit SHA and TypeSpec project path");
            var (commitSha, tspProjectPath) = await _helper.DetectShaAndTspPath(metadata, owner, repoName);
            
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
            var title = $"[TypeSpec] Address APIView feedback for {metadata.PackageName}";

            // Dry-run mode: preview issue
            if (dryRun)
            {
                return new DefaultCommandResponse
                {
                    Message = $"=== DRY RUN ===\n\nTarget: {owner}/{repoName}\nTitle: {title}\n\n--- Issue Body ---\n{prompt}\n\n--- Next step: Assign issue to Copilot coding agent ---"
                };
            }

            // Create issue
            _logger.LogInformation("Creating issue in {Owner}/{Repo}", owner, repoName);
            var issue = await _gitHubService.CreateIssueAsync(owner, repoName, title, prompt);

            return new DefaultCommandResponse
            {
                Message = $"✓ Issue created: {issue.HtmlUrl}\n\n⚠️ Next step: Assign Copilot to this issue to start the coding agent.\n   In the issue, click 'Assignees' and select 'Copilot'."
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

    private static bool IsValidApiViewUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return url.StartsWith("https://apiview.dev/", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://apiview-staging.dev/", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://spa.apiviewstagingtest.com/", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://spa.apiview.dev/", StringComparison.OrdinalIgnoreCase);
    }
}
