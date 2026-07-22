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
    private static readonly IReadOnlyDictionary<string, string> DefaultTargetRepos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["python"] = "azure-sdk-for-python",
        ["java"] = "azure-sdk-for-java",
        ["csharp"] = "azure-sdk-for-net",
        ["js"] = "azure-sdk-for-js",
        ["go"] = "azure-sdk-for-go",
        ["cpp"] = "azure-sdk-for-cpp",
        ["swift"] = "azure-sdk-for-ios",
        ["rust"] = "azure-sdk-for-rust"
    };

    private static readonly string[] SupportedLanguages = [.. DefaultTargetRepos.Keys.Order(StringComparer.OrdinalIgnoreCase)];
    private static readonly string SupportedLanguagesDescription = string.Join(", ", SupportedLanguages);

    private const string CreateCommandName = "create";
    private const string GetReleaseStatusCommandName = "get-release-status";
    private const string RequestReviewPullRequestToolName = "azsdk_apireviewhub_request_review_pr";
    private const string GetReleaseStatusToolName = "azsdk_apireview_get_release_status";
    private const string DefaultEndpoint = "https://api-review-hub.azurewebsites.net";
    private const string DefaultTargetOwner = "Azure";
    private const string DefaultApiViewReleaseStatusEndpoint = "https://apiview.dev/AutoReview/GetReviewStatus";

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.ApiReviewHub];

    private readonly Option<string> languageOption = CreateLanguageOption();

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
        Description = "The GitHub repository for the target working branch. By default, the command selects the appropriate repo based on the language."
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
        DefaultValueFactory = _ => 30
    };

    protected override List<Command> GetCommands() =>
    [
        new McpCommand(CreateCommandName, "Request creation of an API Review Hub review pull request", RequestReviewPullRequestToolName)
        {
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
        var response = await GetReleaseStatus(
            parseResult.GetValue(languageOption) ?? string.Empty,
            parseResult.GetValue(packageNameOption) ?? string.Empty,
            parseResult.GetValue(packageVersionOption) ?? string.Empty,
            parseResult.GetValue(apiHashOption) ?? string.Empty,
            ct);

        if (IsJsonOutput(parseResult))
        {
            response.Details = null;
        }

        return response;
    }

    private static bool IsJsonOutput(ParseResult parseResult)
    {
        var outputFormat = parseResult.GetValue(SharedOptions.Format);
        return string.Equals(outputFormat, "json", StringComparison.OrdinalIgnoreCase);
    }

    [McpServerTool(Name = RequestReviewPullRequestToolName), Description("Request API Review Hub creation of a review pull request for a package API change.")]
    public async Task<ApiReviewHubResponse> RequestReviewPullRequest(
        [Description("The SDK language for the review PR request.")] string language,
        [Description("The package name to review.")] string packageName,
        [Description("The release tag or ref used as the base API surface.")] string baseTag,
        [Description("The GitHub owner for the target working branch.")] string targetOwner,
        [Description("The GitHub repository for the target working branch. By default, the command selects the appropriate repo based on the language.")] string targetRepo,
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
                DefaultEndpoint,
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

        if (!string.IsNullOrWhiteSpace(language) && DefaultTargetRepos.TryGetValue(language, out var repo))
        {
            return repo;
        }

        throw new ArgumentException($"Unsupported language '{language}'. Supported values: {string.Join(", ", SupportedLanguages)}.", nameof(language));
    }

    private static Option<string> CreateLanguageOption()
    {
        var option = new Option<string>("--language")
        {
            Description = $"The SDK language. Supported values: {SupportedLanguagesDescription}.",
            Required = true
        };

        option.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!SupportedLanguages.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                result.AddError($"Invalid language '{value}'. Supported values: {SupportedLanguagesDescription}.");
            }
        });

        return option;
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
                Details = BuildDetails(result, packageName, packageVersion, apiHash)
            };

            if (!result.IsApproved)
            {
                response.ResponseError = BuildFailureMessage(result, packageName, packageVersion);
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

    private static List<string> BuildDetails(ApiReviewReleaseStatusResult result, string packageName, string packageVersion, string apiHash)
    {
        var details = new List<string>();

        details.Add("== API Review Hub (Primary) ==");
        if (result.ReviewHub.Succeeded && result.ReviewHub.Result != null)
        {
            details.Add($"Allowed: {result.ReviewHub.Result.Allowed}");
            details.Add($"Reason: {result.ReviewHub.Result.Reason ?? "none"}");
            if (!string.IsNullOrWhiteSpace(result.ReviewHub.Result.Details))
            {
                details.Add(result.ReviewHub.Result.Details);
            }

            AddApprovalDetails(details, result.ReviewHub.Result.Approvals, apiHash);
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
            details.Add("== APIView (Legacy) ==");
            if (result.ApiView.Succeeded && result.ApiView.Result != null)
            {
                details.Add("Queried because the primary API Review Hub result was not approved or could not be retrieved.");
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

        details.Add(string.Empty);
        details.Add("== Final Result ==");
        details.Add($"Approved: {result.IsApproved}");
        if (result.IsApproved)
        {
            details.Add($"Source: {GetSourceLabel(result.FinalSource)}");
        }
        else
        {
            details.Add($"Reason: {result.Reason}");
        }

        return details;
    }

    private static string BuildFailureMessage(ApiReviewReleaseStatusResult result, string packageName, string packageVersion)
    {
        return result.Reason switch
        {
            "rejected" => $"API review release gate is rejected for {packageName} {packageVersion}.",
            "missingApiHash" => $"API review release gate cannot be approved for {packageName} {packageVersion} because no API hash was provided.",
            _ => $"API review release gate is not approved for {packageName} {packageVersion}."
        };
    }

    private static void AddApprovalDetails(List<string> details, IReadOnlyList<ApiReviewHubReleaseGateApproval> approvals, string apiHash)
    {
        if (approvals.Count == 0)
        {
            details.Add("Approval records returned by service: none");
            return;
        }

        if (string.IsNullOrWhiteSpace(apiHash))
        {
            details.Add("No API hash was provided. Approval records returned by the service:");
        }
        else
        {
            details.Add($"Provided API hash: {apiHash}");
            details.Add("Approval records returned by the service:");
        }

        foreach (var approval in approvals.OrderByDescending(approval => ApiHashMatches(approval.ApiHash, apiHash)).ThenByDescending(approval => approval.LastUpdatedOn, StringComparer.Ordinal))
        {
            var matchText = ApiHashMatches(approval.ApiHash, apiHash) ? " [provided hash]" : string.Empty;
            details.Add($"- {approval.Status}: {approval.ApiHash}{matchText}");

            if (!string.IsNullOrWhiteSpace(approval.LastUpdatedBy) || !string.IsNullOrWhiteSpace(approval.LastUpdatedOn))
            {
                details.Add($"  Updated by: {approval.LastUpdatedBy ?? "unknown"}{FormatOnSuffix(approval.LastUpdatedOn)}");
            }

            if (!string.IsNullOrWhiteSpace(approval.PullRequestUrl))
            {
                details.Add($"  Pull request: {approval.PullRequestUrl}");
            }
        }
    }

    private static bool ApiHashMatches(string approvalApiHash, string requestedApiHash)
    {
        return !string.IsNullOrWhiteSpace(requestedApiHash)
            && string.Equals(approvalApiHash, requestedApiHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatOnSuffix(string? lastUpdatedOn)
    {
        return string.IsNullOrWhiteSpace(lastUpdatedOn) ? string.Empty : $" on {lastUpdatedOn}";
    }

    private static string GetSourceLabel(string finalSource)
    {
        return finalSource switch
        {
            "ApiReviewHub" => "API Review Hub (Primary)",
            "APIView" => "APIView (Legacy)",
            _ => finalSource
        };
    }
}