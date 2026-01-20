using System.CommandLine;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.APIView;

public enum ContentType
{
    Text,
    CodeFile
}

[McpServerToolType]
[Description("APIView operations including comments and content")]
public class APIViewReviewTool : MCPMultiCommandTool
{
    // Sub-command constants
    private const string GetCommentsCmd = "get-comments";
    private const string GetContentCmd = "get-content";

    private const string ApiViewGetCommentsToolName = "azsdk_apiview_get_comments";

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.APIView];

    private readonly IAPIViewService _apiViewService;
    private readonly ILogger<APIViewReviewTool> _logger;

    private readonly Option<string> outputFileOption = new("--output-file"){Description = "Output file path to save the content"};
    private readonly Option<string> contentReturnTypeOption = new("--content-return-type")
    {
        Description = "The APIView content type (text or codefile). Defaults to 'text'.",
        DefaultValueFactory = _ => "text"
    };

    private readonly Option<string> apiViewUrlOption = new("--url")
    {
        Description = "The URL to the API review in APIView (e.g., https://apiview.dev/review/{reviewId}?activeApiRevisionId={revisionId})",
        Required = true
    };

    public APIViewReviewTool(ILogger<APIViewReviewTool> logger, IAPIViewService apiViewService)
    {
        _logger = logger;
        _apiViewService = apiViewService;
    }

    protected override List<Command> GetCommands() =>
    [
        new McpCommand(GetCommentsCmd, "Get comments for a specific APIView URL", ApiViewGetCommentsToolName) { apiViewUrlOption },
        new(GetContentCmd, "Get content by APIView URL") 
        {
            apiViewUrlOption, outputFileOption, contentReturnTypeOption
        }
    ];

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        string commandName = parseResult.CommandResult.Command.Name;
        APIViewResponse result = commandName switch
        {
            GetCommentsCmd => await GetComments(parseResult, ct),
            GetContentCmd => await GetContent(parseResult, ct),
            _ => new APIViewResponse { ResponseError = $"Unknown command: {commandName}" }
        };
        
        return result;
    }

    [McpServerTool(Name = ApiViewGetCommentsToolName), Description("Get all the comments of an APIView API using the APIView URL")]
    public async Task<APIViewResponse> GetComments(string apiViewUrl)
    {
        try
        {
            (string revisionId, _) = ExtractIdsFromUrl(apiViewUrl);
            string environment = GetEnvironmentFromUrl(apiViewUrl);

            string? result = await _apiViewService.GetCommentsByRevisionAsync(revisionId, environment);
            if (result == null)
            {
                return new APIViewResponse { ResponseError = $"Failed to retrieve comments for API View: {apiViewUrl}" };
            }

            return new APIViewResponse
            {
               Message = $"Comments retrieved successfully", Result = result
            };
        }
        catch (Exception ex)
        {
            return new APIViewResponse { ResponseError = $"Failed to get comments: {ex.Message}" };
        }
    }

    private async Task<APIViewResponse> GetComments(ParseResult parseResult, CancellationToken ct)
    {
        string? apiViewUrl = parseResult.GetValue(apiViewUrlOption);
        return await GetComments(apiViewUrl!);
    }

    private async Task<APIViewResponse> GetContent(ParseResult parseResult, CancellationToken ct)
    {
        string? apiViewUrl = parseResult.GetValue(apiViewUrlOption);
        string? outputFile = parseResult.GetValue(outputFileOption);
        string? contentType = parseResult.GetValue(contentReturnTypeOption);

        if (!Enum.TryParse<ContentType>(contentType, ignoreCase: true, out _))
        {
            var validValues = string.Join(", ", Enum.GetNames<ContentType>());
            return new APIViewResponse { ResponseError = $"Invalid content type '{contentType}'. Must be one of: {validValues}." };
        }

        (string revisionId, string reviewId) = ExtractIdsFromUrl(apiViewUrl!);
        string environment = GetEnvironmentFromUrl(apiViewUrl!);
        try
        {
            string? result = await _apiViewService.GetRevisionContent(revisionId, reviewId, contentType, environment);
            if (result == null)
            {
                return new APIViewResponse { ResponseError = $"Content not found" };
            }

            if (!string.IsNullOrEmpty(outputFile))
            {
                await File.WriteAllTextAsync(outputFile, result, ct);

                return new APIViewResponse
                {
                    Message = $"Content saved to file: {outputFile} ({result.Length:N0} characters)",
                };
            }

            return new APIViewResponse
            {
                Message = $"Content retrieved successfully ({result.Length:N0} characters)",
                Result = result
            };
        }
        catch (ArgumentException ex)
        {
            return new APIViewResponse { ResponseError = ex.Message };
        }
        catch (Exception ex)
        {
            return new APIViewResponse { ResponseError = $"Failed to get content: {ex.Message}" };
        }
    }

    private (string revisionId, string reviewId) ExtractIdsFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Input cannot be null or empty", nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ArgumentException("Input needs to be a valid APIView URL (e.g., https://apiview.dev/review/{reviewId}?activeApiRevisionId={revisionId})", nameof(url));
        }

        try
        {
            // Pattern: /review/{reviewId} in path and activeApiRevisionId={revisionId} in query string
            var match = Regex.Match(url, @"/review/([^/?]+).*[?&]activeApiRevisionId=([^&#]+)", RegexOptions.IgnoreCase);
            
            if (!match.Success)
            {
                throw new ArgumentException("APIView URL must contain both 'activeApiRevisionId' query parameter AND '/review/{reviewId}' path segment");
            }

            string reviewId = match.Groups[1].Value;
            string revisionId = match.Groups[2].Value;

            return (revisionId, reviewId);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            _logger.LogError(ex, "Failed to parse APIView URL {Url}", url);
            throw new ArgumentException($"Error parsing URL: {ex.Message}", nameof(url));
        }
    }

    private string GetEnvironmentFromUrl(string url)
    {
        // Check if APIVIEW_ENVIRONMENT is set and use it as override
        string? envOverride = Environment.GetEnvironmentVariable("APIVIEW_ENVIRONMENT");
        if (!string.IsNullOrEmpty(envOverride))
        {
            return envOverride;
        }

        // Auto-detect from URL
        if (url.Contains("apiviewstagingtest.com", StringComparison.OrdinalIgnoreCase))
        {
            return "staging";
        }
        if (url.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return "local";
        }
        return "production"; // Default for apiview.dev
    }
}
