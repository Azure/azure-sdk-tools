// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Models.Responses.SLA;
using Azure.Sdk.Tools.Cli.Services.SLA;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.SLA;

[McpServerToolType, Description("Check SLA status, open issues, and issues needing attention for Azure SDK service areas")]
public class SLAStatusTool(
    ISLAMetricsService slaMetricsService,
    ILogger<SLAStatusTool> logger
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } =
    [
        SharedCommandGroups.SLA
    ];

    private const string SLAStatusCommandName = "status";
    private const string SLAStatusToolName = "azsdk_sla_status";

    private readonly Option<string> serviceOpt = new("--service", "-s")
    {
        Description = "Service label to query (e.g., KeyVault, Storage)",
        Required = true,
    };

    private readonly Option<string?> repoOpt = new("--repo", "-r")
    {
        Description = "Specific repo name (e.g., azure-sdk-for-python). Defaults to all SDK repos.",
        Required = false,
    };

    private readonly Option<int> daysOpt = new("--days", "-d")
    {
        Description = "Look-back window in days. Must exceed longest SLA threshold to capture breached issues.",
        Required = false,
        DefaultValueFactory = _ => 180,
    };

    private readonly Option<int> approachingWindowOpt = new("--approaching-window")
    {
        Description = "Days before SLA breach to flag as 'approaching'.",
        Required = false,
        DefaultValueFactory = _ => 7,
    };

    private readonly Option<bool> includeClosedOpt = new("--include-closed")
    {
        Description = "Include recently closed issues in metrics.",
        Required = false,
        DefaultValueFactory = _ => false,
    };

    protected override Command GetCommand() =>
        new McpCommand(SLAStatusCommandName, "Check SLA status for a service area", SLAStatusToolName)
        {
            serviceOpt,
            repoOpt,
            daysOpt,
            approachingWindowOpt,
            includeClosedOpt,
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var service = parseResult.GetValue(serviceOpt)!;
        var repo = parseResult.GetValue(repoOpt);
        var days = parseResult.GetValue(daysOpt);
        var approachingWindow = parseResult.GetValue(approachingWindowOpt);
        var includeClosed = parseResult.GetValue(includeClosedOpt);

        return await GetSLAStatus(service, repo, days, approachingWindow, includeClosed, ct);
    }

    [McpServerTool(Name = SLAStatusToolName), Description(
        "Check SLA status metrics for an Azure SDK service area. Returns FQR (First Question Response), " +
        "bug resolution, and question resolution compliance, plus lists of approaching and breached issues. " +
        "Use this when asked about SLA status, open issues for a service, issues that need attention, " +
        "issue response times, or breached SLAs.")]
    public async Task<SLAStatusResponse> GetSLAStatus(
        [Description("Service label to query (e.g., KeyVault, Storage, EventHubs)")] string service,
        [Description("Specific repo name (e.g., azure-sdk-for-python). Omit to query all SDK repos.")] string? repo = null,
        [Description("Look-back window in days (default: 180)")] int lookbackDays = 180,
        [Description("Days before SLA breach to flag as approaching (default: 7)")] int approachingWindowDays = 7,
        [Description("Include recently closed issues in metrics")] bool includeClosed = false,
        CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Computing SLA status for service '{Service}' in {Repo}",
                service, repo ?? "all repos");

            return await slaMetricsService.ComputeSLAStatusAsync(
                service, repo, lookbackDays, approachingWindowDays, includeClosed, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to compute SLA status for service '{Service}'", service);
            return new SLAStatusResponse
            {
                Service = service,
                ResponseError = $"Failed to compute SLA status: {ex.Message}",
                NextSteps = ["Verify the service label exists in the target repo(s)", "Check your GitHub authentication (gh auth status)"],
            };
        }
    }
}
