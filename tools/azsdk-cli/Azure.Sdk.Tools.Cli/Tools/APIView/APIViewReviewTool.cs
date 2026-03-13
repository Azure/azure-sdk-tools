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
    private const string CreateCIRevisionCmd = "create-ci-revision";
    private const string CreatePullRequestRevisionCmd = "create-pull-request-revision";

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

    private readonly Option<string> buildIdOption = new("--build-id")
    {
        Description = "The Azure DevOps build ID",
        Required = true
    };

    private readonly Option<string> codeFileOption = new("--code-token-file-path")
    {
        Description = "The APIView token file path within the artifact (e.g., 'azure-core_python.json')",
        Required = true,
    };

    private readonly Option<string> sourceFileOption = new("--source-file-path")
    {
        Description = "The original source/package file path relative to the artifact (e.g., 'azure-core-1.0.0.whl', 'Microsoft.Cache-Redis.New.json')",
        Required = true
    };

    private readonly Option<string> repoNameOption = new("--repo-name")
    {
        Description = "The repository name in 'owner/repo' format (e.g., 'Azure/azure-sdk-for-python')",
        Required = true
    };

    private readonly Option<string> packageNameOption = new("--package-name")
    {
        Description = "The package name (e.g., 'azure-core')",
        Required = true
    };

    private readonly Option<string> artifactNameOption = new("--artifact-name")
    {
        Description = "The Azure DevOps artifact name",
        Required = true
    };

    private readonly Option<string> projectOption = new("--project")
    {
        Description = "The Azure DevOps project name (defaults to 'internal')",
        DefaultValueFactory = _ => "internal"
    };

    private readonly Option<string> labelOption = new("--label")
    {
        Description = "A label to associate with the API review. If not provided, defaults to 'Source Branch:{sourceBranch}'"
    };

    private readonly Option<string> packageVersionOption = new("--package-version")
    {
        Description = "The package version for the API review"
    };

    private readonly Option<bool> compareAllRevisionsOption = new("--compare-all-revisions")
    {
        Description = "Whether to compare all revisions (typically set for released packages)",
        DefaultValueFactory = _ => false
    };

    private readonly Option<bool> setReleaseTagOption = new("--set-release-tag")
    {
        Description = "Whether to tag the revision as released (used from release pipelines)",
        DefaultValueFactory = _ => false
    };

    private readonly Option<string> packageTypeOption = new("--package-type")
    {
        Description = "The SDK package type"
    };

    private readonly Option<string> sourceBranchOption = new("--source-branch")
    {
        Description = "The source branch for the build"
    };

    // create-pull-request-revision specific options
    private readonly Option<string> commitShaOption = new("--commit-sha")
    {
        Description = "The git commit SHA of the pull request head",
        Required = true
    };

    private readonly Option<int> pullRequestNumberOption = new("--pull-request-number")
    {
        Description = "The pull request number",
        Required = true
    };

    private readonly Option<string> languageOption = new("--language")
    {
        Description = "The language identifier (e.g., 'python', 'java', 'js', 'net', 'go')"
    };

    private readonly Option<string> baselineCodeFileOption = new("--baseline-code-file")
    {
        Description = "The baseline code file name for API comparison"
    };

    private readonly Option<string> metadataFileOption = new("--metadata-file")
    {
        Description = "TypeSpec metadata file name within the artifact (e.g., 'typespec-metadata.json')"
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
        },
        new(CreateCIRevisionCmd, "Create an API revision from Azure DevOps pipeline artifacts (CI/release pipeline usage)")
        {
            buildIdOption, artifactNameOption, sourceFileOption, codeFileOption,
            repoNameOption, packageNameOption, projectOption,
            labelOption, compareAllRevisionsOption, packageVersionOption, setReleaseTagOption, packageTypeOption, sourceBranchOption
        },
        new(CreatePullRequestRevisionCmd, "Create an API revision if API changes are detected in a pull request (PR pipeline usage)")
        {
            buildIdOption, artifactNameOption, sourceFileOption, commitShaOption,
            repoNameOption, packageNameOption, pullRequestNumberOption,
            projectOption, packageTypeOption, codeFileOption, languageOption, baselineCodeFileOption, metadataFileOption
        }
    ];

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        string commandName = parseResult.CommandResult.Command.Name;
        APIViewResponse result = commandName switch
        {
            GetCommentsCmd => await GetComments(parseResult, ct),
            GetContentCmd => await GetContent(parseResult, ct),
            CreateCIRevisionCmd => await CreateCIRevision(parseResult, ct),
            CreatePullRequestRevisionCmd => await CreatePullRequestRevision(parseResult, ct),
            _ => new APIViewResponse { ResponseError = $"Unknown command: {commandName}" }
        };
        
        return result;
    }

    [McpServerTool(Name = ApiViewGetCommentsToolName), Description("Get API review comments and feedback from APIView for a package. Retrieves all reviewer comments left on the API review.")]
    public async Task<APIViewResponse> GetComments(string apiViewUrl)
    {
        try
        {
            (string revisionId, _) = ExtractIdsFromUrl(apiViewUrl);

            string? result = await _apiViewService.GetCommentsByRevisionAsync(revisionId);
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
        try
        {
            string? result = await _apiViewService.GetRevisionContent(revisionId, reviewId, contentType);
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

    private async Task<APIViewResponse> CreateCIRevision(ParseResult parseResult, CancellationToken ct)
    {
        string? reviewFilePath = parseResult.GetValue(codeFileOption);
        string? buildId = parseResult.GetValue(buildIdOption);
        string? artifactName = parseResult.GetValue(artifactNameOption);
        string? originalFilePath = parseResult.GetValue(sourceFileOption);
        string? label = parseResult.GetValue(labelOption);
        string? repoName = parseResult.GetValue(repoNameOption);
        string? packageName = parseResult.GetValue(packageNameOption);
        string? project = parseResult.GetValue(projectOption);
        bool compareAllRevisions = parseResult.GetValue(compareAllRevisionsOption);
        string? packageVersion = parseResult.GetValue(packageVersionOption);
        bool setReleaseTag = parseResult.GetValue(setReleaseTagOption);
        string? packageType = parseResult.GetValue(packageTypeOption);
        string? sourceBranch = parseResult.GetValue(sourceBranchOption);
        label ??= !string.IsNullOrEmpty(sourceBranch) ? $"Source Branch:{sourceBranch}" : null;

        if (string.IsNullOrEmpty(repoName) || !repoName.Contains('/'))
        {
            return new APIViewResponse { ResponseError = $"Invalid --repo-name '{repoName}'. Must be in 'owner/repo' format (e.g., 'Azure/azure-sdk-for-python')." };
        }

        try
        {
            (string? content, int statusCode) = await _apiViewService.CreateCIReviewAsync(
                buildId!, artifactName!, originalFilePath!, reviewFilePath!,
                repoName!, packageName!, project!,
                label, compareAllRevisions, packageVersion, setReleaseTag, packageType, sourceBranch);

            return statusCode switch
            {
                200 => new APIViewResponse
                {
                    Message = $"API review approved and package name approved for {packageName}",
                    Result = content
                },
                201 => new APIViewResponse
                {
                    Message = $"API review is not yet approved, but package name is approved for {packageName}",
                    Result = content
                },
                202 => new APIViewResponse
                {
                    Message = $"API review created. API review and package name are not yet approved for {packageName}",
                    Result = content
                },
                _ => new APIViewResponse
                {
                    ResponseError = $"Invalid status code from APIView. Status code {statusCode}. Please reach out to Azure SDK engineering systems on Teams channel."
                }
            };
        }
        catch (Exception ex)
        {
            return new APIViewResponse { ResponseError = $"Failed to create API revision: {ex.Message}" };
        }
    }

    private async Task<APIViewResponse> CreatePullRequestRevision(ParseResult parseResult, CancellationToken ct)
    {
        string? buildId = parseResult.GetValue(buildIdOption);
        string? artifactName = parseResult.GetValue(artifactNameOption);
        string? filePath = parseResult.GetValue(sourceFileOption);
        string? commitSha = parseResult.GetValue(commitShaOption);
        string? repoName = parseResult.GetValue(repoNameOption);
        string? packageName = parseResult.GetValue(packageNameOption);
        int pullRequestNumber = parseResult.GetValue(pullRequestNumberOption);
        string? project = parseResult.GetValue(projectOption);
        string? packageType = parseResult.GetValue(packageTypeOption);
        string? language = parseResult.GetValue(languageOption);
        string? codeFile = parseResult.GetValue(codeFileOption);
        string? baselineCodeFile = parseResult.GetValue(baselineCodeFileOption);
        string? metadataFile = parseResult.GetValue(metadataFileOption);

        if (string.IsNullOrEmpty(repoName) || !repoName.Contains('/'))
        {
            return new APIViewResponse { ResponseError = $"Invalid --repo-name '{repoName}'. Must be in 'owner/repo' format (e.g., 'Azure/azure-sdk-for-python')." };
        }

        try
        {
            (string? content, int statusCode) = await _apiViewService.CreatePullRequestRevisionAsync(
                buildId!, artifactName!, filePath!, commitSha!,
                repoName!, packageName!,
                pullRequestNumber, codeFile, baselineCodeFile, language, project, packageType, metadataFile);

            return statusCode switch
            {
                201 => new APIViewResponse
                {
                    Message = $"API changes detected for {packageName}. New API revision created.",
                    Result = content
                },
                208 => new APIViewResponse
                {
                    Message = $"No API changes detected for {packageName}. Existing revision is up to date.",
                    Result = content
                },
                _ => new APIViewResponse
                {
                    ResponseError = $"Invalid status code from APIView. Status code {statusCode}. Please reach out to Azure SDK engineering systems on Teams channel."
                }
            };
        }
        catch (Exception ex)
        {
            return new APIViewResponse { ResponseError = $"Failed to create API revision: {ex.Message}" };
        }
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
}
