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

public enum ContentType
{
    Text,
    CodeFile
}

[McpServerToolType]
[Description("Access APIView functionality including API structure, version diffs, comments, and review status")]
public class APIViewTool : MCPTool
{
    private const string GetRevisionCommentsSubCommand = "get-revision-comments";
    private const string GetRevisionContentSubCommand = "get-revision-content";
    private const string ListReviewVersionsSubCommand = "list-review-versions";
    private const string CheckAuthenticationSubCommand = "check-authentication";
    private const string GetAuthenticationGuidanceSubCommand = "get-authentication-guidance";
    private readonly IAPIViewService _apiViewService;
    private readonly ILogger<APIViewTool> _logger;
    private readonly IOutputHelper _output;

    private readonly Option<string> environmentOption = new("--environment",
        description: "The APIView environment (defaults to production)", getDefaultValue: () => "production");

    private readonly Option<string> authTokenOption = new("--auth-token",
        "Authentication token for APIView (uses default authentication if not provided)");

    private readonly Option<string> outputFileOption = 
        new("--output-file", "Output file path to save the revision content");

    private readonly Option<string> contentTypeOption = new("--content-type",
        description: "The APIView revision content type (text or codefile). Defaults to 'text'.", getDefaultValue: () => "text");


    // Required options for different commands
    private readonly Option<string> revisionIdOption =
        new("--revision-id", "The APIView revision ID") { IsRequired = true };
    private readonly Option<string> reviewIdOptions = new("--review-id", "The APIView review ID") { IsRequired = true };

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
        Command getRevisionCommentsCmd = new(GetRevisionCommentsSubCommand, "Get comments for a specific APIView revision");
        getRevisionCommentsCmd.AddOption(revisionIdOption);
        getRevisionCommentsCmd.AddOption(environmentOption);
        getRevisionCommentsCmd.AddOption(authTokenOption);
        getRevisionCommentsCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        parentCommand.AddCommand(getRevisionCommentsCmd);

        Command getRevisionDiffCmd = new(GetRevisionContentSubCommand,
            "Get the APIView revision content as a text or codefile");
        getRevisionDiffCmd.AddOption(revisionIdOption);
        getRevisionDiffCmd.AddOption(environmentOption);
        getRevisionDiffCmd.AddOption(authTokenOption);
        getRevisionDiffCmd.AddOption(outputFileOption);
        getRevisionDiffCmd.AddOption(contentTypeOption);
        getRevisionDiffCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        parentCommand.AddCommand(getRevisionDiffCmd);

        // List revisions command
        Command listRevisionsCmd = new(ListReviewVersionsSubCommand, "List all versions for a review");
        listRevisionsCmd.AddOption(reviewIdOptions);
        listRevisionsCmd.AddOption(environmentOption);
        listRevisionsCmd.AddOption(authTokenOption);
        listRevisionsCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        parentCommand.AddCommand(listRevisionsCmd);

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
            GetRevisionContentSubCommand => await GetRevisionContent(ctx, ct),
            ListReviewVersionsSubCommand => await ListReviewVersions(ctx, ct),
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
            string? result = await _apiViewService.ListReviewVersions(reviewId, environment, authToken);
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

    private async Task<APIViewResponse> GetRevisionContent(InvocationContext ctx, CancellationToken ct)
    {
        string? revisionId = ctx.ParseResult.GetValueForOption(revisionIdOption);
        string? environment = ctx.ParseResult.GetValueForOption(environmentOption);
        string? authToken = ctx.ParseResult.GetValueForOption(authTokenOption);
        string? outputFile = ctx.ParseResult.GetValueForOption(outputFileOption);
        string? contentType = ctx.ParseResult.GetValueForOption(contentTypeOption);

        if (string.IsNullOrEmpty(revisionId))
        {
            SetFailure();
            return new APIViewResponse { ResponseError = "Revision ID and review ID are required" };
        }

        try
        {
            ContentType parsedContentType = ParseContentType(contentType);
            string? result = parsedContentType switch
            {
                ContentType.Text => await _apiViewService.GetRevisionContentText(revisionId, environment,
                    authToken),
                ContentType.CodeFile => await _apiViewService.GetRevisionCodeTokenFile(revisionId, environment,
                    authToken),
                _ => throw new ArgumentOutOfRangeException(nameof(parsedContentType), parsedContentType, "Unsupported content type")
            };

            if (result == null)
            {
                return new APIViewResponse
                {
                    Success = false,
                    ResponseError = $"Failed to retrieve revision content for revision {revisionId}"
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
        catch (ArgumentException ex)
        {
            // Handle content type parsing errors specifically
            return new APIViewResponse
            {
                Success = false,
                ResponseError = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get revision content for revision {RevisionId}", revisionId);
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

    private async Task<APIViewResponse> CheckAuthentication(InvocationContext ctx, CancellationToken ct)
    {
        string? environment = ctx.ParseResult.GetValueForOption(environmentOption);
        return await CheckAuthentication(environment);
    }

    private async Task<APIViewResponse> GetAuthenticationGuidance(InvocationContext ctx, CancellationToken ct)
    {
        return await GetAuthenticationGuidance();
    }

    private static ContentType ParseContentType(string? contentTypeString)
    {
        return contentTypeString?.ToLowerInvariant() switch
        {
            "text" => ContentType.Text,
            "codefile" => ContentType.CodeFile,
            _ => throw new ArgumentException($"Invalid content type '{contentTypeString}'. Valid values are: text, codefile")
        };
    }
}
