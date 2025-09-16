using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.APIView;

[McpServerToolType]
[Description("Access APIView functionality including API structure, version diffs, comments, and review status")]
public class APIViewTool : MCPTool
{
    private const string GetRevisionCommentsSubCommand = "get-revision-comments";
    private const string GetTokenFileForRevisionSubCommand = "get-token-file-for-revision";
    private const string ListReviewVersionsSubCommand = "list-review-versions";
    private const string GetLatestRevisionSubCommand = "get-latest-revision";
    private const string CheckAuthenticationSubCommand = "check-authentication";
    private const string GetAuthenticationGuidanceSubCommand = "get-authentication-guidance";
    private readonly IAPIViewService _apiViewService;
    private readonly ILogger<APIViewTool> _logger;
    private readonly IOutputHelper _output;

    private readonly Option<string> environmentOption = new("--environment",
        description: "The APIView environment (defaults to production)", getDefaultValue: () => "production");

    private readonly Option<string> authTokenOption = new("--auth-token",
        "Authentication token for APIView (uses default authentication if not provided)");

    // Optional options
    private readonly Option<string> diffRevisionIdOption =
        new("--diff-revision-id", "The APIView diff revision ID for comparison (optional)");

    private readonly Option<string> reviewIdOptions = new("--review-id", "The APIView review ID") { IsRequired = true };

    private readonly Option<string> outputFileOption = 
        new("--output-file", "Output file path to save the revision content");

    // Required options for different commands
    private readonly Option<string> revisionIdOption =
        new("--revision-id", "The APIView revision ID") { IsRequired = true };

    public APIViewTool(ILogger<APIViewTool> logger, IOutputHelper output, IAPIViewService apiViewService)
    {
        _logger = logger;
        _output = output;
        _apiViewService = apiViewService;

        CommandHierarchy =
        [
            SharedCommandGroups.APIView
        ];
    }

    public override Command GetCommand()
    {
        Command parentCommand = new("api", "Access APIView functionality");

        // Get revision comments command
        Command getRevisionCommentsCmd =
            new(GetRevisionCommentsSubCommand, "Get comments for a specific APIView revision");
        getRevisionCommentsCmd.AddOption(revisionIdOption);
        getRevisionCommentsCmd.AddOption(environmentOption);
        getRevisionCommentsCmd.AddOption(authTokenOption);
        getRevisionCommentsCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        parentCommand.AddCommand(getRevisionCommentsCmd);

        Command getRevisionDiffCmd = new(GetTokenFileForRevisionSubCommand,
            "Get the token file for a specific APIView revision or diff revision");
        getRevisionDiffCmd.AddOption(revisionIdOption);
        getRevisionDiffCmd.AddOption(diffRevisionIdOption);
        getRevisionDiffCmd.AddOption(reviewIdOptions);
        getRevisionDiffCmd.AddOption(environmentOption);
        getRevisionDiffCmd.AddOption(authTokenOption);
        getRevisionDiffCmd.AddOption(outputFileOption);
        getRevisionDiffCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        parentCommand.AddCommand(getRevisionDiffCmd);

        // List revisions command
        Command listRevisionsCmd = new(ListReviewVersionsSubCommand, "List all versions for a review");
        listRevisionsCmd.AddOption(reviewIdOptions);
        listRevisionsCmd.AddOption(environmentOption);
        listRevisionsCmd.AddOption(authTokenOption);
        listRevisionsCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        parentCommand.AddCommand(listRevisionsCmd);

        // Get latest revision command
        Command getLatestRevisionCmd = new(GetLatestRevisionSubCommand, "Get the latest revision for a review");
        getLatestRevisionCmd.AddOption(reviewIdOptions);
        getLatestRevisionCmd.AddOption(environmentOption);
        getLatestRevisionCmd.AddOption(authTokenOption);
        getLatestRevisionCmd.AddOption(outputFileOption);
        getLatestRevisionCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        parentCommand.AddCommand(getLatestRevisionCmd);

        // Authentication commands
        Command checkAuthCmd = new(CheckAuthenticationSubCommand, "Check APIView authentication status");
        checkAuthCmd.AddOption(environmentOption);
        checkAuthCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        parentCommand.AddCommand(checkAuthCmd);

        Command getAuthGuidanceCmd = new(GetAuthenticationGuidanceSubCommand, "Get APIView authentication guidance");
        getAuthGuidanceCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        parentCommand.AddCommand(getAuthGuidanceCmd);

        return parentCommand;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        string commandName = ctx.ParseResult.CommandResult.Command.Name;
        APIViewResponse result = commandName switch
        {
            GetRevisionCommentsSubCommand => await GetRevisionComments(ctx, ct),
            GetTokenFileForRevisionSubCommand => await GetTokenCodeFileForRevision(ctx, ct),
            ListReviewVersionsSubCommand => await ListReviewVersions(ctx, ct),
            GetLatestRevisionSubCommand => await GetLatestRevision(ctx, ct),
            CheckAuthenticationSubCommand => await CheckAuthentication(ctx, ct),
            GetAuthenticationGuidanceSubCommand => await GetAuthenticationGuidance(ctx, ct),
            _ => new APIViewResponse { ResponseError = $"Unknown command: {commandName}" }
        };

        ctx.ExitCode = ExitCode;
        _output.Output(result);
    }

