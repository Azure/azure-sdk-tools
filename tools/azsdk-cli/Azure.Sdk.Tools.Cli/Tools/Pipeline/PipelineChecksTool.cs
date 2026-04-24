// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Pipeline;

[Description("Gets pipeline and GitHub Actions check results from a GitHub Pull Request.")]
[McpServerToolType]
public class PipelineChecksTool(
    IPipelineIdentifierHelper pipelineHelper,
    ILogger<PipelineChecksTool> logger
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.AzurePipelines];

    private const string GetPrChecksToolName = "azsdk_get_pr_checks";

    private readonly Argument<string> prLocatorArg = new("pullRequest")
    {
        Description = "GitHub pull request link or number",
    };

    private readonly Option<bool> failedOpt = new("--failed", "-f")
    {
        Description = "Filter to checks with FAILURE conclusion only",
        Required = false,
    };

    private readonly Option<bool> blockingOpt = new("--blocking", "-b")
    {
        Description = "Filter to checks with conclusion != SUCCESS (blocking merge)",
        Required = false,
    };

    protected override Command GetCommand() =>
        new McpCommand("checks", "Get pipeline and CI checks from a GitHub Pull Request", GetPrChecksToolName) { prLocatorArg, failedOpt, blockingOpt };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var prLinkOrNumber = parseResult.GetValue(prLocatorArg);
        var failed = parseResult.GetValue(failedOpt);
        var blocking = parseResult.GetValue(blockingOpt);

        return await GetPrChecks(prLinkOrNumber, failed, blocking, ct);
    }

    [McpServerTool(Name = GetPrChecksToolName), Description("Get pipeline and CI check results from a GitHub Pull Request link or PR number")]
    public async Task<PrChecksResponse> GetPrChecks(
        [Description("GitHub Pull Request link or PR number")] string prLinkOrNumber,
        [Description("Filter to FAILURE only")] bool failed = false,
        [Description("Filter to conclusion != SUCCESS (blocking merge)")] bool blocking = false,
        CancellationToken ct = default)
    {
        try
        {
            var parsed = await pipelineHelper.TryResolveGitHubPrAsync(prLinkOrNumber, ct);
            if (parsed == null)
            {
                return new PrChecksResponse
                {
                    ResponseError = $"Invalid GitHub Pull Request identifier: {prLinkOrNumber}. Expected a PR link (https://github.com/owner/repo/pull/123) or a PR number when in a git repo."
                };
            }

            logger.LogInformation("Getting check runs for {owner}/{repo}#{prNumber}", parsed.Owner, parsed.Repo, parsed.PrNumber);
            var checks = await pipelineHelper.GetPrCheckRunsAsync(parsed.Owner, parsed.Repo, parsed.PrNumber, ct);

            if (failed)
            {
                checks = checks.Where(c => c.Conclusion == "FAILURE").ToList();
            }
            else if (blocking)
            {
                checks = checks.Where(c => c.Conclusion != "SUCCESS").ToList();
            }

            return new PrChecksResponse
            {
                Checks = checks,
                PrLink = $"https://github.com/{parsed.Owner}/{parsed.Repo}/pull/{parsed.PrNumber}",
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get PR checks for {prLinkOrNumber}", prLinkOrNumber);
            return new PrChecksResponse
            {
                ResponseError = $"Failed to get PR checks for {prLinkOrNumber}: {ex.Message}"
            };
        }
    }
}
