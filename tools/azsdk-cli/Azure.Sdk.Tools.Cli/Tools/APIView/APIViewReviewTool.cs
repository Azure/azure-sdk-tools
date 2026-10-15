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
    private const string RequestCopilotReviewCmd = "request-copilot-review";
    private const string GetCopilotReviewCmd = "get-copilot-review";
    private const string GetReviewUrlCmd = "get-review-url";

    private const string ApiViewGetCommentsToolName = "azsdk_apiview_get_comments";
    private const string ApiViewRequestCopilotReviewToolName = "azsdk_apiview_request_copilot_review";
    private const string ApiViewGetCopilotReviewToolName = "azsdk_apiview_get_copilot_review";
    private const string ApiViewGetReviewUrlToolName = "azsdk_apiview_get_review_url";

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.APIView];

    private readonly IAPIViewService _apiViewService;
    private readonly ILogger<APIViewReviewTool> _logger;

    private readonly Option<string> outputFileOption = new("--output-file"){Description = "Output file path to save the content"};
    private readonly Option<string> contentReturnTypeOption = new("--content-return-type")
    {
        Description = "The APIView content type (text or codefile). Defaults to 'text'.",
        DefaultValueFactory = _ => "text"
    };

    private readonly Option<string> apiViewUrlRequiredOption = new("--url")
    {
        Description = "The URL to the API review in APIView (e.g., https://apiview.dev/review/{reviewId}?activeApiRevisionId={revisionId})",
        Required = true
    };

    private readonly Option<string> apiViewUrlOption = new("--url")
    {
        Description = "The URL to the API review in APIView (e.g., https://apiview.dev/review/{reviewId}?activeApiRevisionId={revisionId}). Use --api-text instead to provide the text directly."
    };

    private readonly Option<string> packageNameOption = new("--package-name")
    {
        Description = "The package name (e.g., 'azure-core')",
        Required = true
    };

    private readonly Option<string> packageVersionOption = new("--package-version")
    {
        Description = "The package version for the API review"
    };

    // get-review-url specific options
    private readonly Option<string> languageQueryOption = new("--language")
    {
        Description = $"The language of the package. Supported values: {string.Join(", ", SupportedLanguages)}.",
        Required = true
    };

    private readonly Option<string> languageOption = new("--language")
    {
        Description = "The language identifier (e.g., 'python', 'java', 'js', 'net', 'go')"
    };

    private readonly Option<string> apiTextOption = new("--api-text")
    {
        Description = "The API surface text to review. Accepts raw text or a markdown code block — if a language-tagged fence is used (e.g. ```python ... ```), the language is inferred automatically."
    };

    private readonly Option<string> baseApiTextOption = new("--base-api-text")
    {
        Description = "Previous version of the API surface text. When provided, the Copilot focuses its feedback on what changed between this and --api-text."
    };

    private readonly Option<string> outlineOption = new("--outline")
    {
        Description = "A brief description of the API's purpose and design intent. Helps the Copilot understand context that may not be evident from the surface text alone."
    };

    private readonly Option<string> existingCommentsOption = new("--existing-comments")
    {
        Description = "Existing review comments as a JSON array (use get-comments to retrieve them). Gives the Copilot context about feedback already left on this API."
    };

    private readonly Option<string> jobIdOption = new("--job-id")
    {
        Description = "Job ID returned by request-copilot-review. Use this to check review status and retrieve generated comments.",
        Required = true
    };

    public APIViewReviewTool(ILogger<APIViewReviewTool> logger, IAPIViewService apiViewService)
    {
        _logger = logger;
        _apiViewService = apiViewService;
    }

    protected override List<Command> GetCommands() =>
    [
        new McpCommand(GetCommentsCmd, "Get comments for a specific APIView URL", ApiViewGetCommentsToolName) { apiViewUrlRequiredOption },
        new(GetContentCmd, "Get content by APIView URL")
        {
            apiViewUrlRequiredOption, outputFileOption, contentReturnTypeOption
        },
        new McpCommand(GetReviewUrlCmd, "Get the APIView review URL for a package and language", ApiViewGetReviewUrlToolName)
        {
            packageNameOption, languageQueryOption, packageVersionOption
        },
        new McpCommand(RequestCopilotReviewCmd, "Submit an API for automated Copilot review", ApiViewRequestCopilotReviewToolName)
        {
            apiViewUrlOption, languageOption, apiTextOption, baseApiTextOption, outlineOption, existingCommentsOption
        },
        new McpCommand(GetCopilotReviewCmd, "Get the status and results of a Copilot review", ApiViewGetCopilotReviewToolName)
        {
            jobIdOption
        }
    ];

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        string commandName = parseResult.CommandResult.Command.Name;
        APIViewResponse result = commandName switch
        {
            GetCommentsCmd => await GetComments(parseResult, ct),
            GetContentCmd => await GetContent(parseResult, ct),
            GetReviewUrlCmd => await GetReviewUrl(parseResult, ct),
            RequestCopilotReviewCmd => await RequestCopilotReview(parseResult, ct),
            GetCopilotReviewCmd => await GetCopilotReview(parseResult, ct),
            _ => new APIViewResponse { ResponseError = $"Unknown command: {commandName}" }
        };

        return result;
    }

    [McpServerTool(Name = ApiViewGetCommentsToolName), Description("Get API review comments and feedback from APIView for a package. Retrieves all reviewer comments left on the API review.")]
    public async Task<APIViewResponse> GetComments(string apiViewUrl, CancellationToken ct = default)
    {
        try
        {
            (string revisionId, _) = ExtractIdsFromUrl(apiViewUrl);

            string? result = await _apiViewService.GetCommentsByRevisionAsync(revisionId, ct);
            if (result == null)
            {
                return new APIViewResponse { ResponseError = $"Failed to retrieve comments for API View: {apiViewUrl}" };
            }

            return new APIViewResponse
            {
                Result = result
            };
        }
        catch (Exception ex)
        {
            return new APIViewResponse { ResponseError = $"Failed to get comments: {ex.Message}" };
        }
    }

    private async Task<APIViewResponse> GetComments(ParseResult parseResult, CancellationToken ct)
    {
        string? apiViewUrl = parseResult.GetValue(apiViewUrlRequiredOption);
        return await GetComments(apiViewUrl!, ct);
    }

    [McpServerTool(Name = ApiViewGetReviewUrlToolName), Description("Get the APIView review URL for a package by name and language. Returns the direct link to the API review page for the specified package.")]
    public async Task<APIViewResponse> GetReviewUrlByPackage(string package, string language, string? version = null, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(package) || string.IsNullOrEmpty(language))
            {
                return new APIViewResponse { ResponseError = "Both 'package' and 'language' parameters are required." };
            }

            string? resolvedLanguage = ResolveLanguage(language);
            if (resolvedLanguage == null)
            {
                var supported = string.Join(", ", SupportedLanguages);
                return new APIViewResponse
                {
                    ResponseError = $"Unsupported language '{language}'. Supported languages are: {supported}."
                };
            }

            string? url = await _apiViewService.GetReviewUrlByPackageAsync(package, resolvedLanguage, version, ct);

            if (url == null)
            {
                return new APIViewResponse
                {
                    ResponseError = $"Could not find an APIView review for package '{package}' in language '{resolvedLanguage}'" +
                                   (!string.IsNullOrEmpty(version) ? $" with version '{version}'" : "") + "." +
                                   " Please verify the package name and language are correct."
                };
            }

            return new APIViewResponse { Result = url };
        }
        catch (Exception ex)
        {
            string context = $"package '{package}' ({language})" +
                             (!string.IsNullOrEmpty(version) ? $" version '{version}'" : "");
            return new APIViewResponse { ResponseError = $"Failed to get review URL for {context}: {ex.Message}" };
        }
    }

    private async Task<APIViewResponse> GetReviewUrl(ParseResult parseResult, CancellationToken ct)
    {
        string? package = parseResult.GetValue(packageNameOption);
        string? language = parseResult.GetValue(languageQueryOption);
        string? version = parseResult.GetValue(packageVersionOption);
        return await GetReviewUrlByPackage(package!, language!, version, ct);
    }

    private async Task<APIViewResponse> GetContent(ParseResult parseResult, CancellationToken ct)
    {
        string? apiViewUrl = parseResult.GetValue(apiViewUrlRequiredOption);
        string? outputFile = parseResult.GetValue(outputFileOption);
        string? contentType = parseResult.GetValue(contentReturnTypeOption);

        if (!Enum.TryParse<ContentType>(contentType, ignoreCase: true, out _))
        {
            var validValues = string.Join(", ", Enum.GetNames<ContentType>());
            return new APIViewResponse { ResponseError = $"Invalid content type '{contentType}'. Must be one of: {validValues}." };
        }

        (string revisionId, string reviewId) = ExtractIdsFromUrl(apiViewUrl);
        try
        {
            string? result = await _apiViewService.GetRevisionContent(revisionId, reviewId, contentType, ct);
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

    [McpServerTool(Name = ApiViewRequestCopilotReviewToolName), Description("Submit an API surface text for automated Copilot review. Provide the text directly via 'api-text' (raw or markdown-fenced), or supply an APIView URL to have the text fetched automatically. Returns a job ID — use get-copilot-review to poll for results and comments.")]
    public async Task<APIViewResponse> RequestCopilotReview(string? apiViewUrl = null, string? language = null, string? apiText = null, string? baseApiText = null, string? outline = null, string? existingComments = null, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(apiViewUrl) && string.IsNullOrEmpty(apiText))
            {
                return new APIViewResponse { ResponseError = "Either --url or --api-text is required to submit a Copilot review." };
            }

            string? reviewTarget;
            if (!string.IsNullOrEmpty(apiText))
            {
                reviewTarget = apiText;
            }
            else
            {
                (string revisionId, string reviewId) = ExtractIdsFromUrl(apiViewUrl);
                reviewTarget = await _apiViewService.GetRevisionContent(revisionId, reviewId, "text", ct);
                if (string.IsNullOrEmpty(reviewTarget))
                {
                    return new APIViewResponse { ResponseError = $"Failed to fetch content from APIView URL: {apiViewUrl}" };
                }
            }

            (string? content, int statusCode) = await _apiViewService.StartCopilotReviewAsync(reviewTarget, language, baseApiText, outline, existingComments, ct);

            if (string.IsNullOrEmpty(content))
            {
                return new APIViewResponse { ResponseError = $"Failed to start Copilot review job. No content returned (status: {statusCode})." };
            }

            return new APIViewResponse
            {
                Message = "Copilot review started. Use the job ID from the result with get-copilot-review to check status and retrieve results.",
                Result = content
            };
        }
        catch (Exception ex)
        {
            return new APIViewResponse { ResponseError = $"Failed to submit Copilot review: {ex.Message}" };
        }
    }

    private async Task<APIViewResponse> RequestCopilotReview(ParseResult parseResult, CancellationToken ct)
    {
        string? apiViewUrl = parseResult.GetValue(apiViewUrlOption);
        string? language = parseResult.GetValue(languageOption);
        string? apiText = parseResult.GetValue(apiTextOption);
        string? baseApiText = parseResult.GetValue(baseApiTextOption);
        string? outline = parseResult.GetValue(outlineOption);
        string? existingComments = parseResult.GetValue(existingCommentsOption);
        return await RequestCopilotReview(apiViewUrl, language, apiText, baseApiText, outline, existingComments, ct);
    }

    [McpServerTool(Name = ApiViewGetCopilotReviewToolName), Description("Get the status and results of a Copilot review job. When complete, the response includes all generated review comments.")]
    public async Task<APIViewResponse> GetCopilotReview(string jobId, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(jobId))
            {
                return new APIViewResponse { ResponseError = "Job ID is required." };
            }

            (string? content, int statusCode) = await _apiViewService.GetCopilotReviewAsync(jobId, ct);

            if (content == null)
            {
                return new APIViewResponse { ResponseError = $"Failed to retrieve Copilot review results for job ID: {jobId}" };
            }

            return new APIViewResponse
            {
                Result = content
            };
        }
        catch (Exception ex)
        {
            return new APIViewResponse { ResponseError = $"Failed to get Copilot review results: {ex.Message}" };
        }
    }

    private async Task<APIViewResponse> GetCopilotReview(ParseResult parseResult, CancellationToken ct)
    {
        string? jobId = parseResult.GetValue(jobIdOption);
        return await GetCopilotReview(jobId!, ct);
    }

    public static (string revisionId, string reviewId) ExtractIdsFromUrl(string url)
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
            throw new ArgumentException($"Error parsing URL: {ex.Message}", nameof(url), ex);
        }
    }

    // Supported APIView languages and their common aliases
    private static readonly string[] SupportedLanguages =
        ["C", "C#", "C++", "Go", "Java", "JavaScript", "Json", "Kotlin", "Python", "Rust", "Swift", "TypeSpec", "Xml"];

    private static readonly Dictionary<string, string> LanguageAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["net"] = "C#",
        [".net"] = "C#",
        ["dotnet"] = "C#",
        ["csharp"] = "C#",
        ["cs"] = "C#",
        ["cpp"] = "C++",
        ["js"] = "JavaScript",
        ["typescript"] = "JavaScript",
        ["ts"] = "JavaScript",
        ["golang"] = "Go",
        ["py"] = "Python",
    };

    /// <summary>Resolves a language input to a canonical APIView language name, or null if unsupported.</summary>
    public static string? ResolveLanguage(string language)
    {
        string? canonical = SupportedLanguages.FirstOrDefault(l => l.Equals(language, StringComparison.OrdinalIgnoreCase));
        return canonical ?? (LanguageAliases.GetValueOrDefault(language));
    }

}
