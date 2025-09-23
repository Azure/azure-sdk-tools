using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.APIView;

[McpServerToolType]
[Description("Access APIView functionality including API structure, version diffs, comments, and review status")]
public class APIViewTool : MCPTool
{
    private static readonly string[] validContentTypes = ["Text", "CodeFile"];
    private static readonly string[] validRevisionSelectionTypes = ["Latest", "LatestApproved", "LatestAutomatic", "LatestManual", "Undefined"];

    private readonly IAPIViewService _apiViewService;
    private readonly ILogger<APIViewTool> _logger;

    private readonly Option<string> environmentOption = new("--environment",
        description: "The APIView environment (defaults to production)", getDefaultValue: () => "production");

    private readonly Option<string> outputFileOption = 
        new("--output-file", "Output file path to save the revision content");

    private readonly Option<string> contentReturnTypeOption = new("--content-return-type",
        description: "The APIView revision content type (text or codefile). Defaults to 'text'.", getDefaultValue: () => "text");
    private readonly Option<string> revisionSelectionTypeOption = new("--revision-selection-type",
        description: "The type of revision selection (Latest, latestApproved, LatestAutomatic, LatestManual)");

    private readonly Option<string> revisionIdOption =new("--revision-id", "The APIView revision ID");
    private readonly Option<string> reviewIdOptions = new("--review-id", "The APIView review ID");
    private readonly Option<string> apiViewUrlOption = new("--url", "The full APIView URL (alternative to --revision-id)");

    public APIViewTool(ILogger<APIViewTool> logger, IAPIViewService apiViewService)
    {
        _logger = logger;
        _apiViewService = apiViewService;
    }

    protected override Command GetCommand()
    {
        var revisionCommentsCmd = new Command("comments", "Get comments for a specific revision ID or APIView URL");
        revisionCommentsCmd.AddOption(revisionIdOption);
        revisionCommentsCmd.AddOption(apiViewUrlOption);
        revisionCommentsCmd.AddOption(environmentOption);
        revisionCommentsCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        
        var revisionContentCmd = new Command("content", "Get revision content by revision ID, review ID, or APIView URL (for revision)");
        revisionContentCmd.AddOption(revisionIdOption);
        revisionContentCmd.AddOption(reviewIdOptions);
        revisionContentCmd.AddOption(apiViewUrlOption);
        revisionContentCmd.AddOption(revisionSelectionTypeOption);
        revisionContentCmd.AddOption(environmentOption);
        revisionContentCmd.AddOption(outputFileOption);
        revisionContentCmd.AddOption(contentReturnTypeOption);
        revisionContentCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        
        var revisionCommand = new Command("revision", "APIView revision operations");
        revisionCommand.AddCommand(revisionCommentsCmd);
        revisionCommand.AddCommand(revisionContentCmd);
        
        var authCheckCmd = new Command("check", "Check APIView authentication status");
        authCheckCmd.AddOption(environmentOption);
        authCheckCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        
        var authGuidanceCmd = new Command("guidance", "Get APIView authentication guidance");
        authGuidanceCmd.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        
        var authCommand = new Command("auth", "APIView authentication operations");
        authCommand.AddCommand(authCheckCmd);
        authCommand.AddCommand(authGuidanceCmd);
        
        var rootCommand = new Command("apiview", "Access APIView functionality");
        rootCommand.AddCommand(revisionCommand);
        rootCommand.AddCommand(authCommand);
        
        return rootCommand;
    }

    public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        string commandName = ctx.ParseResult.CommandResult.Command.Name;
        string parentCommandName = ctx.ParseResult.CommandResult.Command.Parents.FirstOrDefault()?.Name ?? "";
        
        APIViewResponse result = (parentCommandName, commandName) switch
        {
            ("revision", "comments") => await GetRevisionComments(ctx, ct),
            ("revision", "content") => await GetRevisionContent(ctx, ct),
            ("auth", "check") => await CheckAuthentication(ctx, ct),
            ("auth", "guidance") => await GetAuthenticationGuidance(ctx, ct),
            _ => new APIViewResponse { ResponseError = $"Unknown command: {parentCommandName} {commandName}" }
        };

