using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

[McpServerToolType, Description("Validate service API readiness for SDK pull requests or local packages using read-only ARM evidence.")]
public class ServiceApiReadinessTool(IServiceApiReadinessService readiness, ILogger<ServiceApiReadinessTool> toolLogger,
    IGitHelper gitHelper, IEnumerable<LanguageService> languageServices,
    ReleasePlanReadinessService releasePlanReadiness) : LanguageMcpTool(languageServices, gitHelper, toolLogger)
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

    private readonly Option<string> _pullRequestUrl = new("--pull-request-url") { Description = "GitHub SDK pull request URL; cannot be combined with --package-path." };
    private readonly Option<string> _packagePath = new("--package-path", "-p") { Description = "Local SDK package directory; cannot be combined with --pull-request-url." };
    private readonly Option<bool> _metadataOnly = new("--metadata-only") { Description = "Return current-head package metadata and provider/API-version requirements without querying ARM; requires --pull-request-url." };
    private readonly Option<bool> _recordReleasePlan = new("--record-release-plan") { Description = "Explicitly record fresh service evidence in the release-plan readiness fields; requires PR input and cannot be combined with --metadata-only." };

    protected override Command GetCommand() => new("validate-service-api-readiness", "Validate service API readiness for an SDK pull request or local package using ARM metadata.")
    {
        _pullRequestUrl,
        _packagePath,
        _metadataOnly,
        _recordReleasePlan
    };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var url = parseResult.GetValue(_pullRequestUrl);
        var packagePath = parseResult.GetValue(_packagePath);
        var metadataOnly = parseResult.GetValue(_metadataOnly);
        var recordReleasePlan = parseResult.GetValue(_recordReleasePlan);
        if (!metadataOnly && !recordReleasePlan)
        {
            return await ValidateServiceApiReadiness(url, packagePath, ct);
        }
        var operation = recordReleasePlan ? "Release-plan recording" : "The metadata-only check";
        var access = recordReleasePlan ? "configured" : "GitHub";
        try
        {
            if (url is null || packagePath is not null || metadataOnly && recordReleasePlan)
            {
                throw new ArgumentException("Supply one SDK PR URL and one check mode.");
            }
            var (owner, repository, pullRequest) = GitHubService.ParsePullRequestUrl(url);
            return metadataOnly
                ? await readiness.GetPackageMetadataAsync(owner, repository, pullRequest, ct)
                : await releasePlanReadiness.CheckAsync(owner, repository, pullRequest, true, ct);
        }
        catch (Exception)
        {
            return new ServiceApiReadinessResponse
            {
                ReadinessStatus = ServiceApiReadinessResponse.Error,
                Detail = $"{operation} could not complete. Supply one SDK PR URL and verify {access} access.",
                ResponseError = $"{operation} could not complete."
            };
        }
    }

    [McpServerTool(Name = "azsdk_validate_service_api_readiness", ReadOnly = true)]
    [Description("Validate service API readiness by checking whether ARM metadata lists the exact API versions recorded in SDK metadata. Supply exactly one of pullRequestUrl or packagePath. Returns Passed, Not detected, Inconclusive, or Error with enriched PackageInfo evidence. Does not merge, release, comment, or change labels. Only PR input attests a captured PR head; callers must verify that head before publishing results.")]
    public async Task<ServiceApiReadinessResponse> ValidateServiceApiReadiness(
        [Description("GitHub SDK pull request URL, for example https://github.com/Azure/azure-sdk-for-java/pull/123. Mutually exclusive with packagePath.")] string? pullRequestUrl = null,
        [Description("Local SDK package directory, inspected from the working tree. Mutually exclusive with pullRequestUrl.")] string? packagePath = null,
        CancellationToken ct = default)
    {
        try
        {
            if ((pullRequestUrl is null) == (packagePath is null))
            {
                throw new ArgumentException("Supply exactly one package source.");
            }
            if (packagePath is not null)
            {
                if (string.IsNullOrWhiteSpace(packagePath) || !Directory.Exists(packagePath))
                {
                    throw new ArgumentException("The local SDK package directory must exist.");
                }
                var fullPath = Path.GetFullPath(packagePath);
                var language = await GetLanguageServiceAsync(fullPath, ct)
                    ?? throw new ArgumentException("The SDK package language could not be resolved.");
                var package = await language.GetPackageInfo(fullPath, ct);
                return await readiness.CheckPackageAsync(package, ct);
            }
            var (owner, repository, pullRequest) = GitHubService.ParsePullRequestUrl(pullRequestUrl!);
            logger.LogInformation("Checking service API-version evidence for {Owner}/{Repository} PR {PullRequest}", owner, repository, pullRequest);
            return await releasePlanReadiness.CheckAsync(owner, repository, pullRequest, false, ct);
        }
        catch (Exception exception)
        {
            var message = exception is ArgumentException
                ? "Provide exactly one GitHub SDK pull request URL or existing local SDK package path."
                : "The readiness check could not complete. Check authentication and network access.";
            return new ServiceApiReadinessResponse
            {
                InputKind = packagePath is null ? "pull-request" : "package",
                ReadinessStatus = ServiceApiReadinessResponse.Error,
                Detail = message,
                ResponseError = message
            };
        }
    }
}
