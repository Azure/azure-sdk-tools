using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

[McpServerToolType, Description("Read-only service API-version evidence for SDK pull requests.")]
public class ServiceApiReadinessTool(IServiceApiReadinessService readiness, ILogger<ServiceApiReadinessTool> logger) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

    private readonly Option<string> _pullRequestUrl = new("--pull-request-url") { Required = true, Description = "GitHub SDK pull request URL." };

    protected override Command GetCommand() => new("check-api-version", "Check whether Azure lists the API version used by an SDK pull request.")
    {
        _pullRequestUrl
    };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        return await CheckServiceApiVersion(parseResult.GetValue(_pullRequestUrl)!, ct);
    }

    [McpServerTool(Name = "azsdk_check_service_api_version", ReadOnly = true)]
    [Description("Check whether ARM metadata lists the exact API version recorded in an SDK pull request. Returns Passed, Not detected, Inconclusive, or Error with source evidence. Does not merge, release, comment, or change labels; callers must verify the returned head before publishing results.")]
    public async Task<ServiceApiReadinessResponse> CheckServiceApiVersion(
        [Description("GitHub SDK pull request URL, for example https://github.com/Azure/azure-sdk-for-java/pull/123.")] string pullRequestUrl,
        CancellationToken ct = default)
    {
        try
        {
            var (owner, repository, pullRequest) = GitHubService.ParsePullRequestUrl(pullRequestUrl);
            logger.LogInformation("Checking service API-version evidence for {Owner}/{Repository} PR {PullRequest}", owner, repository, pullRequest);
            return await readiness.CheckAsync(owner, repository, pullRequest, ct);
        }
        catch (Exception exception)
        {
            var message = exception is ArgumentException
                ? "Provide a GitHub SDK pull request URL, for example https://github.com/Azure/azure-sdk-for-java/pull/123."
                : "The readiness check could not complete. Check authentication and network access.";
            return new ServiceApiReadinessResponse
            {
                ReadinessStatus = ServiceApiReadinessResponse.Error,
                Detail = message,
                ResponseError = message
            };
        }
    }
}