        return result;
    }

    [McpServerTool(Name = "azsdk_apiview_get_comments")]
    [Description("Get all the comments of an APIView revision by revision ID or APIView URL")]
    public async Task<APIViewResponse> GetRevisionComments(
        string revisionId,
        string? environment = null)
    {
        try
        {
            string actualRevisionId = ExtractRevisionIdFromInput(revisionId);
            string? result = await _apiViewService.GetCommentsByRevisionAsync(actualRevisionId, environment);
            if (result == null)
            {
                _logger.LogError("Failed to retrieve comments for revision {RevisionId}", actualRevisionId);
                return new APIViewResponse
                {
                    Success = false, ResponseError = $"Failed to retrieve comments for revision {actualRevisionId}"
                };
            }

            _logger.LogInformation("Comments retrieved successfully for revision {RevisionId}. Comments: {comments}", actualRevisionId, result);
            return new APIViewResponse
            {
                Success = true, Message = $"Comments retrieved successfully {result}", Data = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get comments for revision {RevisionId}", revisionId);
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
            _logger.LogInformation("Authentication status retrieved successfully. Status: {Status}", result);
            return new APIViewResponse
            {
                Success = true, Message = "Authentication status retrieved successfully", Data = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check authentication status");
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
            _logger.LogInformation("Authentication guidance retrieved successfully. Guidance: {Guidance}", result);
            return new APIViewResponse
            {
                Success = true, Message = "Authentication guidance retrieved successfully", Data = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get authentication guidance");
            return new APIViewResponse { ResponseError = $"Failed to get authentication guidance: {ex.Message}" };
        }
    }

    private async Task<APIViewResponse> GetRevisionComments(InvocationContext ctx, CancellationToken ct)
    {
        string? revisionId = ctx.ParseResult.GetValueForOption(revisionIdOption);
        string? apiViewUrl = ctx.ParseResult.GetValueForOption(apiViewUrlOption);
        string? environment = ctx.ParseResult.GetValueForOption(environmentOption);

        if (string.IsNullOrEmpty(revisionId) && string.IsNullOrEmpty(apiViewUrl))
        {
            _logger.LogError("Either --revision-id or --url must be provided");
            return new APIViewResponse { ResponseError = "Either --revision-id or --url must be provided" };
        }

        if (!string.IsNullOrEmpty(revisionId) && !string.IsNullOrEmpty(apiViewUrl))
        {
            _logger.LogError("Cannot specify both --revision-id and --url. Please use only one");
            return new APIViewResponse { ResponseError = "Cannot specify both --revision-id and --url. Please use only one" };
        }

        string input = !string.IsNullOrEmpty(revisionId) ? revisionId : apiViewUrl!;
        return await GetRevisionComments(input, environment);
    }

    private async Task<APIViewResponse> GetRevisionContent(InvocationContext ctx, CancellationToken ct)
    {
        string? revisionId = ctx.ParseResult.GetValueForOption(revisionIdOption);
        string? apiViewUrl = ctx.ParseResult.GetValueForOption(apiViewUrlOption);
        string? environment = ctx.ParseResult.GetValueForOption(environmentOption);
        string? outputFile = ctx.ParseResult.GetValueForOption(outputFileOption);
        string? contentType = ctx.ParseResult.GetValueForOption(contentReturnTypeOption);
        string? reviewId = ctx.ParseResult.GetValueForOption(reviewIdOptions);
        string? revisionSelectionType = ctx.ParseResult.GetValueForOption(revisionSelectionTypeOption);

        if (!validContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            string errorMessage = $"Invalid content type '{contentType}'. Must be one of: {string.Join(", ", validContentTypes)}.";
            _logger.LogError(errorMessage);
            return new APIViewResponse { ResponseError = errorMessage };
        }

        int revisionReviewInputCount = new[] { apiViewUrl, revisionId, reviewId }.Count(x => !string.IsNullOrEmpty(x));
        switch (revisionReviewInputCount)
        {
            case 0:
            {
                string errorMessage = "Either --revision-id, --review-id, or --url must be provided.";
                _logger.LogError(errorMessage);
                return new APIViewResponse { ResponseError = errorMessage };
            }
            case > 1:
            {
                string errorMessage = "Cannot specify multiple revision/review options. Use only one of: --revision-id, --review-id, or --url.";
                _logger.LogError(errorMessage);
                return new APIViewResponse { ResponseError = errorMessage };
            }
        }

        if (!string.IsNullOrEmpty(apiViewUrl))
        {
            if (!string.IsNullOrEmpty(revisionSelectionType))
            {
                string errorMessage = "Cannot specify --revision-selection-type with --url.";
                _logger.LogError(errorMessage);
                return new APIViewResponse { ResponseError = errorMessage };
            }

            try
            {
                revisionId = ExtractRevisionIdFromInput(apiViewUrl);
                revisionSelectionType = "Undefined";
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Failed to extract revision ID from URL");
                return new APIViewResponse { ResponseError = ex.Message };
            }
        }
        else if (!string.IsNullOrEmpty(revisionId))
        {
            if (!string.IsNullOrEmpty(revisionSelectionType))
            {
                string errorMessage = "Cannot specify --revision-selection-type with --revision-id.";
                _logger.LogError(errorMessage);
                return new APIViewResponse { ResponseError = errorMessage };
            }
            revisionSelectionType = "Undefined";
        }
        else
        {
            revisionSelectionType ??= "Latest";
            if (!validRevisionSelectionTypes.Contains(revisionSelectionType, StringComparer.OrdinalIgnoreCase))
            {
                string errorMessage = $"Invalid revision selection type '{revisionSelectionType}'. Must be one of: {string.Join(", ", validRevisionSelectionTypes)}.";
                _logger.LogError(errorMessage);
                return new APIViewResponse { ResponseError = errorMessage };
            }
        }

        try
        {
            string? result = await _apiViewService.GetRevisionContent(revisionId, reviewId, revisionSelectionType, contentType, environment);
            if (result == null)
            {
                _logger.LogError("Failed to retrieve revision content for revision {RevisionId}", revisionId);
                return new APIViewResponse
                {
                    Success = false,
                    ResponseError = $"Failed to retrieve revision content for revision {revisionId}"
                };
            }

            if (!string.IsNullOrEmpty(outputFile))
            {
                await File.WriteAllTextAsync(outputFile, result, ct);
                _logger.LogInformation("Revision content saved to file: {OutputFile} ({CharacterCount:N0} characters)", outputFile, result.Length);

                return new APIViewResponse
                {
                    Success = true,
                    Message = $"Revision content saved to file: {outputFile} ({result.Length:N0} characters)",
                    Data = $"File saved: {outputFile}"
                };
            }

            _logger.LogInformation("Revision content retrieved successfully: {result}", result);
            return new APIViewResponse
            {
                Success = true,
                Message = $"Revision content retrieved successfully ({result.Length:N0} characters)",
                Data = result
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided");
            return new APIViewResponse
            {
                Success = false,
                ResponseError = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get revision content for revision {RevisionId}", revisionId);
            return new APIViewResponse { ResponseError = $"Failed to get revision content: {ex.Message}" };
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

    private string ExtractRevisionIdFromInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Input cannot be null or empty", nameof(input));
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out Uri? uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                string? revisionId = query["activeApiRevisionId"];

                if (string.IsNullOrWhiteSpace(revisionId))
                {
                    throw new ArgumentException("APIView URL must contain 'activeApiRevisionId' parameter");
                }

                _logger.LogInformation("Extracted revision ID {RevisionId} from URL {Url}", revisionId, input);
                return revisionId;
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                throw new ArgumentException($"Error parsing URL: {ex.Message}", nameof(input));
            }
        }

        return input;
    }
}
