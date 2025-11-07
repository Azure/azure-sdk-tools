using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.APIView;

[McpServerToolType]
[Description("APIView revision operations including comments and content")]
public class APIViewRevisionTool : MCPMultiCommandTool
{
    // Sub-command constants
    private const string GetComments = "get-comments";
    private const string GetContent = "get-content";

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.APIView];

    private static readonly string[] validContentTypes = ["Text", "CodeFile"];

    private readonly IAPIViewService _apiViewService;
    private readonly ILogger<APIViewRevisionTool> _logger;

    private readonly Option<string> environmentOption = new("--environment")
    {
        Description = "The APIView environment (defaults to production)",
        DefaultValueFactory = _ => "production"
    };

    private readonly Option<string> outputFileOption = new("--output-file"){Description = "Output file path to save the revision content"};
    private readonly Option<string> contentReturnTypeOption = new("--content-return-type")
    {
        Description = "The APIView revision content type (text or codefile). Defaults to 'text'.",
        DefaultValueFactory = _ => "text"
    };

    private readonly Option<string> apiViewUrlOption = new("--url"){Description = "The full APIView URL"};

    public APIViewRevisionTool(ILogger<APIViewRevisionTool> logger, IAPIViewService apiViewService)
    {
        _logger = logger;
        _apiViewService = apiViewService;
    }

    protected override List<Command> GetCommands() =>
    [
        new(GetComments, "Get comments for a specific revision ID or APIView URL") { apiViewUrlOption, environmentOption },
        new(GetContent, "Get revision content by revision ID, review ID, or APIView URL (for revision)") 
        {
            apiViewUrlOption, environmentOption, outputFileOption, contentReturnTypeOption
        }
    ];

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        string commandName = parseResult.CommandResult.Command.Name;
        APIViewResponse result = commandName switch
        {
            GetComments => await GetRevisionComments(parseResult, ct),
            GetContent => await GetRevisionContent(parseResult, ct),
            _ => new APIViewResponse { ResponseError = $"Unknown revision command: {commandName}" }
        };
        
        return result;
    }

    [McpServerTool(Name = "azsdk_apiview_get_comments")]
    [Description("Get all the comments of an APIView API using the APIView URL")]
    public async Task<APIViewResponse> GetRevisionComments(
        string apiViewUrl,
        string? environment = null)
    {
        try
        {
            (string? revisionId, _) = ExtractIdsFromUrl(apiViewUrl);
            
            if (string.IsNullOrWhiteSpace(revisionId))
            {
                return new APIViewResponse { ResponseError = "APIView URL must contain 'activeApiRevisionId' query parameter to retrieve comments" };
            }

            string? result = await _apiViewService.GetCommentsByRevisionAsync(revisionId, environment);
            if (result == null)
            {
                return new APIViewResponse { ResponseError = $"Failed to retrieve comments for revision {revisionId}" };
            }

            return new APIViewResponse
            {
                Success = true, Message = $"Comments retrieved successfully", Result = result
            };
        }
        catch (Exception ex)
        {
            return new APIViewResponse { ResponseError = $"Failed to get comments: {ex.Message}" };
        }
    }

    private async Task<APIViewResponse> GetRevisionComments(ParseResult parseResult, CancellationToken ct)
    {
        string? apiViewUrl = parseResult.GetValue(apiViewUrlOption);
        string? environment = parseResult.GetValue(environmentOption);

        if ( string.IsNullOrEmpty(apiViewUrl))
        {
            _logger.LogError("--url must be provided");
            return new APIViewResponse { ResponseError = "--url must be provided" };
        }

        return await GetRevisionComments(apiViewUrl, environment);
    }

    private async Task<APIViewResponse> GetRevisionContent(ParseResult parseResult, CancellationToken ct)
    {
        string? apiViewUrl = parseResult.GetValue(apiViewUrlOption);
        string? environment = parseResult.GetValue(environmentOption);
        string? outputFile = parseResult.GetValue(outputFileOption);
        string? contentType = parseResult.GetValue(contentReturnTypeOption);

        if (!validContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return new APIViewResponse { ResponseError = $"Invalid content type '{contentType}'. Must be one of: {string.Join(", ", validContentTypes)}." };
        }

        if (string.IsNullOrEmpty(apiViewUrl))
        {
            return new APIViewResponse { ResponseError = "--url must be provided" };
        }

        (string? revisionId, string? reviewId) = ExtractIdsFromUrl(apiViewUrl);
        try
        {
            string? result = await _apiViewService.GetRevisionContent(revisionId, reviewId, contentType, environment);
            if (result == null)
            {
                return new APIViewResponse { ResponseError = $"Revision content not found" };
            }

            if (!string.IsNullOrEmpty(outputFile))
            {
                await File.WriteAllTextAsync(outputFile, result, ct);

                return new APIViewResponse
                {
                    Success = true,
                    Message = $"Revision content saved to file: {outputFile} ({result.Length:N0} characters)",
                };
            }

            return new APIViewResponse
            {
                Success = true,
                Message = $"Revision content retrieved successfully ({result.Length:N0} characters)",
                Content = result
            };
        }
        catch (ArgumentException ex)
        {
            return new APIViewResponse { ResponseError = ex.Message };
        }
        catch (Exception ex)
        {
            return new APIViewResponse { ResponseError = $"Failed to get revision content: {ex.Message}" };
        }
    }

    private (string? revisionId, string? reviewId) ExtractIdsFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Input cannot be null or empty", nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ArgumentException("Input needs to be a valid APIView URL (e.g., https://apiview.dev/review/{reviewId}?activeApiRevisionId={revisionId})", nameof(url));
        }

        string? reviewId = null;

        try
        {
            // Extract revision ID from query string
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            string? revisionId = query["activeApiRevisionId"];

            // Extract review ID from path
            const string reviewSegment = "/review/";
            int reviewIndex = uri.AbsolutePath.IndexOf(reviewSegment, StringComparison.OrdinalIgnoreCase);
            
            if (reviewIndex >= 0)
            {
                int startIndex = reviewIndex + reviewSegment.Length;
                int endIndex = uri.AbsolutePath.IndexOf('/', startIndex);
                
                reviewId = endIndex > startIndex 
                    ? uri.AbsolutePath.Substring(startIndex, endIndex - startIndex)
                    : uri.AbsolutePath.Substring(startIndex);
            }

            if (string.IsNullOrWhiteSpace(revisionId) || string.IsNullOrWhiteSpace(reviewId))
            {
                throw new ArgumentException("APIView URL must contain both 'activeApiRevisionId' query parameter AND '/review/{reviewId}' path segment");
            }

            return (revisionId, reviewId);
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            _logger.LogError(ex, "Failed to parse APIView URL {Url}", url);
            throw new ArgumentException($"Error parsing URL: {ex.Message}", nameof(url));
        }
    }
}
