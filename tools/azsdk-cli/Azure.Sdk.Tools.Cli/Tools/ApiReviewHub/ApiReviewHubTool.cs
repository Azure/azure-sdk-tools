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
    ILogger<ApiReviewHubTool> logger) : MCPMultiCommandTool
{
    internal static readonly IReadOnlyDictionary<string, string> DefaultTargetRepos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
    private const string RequestReviewPullRequestToolName = "azsdk_apireviewhub_request_review_pr";
    private const string DefaultEndpoint = "https://api-review-hub.azurewebsites.net";
    private const string DefaultTargetOwner = "Azure";

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.ApiReviewHub];

    private readonly Option<string> languageOption = CreateLanguageOption();

    private readonly Option<string> packageNameOption = new("--package-name")
    {
        Description = "The package name.",
        Required = true
    };

    private readonly Option<string> baseTagOption = new("--base-tag")
    {
        Description = "The optional release tag or ref used as the base API surface."
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
        }
    ];

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        return parseResult.CommandResult.Command.Name switch
        {
            CreateCommandName => await HandleCreateCommand(parseResult, ct),
            _ => new DefaultCommandResponse { ResponseError = $"Unknown command: {parseResult.CommandResult.Command.Name}" }
        };
    }

    private async Task<CommandResponse> HandleCreateCommand(ParseResult parseResult, CancellationToken ct)
    {
        return await RequestReviewPullRequest(
            parseResult.GetValue(languageOption) ?? string.Empty,
            parseResult.GetValue(packageNameOption) ?? string.Empty,
            parseResult.GetValue(targetOwnerOption) ?? string.Empty,
            ResolveTargetRepo(parseResult.GetValue(languageOption), parseResult.GetValue(targetRepoOption)),
            parseResult.GetValue(targetBranchOption) ?? string.Empty,
            parseResult.GetValue(baseTagOption),
            !parseResult.GetValue(noWaitOption),
            parseResult.GetValue(pollIntervalSecondsOption),
            ct);
    }

    [McpServerTool(Name = RequestReviewPullRequestToolName), Description("Request API Review Hub creation of a review pull request for a package API change.")]
    public async Task<ApiReviewHubResponse> RequestReviewPullRequest(
        [Description("The SDK language for the review PR request.")] string language,
        [Description("The package name to review.")] string packageName,
        [Description("The GitHub owner for the target working branch.")] string targetOwner,
        [Description("The GitHub repository for the target working branch. By default, the command selects the appropriate repo based on the language.")] string targetRepo,
        [Description("The target working branch name.")] string targetBranch,
        [Description("The optional release tag or ref used as the base API surface.")] string? baseTag = null,
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
                BaseTag = baseTag ?? string.Empty,
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

}