    [McpServerTool(Name = "azsdk_apiview_get_comments")]
    [Description("Get all the comments of an APIView revision")]
    public async Task<APIViewResponse> GetRevisionComments(
        string revisionId,
        string? environment = null,
        string? authToken = null)
    {
        try
        {
            string? result = await _apiViewService.GetCommentByRevisionAsync(revisionId, environment, authToken);
            if (result == null)
            {
                return new APIViewResponse
                {
                    Success = false, ResponseError = $"Failed to retrieve comments for revision {revisionId}"
                };
            }

            return new APIViewResponse
            {
                Success = true, Message = $"Comments retrieved successfully {result}", Data = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get comments for revision {RevisionId}", revisionId);
            SetFailure();
            return new APIViewResponse { ResponseError = $"Failed to get comments: {ex.Message}" };
        }
    }



    [McpServerTool(Name = "azsdk_apiview_check_authentication")]
    [Description("Check APIView authentication status and available credentials")]
    public async Task<APIViewResponse> CheckAuthentication(string? environment = null)
    {
        try
        {
            string result = await _apiViewService.CheckAuthenticationStatusAsync(environment);
            return new APIViewResponse
            {
                Success = true, Message = "Authentication status retrieved successfully", Data = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check authentication status");
            SetFailure();
            return new APIViewResponse { ResponseError = $"Failed to check authentication: {ex.Message}" };
        }
    }

    [McpServerTool(Name = "azsdk_apiview_get_authentication_guidance")]
    [Description("Get detailed guidance on how to authenticate with APIView")]
    public async Task<APIViewResponse> GetAuthenticationGuidance()
    {
        try
        {
            string result = await _apiViewService.GetAuthenticationGuidanceAsync();
            return new APIViewResponse
            {
                Success = true, Message = "Authentication guidance retrieved successfully", Data = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get authentication guidance");
            SetFailure();
            return new APIViewResponse { ResponseError = $"Failed to get authentication guidance: {ex.Message}" };
        }
    }

    [McpServerTool(Name = "azsdk_apiview_list_review_versions")]
    [Description("List all versions for an APIView review")]
    public async Task<APIViewResponse> ListReviewVersions(
        string reviewId,
        string? environment = null,
        string? authToken = null)
    {
        try
        {
            string? result = await _apiViewService.ListReviewVersions(reviewId, environment ?? "production", authToken);
            if (result == null)
            {
                return new APIViewResponse
                {
                    Success = false, ResponseError = $"Failed to retrieve revisions for review {reviewId}"
                };
            }

            return new APIViewResponse
            {
                Success = true, Message = $"Revisions retrieved successfully for review {reviewId}", Data = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get revisions for review {ReviewId}", reviewId);
            SetFailure();
            return new APIViewResponse { ResponseError = $"Failed to get review revisions: {ex.Message}" };
        }
    }



    private async Task<APIViewResponse> GetRevisionComments(InvocationContext ctx, CancellationToken ct)
    {
        string? revisionId = ctx.ParseResult.GetValueForOption(revisionIdOption);
        string? environment = ctx.ParseResult.GetValueForOption(environmentOption);
        string? authToken = ctx.ParseResult.GetValueForOption(authTokenOption);

        if (string.IsNullOrEmpty(revisionId))
        {
            SetFailure();
            return new APIViewResponse { ResponseError = "Revision ID is required" };
        }

        return await GetRevisionComments(revisionId, environment, authToken);
    }

    private async Task<APIViewResponse> GetTokenCodeFileForRevision(InvocationContext ctx, CancellationToken ct)
    {
        string? revisionId = ctx.ParseResult.GetValueForOption(revisionIdOption);
        string? diffRevisionId = ctx.ParseResult.GetValueForOption(diffRevisionIdOption);
        string? reviewId = ctx.ParseResult.GetValueForOption(reviewIdOptions);
        string? environment = ctx.ParseResult.GetValueForOption(environmentOption);
        string? authToken = ctx.ParseResult.GetValueForOption(authTokenOption);
        string? outputFile = ctx.ParseResult.GetValueForOption(outputFileOption);

        if (string.IsNullOrEmpty(revisionId) || string.IsNullOrEmpty(reviewId))
        {
            SetFailure();
            return new APIViewResponse { ResponseError = "Revision ID and review ID are required" };
        }

        try
        {
            string? result = await _apiViewService.GetRevisionContent(reviewId, revisionId, diffRevisionId, environment ?? "production", authToken);
            if (result == null)
            {
                return new APIViewResponse
                {
                    Success = false,
                    ResponseError = $"Failed to retrieve revision content for revisions {revisionId} and {diffRevisionId}"
                };
            }

            if (!string.IsNullOrEmpty(outputFile))
            {
                await File.WriteAllTextAsync(outputFile, result, ct);
                return new APIViewResponse
                {
                    Success = true,
                    Message = $"Revision content saved to file: {outputFile} ({result.Length:N0} characters)",
                    Data = $"File saved: {outputFile}"
                };
            }

            return new APIViewResponse
            {
                Success = true,
                Message = $"Revision content retrieved successfully ({result.Length:N0} characters)",
                Data = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get revision content between revisions {RevisionId} and {DiffRevisionId}",
                revisionId, diffRevisionId);
            SetFailure();
            return new APIViewResponse { ResponseError = $"Failed to get revision content: {ex.Message}" };
        }
    }

    private async Task<APIViewResponse> ListReviewVersions(InvocationContext ctx, CancellationToken ct)
    {
        string? reviewId = ctx.ParseResult.GetValueForOption(reviewIdOptions);
        string? environment = ctx.ParseResult.GetValueForOption(environmentOption);
        string? authToken = ctx.ParseResult.GetValueForOption(authTokenOption);

        if (string.IsNullOrEmpty(reviewId))
        {
            SetFailure();
            return new APIViewResponse { ResponseError = "Review ID is required" };
        }

        return await ListReviewVersions(reviewId, environment, authToken);
    }

    private async Task<APIViewResponse> GetLatestRevision(InvocationContext ctx, CancellationToken ct)
    {
        string? reviewId = ctx.ParseResult.GetValueForOption(reviewIdOptions);
        string? environment = ctx.ParseResult.GetValueForOption(environmentOption);
        string? authToken = ctx.ParseResult.GetValueForOption(authTokenOption);
        string? outputFile = ctx.ParseResult.GetValueForOption(outputFileOption);

        if (string.IsNullOrEmpty(reviewId))
        {
            SetFailure();
            return new APIViewResponse { ResponseError = "Review ID is required" };
        }

        try
        {
            string? result = await _apiViewService.GetLatestRevisionAsync(reviewId, environment ?? "production", authToken);
            if (result == null)
            {
                return new APIViewResponse
                {
                    Success = false, ResponseError = $"Failed to retrieve latest revision for review {reviewId}"
                };
            }

            if (!string.IsNullOrEmpty(outputFile))
            {
                await File.WriteAllTextAsync(outputFile, result, ct);
                return new APIViewResponse
                {
                    Success = true,
                    Message = $"Latest revision saved to file: {outputFile} ({result.Length:N0} characters)",
                    Data = $"File saved: {outputFile}"
                };
            }

            return new APIViewResponse
            {
                Success = true,
                Message = $"Latest revision retrieved successfully ({result.Length:N0} characters)",
                Data = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest revision for review {ReviewId}", reviewId);
            SetFailure();
            return new APIViewResponse { ResponseError = $"Failed to get latest revision: {ex.Message}" };
        }
    }

    private async Task<APIViewResponse> CheckAuthentication(InvocationContext ctx, CancellationToken ct)
    {
        string? environment = ctx.ParseResult.GetValueForOption(environmentOption);
        return await CheckAuthentication(environment);
    }

    private async Task<APIViewResponse> GetAuthenticationGuidance(InvocationContext ctx, CancellationToken ct)
    {
        return await GetAuthenticationGuidance();
    }
}
