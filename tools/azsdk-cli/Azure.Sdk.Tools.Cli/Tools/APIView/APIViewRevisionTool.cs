using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.APIView;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.APIView;

[McpServerToolType]
[Description("APIView revision operations including comments and content")]
public class APIViewRevisionTool : MCPMultiCommandTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [
        SharedCommandGroups.APIView,
        SharedCommandGroups.APIViewRevision
    ];

    private static readonly string[] validContentTypes = ["Text", "CodeFile"];
    private static readonly string[] validRevisionFiltersTypes = ["Latest", "LatestApproved", "LatestAutomatic", "LatestManual", "Undefined"];

    private readonly IAPIViewService _apiViewService;
    private readonly ILogger<APIViewRevisionTool> _logger;

    private readonly Option<string> environmentOption = new("--environment",
        description: "The APIView environment (defaults to production)", getDefaultValue: () => "production");

    private readonly Option<string> outputFileOption = 
        new("--output-file", "Output file path to save the revision content");

    private readonly Option<string> contentReturnTypeOption = new("--content-return-type",
        description: "The APIView revision content type (text or codefile). Defaults to 'text'.", getDefaultValue: () => "text");
    private readonly Option<string> filterOption = new("--filter",
        description: "Filter the desired revision (Latest, LatestApproved, LatestAutomatic, LatestManual) - Required when reviewId is used");

    private readonly Option<string> revisionIdOption = new("--revision-id", "The APIView revision ID");
    private readonly Option<string> reviewIdOptions = new("--review-id", "The APIView review ID");
    private readonly Option<string> apiViewUrlOption = new("--url", "The full APIView URL (alternative to --revision-id)");

    public APIViewRevisionTool(ILogger<APIViewRevisionTool> logger, IAPIViewService apiViewService)
    {
        _logger = logger;
        _apiViewService = apiViewService;
    }

    protected override List<Command> GetCommands()
    {
        var revisionCommentsCmd = new Command("comments", "Get comments for a specific revision ID or APIView URL")
        {
            revisionIdOption, apiViewUrlOption, environmentOption
        };
        var revisionContentCmd = new Command("content", "Get revision content by revision ID, review ID, or APIView URL (for revision)")
        {
            revisionIdOption,
            reviewIdOptions,
            apiViewUrlOption,
            filterOption,
            environmentOption,
            outputFileOption,
            contentReturnTypeOption
        };

        return [revisionCommentsCmd, revisionContentCmd];
    }

    public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        string commandName = ctx.ParseResult.CommandResult.Command.Name;
        
        APIViewResponse result = commandName switch
        {
            "comments" => await GetRevisionComments(ctx, ct),
            "content" => await GetRevisionContent(ctx, ct),
            _ => new APIViewResponse { ResponseError = $"Unknown revision command: {commandName}" }
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
                return new APIViewResponse { ResponseError = $"Failed to retrieve comments for revision {actualRevisionId}" };
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
        string? filter = ctx.ParseResult.GetValueForOption(filterOption);

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
            try
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    reviewId = ExtractReviewIdFromUrl(apiViewUrl);
                }
                else
                {
                    revisionId = ExtractRevisionIdFromInput(apiViewUrl);
                    filter = "Undefined";
                }
            }
            catch (ArgumentException ex) when (ex.Message.Contains("activeApiRevisionId"))
            {
                string errorMessage = "Failed to extract revision ID from URL. URL must contain 'activeApiRevisionId' parameter.";
                _logger.LogError(ex, errorMessage);
                return new APIViewResponse { ResponseError = errorMessage};
            }
            catch (ArgumentException ex) when (ex.Message.Contains("/review/"))
            {
                string errorMessage = "Failed to extract review ID from URL. URL path must contain '/review/{reviewId}' segment.";
                _logger.LogError(ex, errorMessage);
                return new APIViewResponse { ResponseError = errorMessage };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while parsing URL");
                return new APIViewResponse { ResponseError = $"Failed to parse URL: {ex.Message}" };
            }
        }
        else if (!string.IsNullOrEmpty(revisionId))
        {
            if (!string.IsNullOrEmpty(filter))
            {
                string errorMessage = "Cannot specify --revision-filter with --revision-id.";
                _logger.LogError(errorMessage);
                return new APIViewResponse { ResponseError = errorMessage };
            }
            filter = "Undefined";
        }
        else
        {
            filter ??= "Latest";
            if (!validRevisionFiltersTypes.Contains(filter, StringComparer.OrdinalIgnoreCase))
            {
                string errorMessage = $"Invalid revision filter '{filter}'. Must be one of: {string.Join(", ", validRevisionFiltersTypes)}.";
                _logger.LogError(errorMessage);
                return new APIViewResponse { ResponseError = errorMessage };
            }
        }

        try
        {
            string? result = await _apiViewService.GetRevisionContent(revisionId, reviewId, filter, contentType, environment);
            if (result == null)
            {
                _logger.LogError($"Revision content not found");
                return new APIViewResponse { ResponseError = $"Revision content not found" };
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
            return new APIViewResponse { ResponseError = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get revision content for revision {RevisionId}", revisionId);
            return new APIViewResponse { ResponseError = $"Failed to get revision content: {ex.Message}" };
        }
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

    private string? ExtractReviewIdFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Input cannot be null or empty", nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ArgumentException("URL must be a valid HTTP or HTTPS URL", nameof(url));
        }

        try
        {
            const string reviewSegment = "/review/";
            int reviewIndex = uri.AbsolutePath.IndexOf(reviewSegment, StringComparison.OrdinalIgnoreCase);
            
            if (reviewIndex >= 0)
            {
                int startIndex = reviewIndex + reviewSegment.Length;
                int endIndex = uri.AbsolutePath.IndexOf('/', startIndex);
                
                string reviewId = endIndex > startIndex 
                    ? uri.AbsolutePath.Substring(startIndex, endIndex - startIndex)
                    : uri.AbsolutePath.Substring(startIndex);
                
                if (!string.IsNullOrWhiteSpace(reviewId))
                {
                    _logger.LogInformation("Extracted review ID {ReviewId} from URL {Url}", reviewId, url);
                    return reviewId;
                }
            }

            throw new ArgumentException("URL path must contain '/review/{reviewId}' segment");
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            _logger.LogWarning(ex, "Failed to extract review ID from URL {Url}", url);
            throw new ArgumentException($"Error parsing URL path: {ex.Message}", nameof(url));
        }
    }
}
