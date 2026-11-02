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

    private readonly Option<string> _owner = new("--owner") { Required = true, Description = "SDK repository owner." };
    private readonly Option<string> _repository = new("--repo") { Required = true, Description = "SDK repository name, such as azure-sdk-for-java." };
    private readonly Option<int> _pullRequest = new("--pr") { Required = true, Description = "SDK pull request number." };

    protected override Command GetCommand() => new("check-api-version", "Check the SDK PR's exact API version against ARM provider metadata without writing GitHub or Azure state.")
    {
        _owner, _repository, _pullRequest
    };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        return await CheckServiceApiVersion(parseResult.GetValue(_owner)!, parseResult.GetValue(_repository)!,
            parseResult.GetValue(_pullRequest), ct);
    }

    public async Task<ServiceApiReadinessResponse> CheckServiceApiVersion(string owner, string repository, int pullRequest, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Checking service API-version evidence for {Owner}/{Repository} PR {PullRequest}", owner, repository, pullRequest);
            return await readiness.CheckAsync(owner, repository, pullRequest, ct);
        }
        catch (Exception)
        {
            return new ServiceApiReadinessResponse
            {
                ReadinessStatus = ServiceApiReadinessResponse.Error,
                Detail = "The readiness command could not complete. Check authentication and network access.",
                ResponseError = "The readiness command could not complete. Check authentication and network access."
            };
        }
    }
}
