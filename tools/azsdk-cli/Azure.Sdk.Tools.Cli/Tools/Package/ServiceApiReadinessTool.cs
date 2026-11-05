using System.CommandLine;
using System.CommandLine.Parsing;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

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

    public async Task<ServiceApiReadinessResponse> CheckServiceApiVersion(string pullRequestUrl, CancellationToken ct = default)
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
                : "The readiness command could not complete. Check authentication and network access.";
            return new ServiceApiReadinessResponse
            {
                ReadinessStatus = ServiceApiReadinessResponse.Error,
                Detail = message,
                ResponseError = message
            };
        }
    }
}
