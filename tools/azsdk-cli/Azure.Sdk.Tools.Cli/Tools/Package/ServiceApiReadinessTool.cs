using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

[McpServerToolType, Description("Read-only service API-version readiness checks for generated SDK pull requests.")]
public class ServiceApiReadinessTool(IServiceApiReadinessService readiness, ILogger<ServiceApiReadinessTool> logger) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

    private readonly Option<string> _owner = new("--owner") { Required = true, Description = "SDK repository owner." };
    private readonly Option<string> _repository = new("--repo") { Required = true, Description = "SDK repository name, such as azure-sdk-for-java." };
    private readonly Option<int> _pullRequest = new("--pr") { Required = true, Description = "SDK pull request number." };
    private readonly Option<string> _expectedHead = new("--expected-head-sha") { Description = "Expected full SDK PR head SHA; changed heads are Inconclusive." };

    protected override Command GetCommand() => new("check-api-version", "Check the SDK PR's exact API version against ARM provider metadata without writing GitHub or Azure state.")
    {
        _owner, _repository, _pullRequest, _expectedHead
    };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        return await CheckServiceApiVersion(parseResult.GetValue(_owner)!, parseResult.GetValue(_repository)!,
            parseResult.GetValue(_pullRequest), parseResult.GetValue(_expectedHead), ct);
    }

    [McpServerTool(Name = "azsdk_check_service_api_version", ReadOnly = true), Description("Check whether ARM provider metadata advertises the exact API version recorded in an SDK PR's files. Returns Passed, Not detected, Inconclusive, or Error with evidence. Never merges, releases, comments, or changes labels.")]
    public async Task<ServiceApiReadinessResponse> CheckServiceApiVersion(string owner, string repository, int pullRequest, string? expectedHeadSha = null, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Checking service API-version evidence for {Owner}/{Repository} PR {PullRequest}", owner, repository, pullRequest);
            return await readiness.CheckAsync(owner, repository, pullRequest, expectedHeadSha, ct);
        }
        catch (Exception)
        {
            return new ServiceApiReadinessResponse
            {
                HeadSha = expectedHeadSha ?? "",
                ReadinessStatus = ServiceApiReadinessResponse.Error,
                Detail = "The readiness command could not complete. Check authentication and network access.",
                ResponseError = "The readiness command could not complete. Check authentication and network access."
            };
        }
    }
}
