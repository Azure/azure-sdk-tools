using System.CommandLine;
using System.ComponentModel;
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
            (string revisionId, _) = ApiViewUrlParser.ExtractIds(apiViewUrl);
            string environment = IAPIViewHttpService.DetectEnvironmentFromUrl(apiViewUrl);
            _logger.LogInformation("Detected APIView environment: {Environment} from URL: {Url}", environment, apiViewUrl);

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

        (string revisionId, string reviewId) = ApiViewUrlParser.ExtractIds(apiViewUrl!);
        string environment = IAPIViewHttpService.DetectEnvironmentFromUrl(apiViewUrl!);
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
}
