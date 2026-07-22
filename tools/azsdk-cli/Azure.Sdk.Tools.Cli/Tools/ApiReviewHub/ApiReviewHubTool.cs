using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.ApiReviewHub;

[McpServerToolType]
[Description("API Review Hub operations including review pull request creation")]
public class ApiReviewHubTool(
    IApiReviewHubService apiReviewHubService,
    IApiReviewReleaseStatusService apiReviewReleaseStatusService,
    ILogger<ApiReviewHubTool> logger) : MCPMultiCommandTool
{
    private const string CreateCommandName = "create";
    private const string GetReleaseStatusCommandName = "get-release-status";
    private const string RequestReviewPullRequestToolName = "azsdk_apireviewhub_request_review_pr";
    private const string GetReleaseStatusToolName = "azsdk_apireview_get_release_status";
    private const string DefaultEndpoint = "https://api-review-hub-staging.azurewebsites.net";
    private const string DefaultTargetOwner = "tjprescott";
    private const string DefaultApiViewReleaseStatusEndpoint = "https://apiview.dev/AutoReview/GetReviewStatus";

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.ApiReviewHub];

    private readonly Option<string> endpointOption = new("--endpoint")
    {
        Description = "The API Review Hub endpoint.",
        DefaultValueFactory = _ => DefaultEndpoint
    };

    private readonly Option<string> languageOption = new("--language")
    {
        Description = "The SDK language.",
        Required = true
    };

    private readonly Option<string> packageNameOption = new("--package-name")
    {
        Description = "The package name.",
        Required = true
    };

    private readonly Option<string> packageVersionOption = new("--package-version")
    {
        Description = "The package version to check.",
        Required = true
    };

    private readonly Option<string> apiHashOption = new("--api-hash")
    {
        Description = "The API Review Hub API hash to check. When omitted, the release gate cannot be approved but current approval status is returned."
    };

    private readonly Option<string> baseTagOption = new("--base-tag")
    {
        Description = "The release tag or ref used as the base API surface.",
        Required = true
    };

    private readonly Option<string> targetOwnerOption = new("--target-owner")
    {
        Description = "The GitHub owner for the target working branch.",
        DefaultValueFactory = _ => DefaultTargetOwner
    };

    private readonly Option<string> targetRepoOption = new("--target-repo")
    {
        Description = "The GitHub repository for the target working branch. Defaults to azure-sdk-for-{language}."
    };

    private readonly Option<string> targetBranchOption = new("--target-branch")
    {
        Description = "The target working branch name.",
        Required = true
    };

    private readonly Option<bool> noWaitOption = new("--no-wait")
    {
        Description = "Return after API Review Hub accepts the request instead of polling operation completion.",
        DefaultValueFactory = _ => false
    };

    private readonly Option<int> pollIntervalSecondsOption = new("--poll-interval-seconds")
    {
        Description = "Seconds to wait between API Review Hub operation status polls.",
        DefaultValueFactory = _ => 10
    };

    protected override List<Command> GetCommands() =>
    [
        new McpCommand(CreateCommandName, "Request creation of an API Review Hub review pull request", RequestReviewPullRequestToolName)
        {
            endpointOption,
            languageOption,
            packageNameOption,
            baseTagOption,
            targetOwnerOption,
            targetRepoOption,
            targetBranchOption,
            noWaitOption,
            pollIntervalSecondsOption
        },
        new McpCommand(GetReleaseStatusCommandName, "Check API review release status using APIView and API Review Hub", GetReleaseStatusToolName)
        {
            languageOption,
            packageNameOption,
            packageVersionOption,
            apiHashOption
        }
    ];

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        return parseResult.CommandResult.Command.Name switch
        {
            CreateCommandName => await HandleCreateCommand(parseResult, ct),
            GetReleaseStatusCommandName => await HandleGetReleaseStatusCommand(parseResult, ct),
            _ => new DefaultCommandResponse { ResponseError = $"Unknown command: {parseResult.CommandResult.Command.Name}" }
        };
    }

    private async Task<CommandResponse> HandleCreateCommand(ParseResult parseResult, CancellationToken ct)
    {
        return await RequestReviewPullRequest(
            parseResult.GetValue(endpointOption) ?? DefaultEndpoint,
            parseResult.GetValue(languageOption) ?? string.Empty,
            parseResult.GetValue(packageNameOption) ?? string.Empty,
            parseResult.GetValue(baseTagOption) ?? string.Empty,
            parseResult.GetValue(targetOwnerOption) ?? string.Empty,
            ResolveTargetRepo(parseResult.GetValue(languageOption), parseResult.GetValue(targetRepoOption)),
            parseResult.GetValue(targetBranchOption) ?? string.Empty,
            !parseResult.GetValue(noWaitOption),
            parseResult.GetValue(pollIntervalSecondsOption),
            ct);
    }

    private async Task<CommandResponse> HandleGetReleaseStatusCommand(ParseResult parseResult, CancellationToken ct)
    {
        return await GetReleaseStatus(
            parseResult.GetValue(languageOption) ?? string.Empty,
            parseResult.GetValue(packageNameOption) ?? string.Empty,
            parseResult.GetValue(packageVersionOption) ?? string.Empty,
            parseResult.GetValue(apiHashOption) ?? string.Empty,
            ct);
    }

    [McpServerTool(Name = RequestReviewPullRequestToolName), Description("Request API Review Hub creation of a review pull request for a package API change.")]
    public async Task<ApiReviewHubResponse> RequestReviewPullRequest(
        [Description("The API Review Hub endpoint.")] string endpoint,
        [Description("The SDK language for the review PR request.")] string language,
        [Description("The package name to review.")] string packageName,
        [Description("The release tag or ref used as the base API surface.")] string baseTag,
        [Description("The GitHub owner for the target working branch.")] string targetOwner,
        [Description("The GitHub repository for the target working branch.")] string targetRepo,
        [Description("The target working branch name.")] string targetBranch,
        [Description("Poll API Review Hub until the operation completes.")] bool waitForCompletion = true,
        [Description("Seconds to wait between API Review Hub operation status polls.")] int pollIntervalSeconds = 10,
        CancellationToken ct = default)
    {
        try
        {
            var request = new ReviewPullRequestCreationRequest
            {
                Language = language,
                PackageName = packageName,
                BaseTag = baseTag,
                TargetBranch = new GitBranchReference
                {
                    Owner = targetOwner,
                    Repo = targetRepo,
                    Name = targetBranch
                }
            };

            var result = await apiReviewHubService.RequestReviewPullRequestAsync(
                request,
                endpoint,
                waitForCompletion,
                TimeSpan.FromSeconds(Math.Max(1, pollIntervalSeconds)),
                ct);

            return new ApiReviewHubResponse
            {
                Message = waitForCompletion
                    ? $"API Review Hub operation {result.OperationId} completed with status {result.Status}."
                    : $"API Review Hub accepted operation {result.OperationId}.",
                Result = result
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to request API Review Hub review PR for {packageName}", packageName);
            return new ApiReviewHubResponse
            {
                ResponseError = $"Failed to request API Review Hub review PR for {packageName}: {ex.Message}"
            };
        }
    }

    private static string ResolveTargetRepo(string? language, string? targetRepo)
    {
        if (!string.IsNullOrWhiteSpace(targetRepo))
        {
            return targetRepo;
        }

        return $"azure-sdk-for-{language}";
    }

    [McpServerTool(Name = GetReleaseStatusToolName), Description("Check API review release status using APIView and API Review Hub.")]
    public async Task<ApiReviewReleaseStatusResponse> GetReleaseStatus(
        [Description("The SDK language.")] string language,
        [Description("The package name.")] string packageName,
        [Description("The package version to check.")] string packageVersion,
        [Description("The API Review Hub API hash to check. When omitted, the release gate cannot be approved but current approval status is returned.")] string apiHash = "",
        CancellationToken ct = default)
    {
        try
        {
            var result = await apiReviewReleaseStatusService.GetReleaseStatusAsync(DefaultEndpoint, language, packageName, packageVersion, apiHash, ct);
            var response = new ApiReviewReleaseStatusResponse
            {
                Result = result,
                Details = BuildDetails(result, packageName, packageVersion)
            };

            if (!result.IsApproved)
            {
                response.ResponseError = $"API review release gate is not approved for {packageName} {packageVersion}.";
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get API review release status for {packageName}", packageName);
            return new ApiReviewReleaseStatusResponse
            {
                ResponseError = $"Failed to get API review release status for {packageName}: {ex.Message}"
            };
        }
    }

    private static List<string> BuildDetails(ApiReviewReleaseStatusResult result, string packageName, string packageVersion)
    {
        var details = new List<string>
        {
            "== Final Result ==",
            $"Primary Source: {result.FinalSource}",
            $"Approved: {result.IsApproved}",
            $"Reason: {result.Reason}"
        };

        details.Add(string.Empty);
        details.Add("== API Review Hub (Primary) ==");
        if (result.ReviewHub.Succeeded && result.ReviewHub.Result != null)
        {
            details.Add($"Allowed: {result.ReviewHub.Result.Allowed}");
            details.Add($"Reason: {result.ReviewHub.Result.Reason ?? "none"}");
            if (!string.IsNullOrWhiteSpace(result.ReviewHub.Result.Details))
            {
                details.Add(result.ReviewHub.Result.Details);
            }
        }
        else
        {
            details.Add($"WARNING: Primary query failed for {packageName} {packageVersion}.");
            if (!string.IsNullOrWhiteSpace(result.ReviewHub.Error))
            {
                details.Add(result.ReviewHub.Error);
            }
        }

        if (result.ApiView != null)
        {
            details.Add(string.Empty);
            details.Add("== APIView (Fallback) ==");
            if (result.ApiView.Succeeded && result.ApiView.Result != null)
            {
                details.Add("Used because the primary API Review Hub query failed.");
                details.Add($"Approved: {result.ApiView.Result.IsApproved}");
                details.Add($"Package Name Approved: {result.ApiView.Result.PackageNameApproved}");
                details.Add($"Reason: {result.ApiView.Result.Reason}");
                details.AddRange(result.ApiView.Result.Details);
            }
            else
            {
                details.Add($"WARNING: Fallback query failed for {packageName} {packageVersion}.");
                if (!string.IsNullOrWhiteSpace(result.ApiView.Error))
                {
                    details.Add(result.ApiView.Error);
                }
            }
        }

        return details;
    }
}