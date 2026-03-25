// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Telemetry;
using ModelContextProtocol.Server;
using OpenTelemetry.Trace;

namespace Azure.Sdk.Tools.Cli.Tools.Core;

[McpServerToolType, Description("Ingest telemetry from copilot hooks into Application Insights")]
public class TelemetryIngestionTool : MCPTool
{
    private const string IngestCommandName = "ingest-telemetry";
    private readonly ITelemetryService telemetryService;
    private readonly ILogger<TelemetryIngestionTool> logger;

    private readonly Option<string> clientTypeOption = new Option<string>("--client-type")
    {
        Description = "Client that generated the telemetry event (vscode or copilot-cli)",
        Required = true
    };

    private readonly Option<string> eventTypeOption = new Option<string>("--event-type")
    {
        Description = "Type of telemetry event (used as the activity name)",
        Required = true
    };

    private readonly Option<string?> sessionIdOption = new Option<string?>("--session-id")
    {
        Description = "Optional session ID associated with the telemetry event"
    };

    private readonly Option<string?> skillNameOption = new Option<string?>("--skill-name")
    {
        Description = "skill name associated with the telemetry event",
        Required = true
    };

    public TelemetryIngestionTool(
        ITelemetryService telemetryService,
        ILogger<TelemetryIngestionTool> logger)
    {
        this.telemetryService = telemetryService;
        this.logger = logger;
    }

    protected override Command GetCommand()
    {
        var cmd = new McpCommand(IngestCommandName, "Ingest telemetry events into Application Insights")
        {
            clientTypeOption,
            eventTypeOption,
            skillNameOption,
            sessionIdOption
        };
        cmd.Hidden = true;
        return cmd;
    }
    
    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var clientType = parseResult.GetValue(clientTypeOption)!;
        var eventType = parseResult.GetValue(eventTypeOption)!;
        var sessionId = parseResult.GetValue(sessionIdOption);
        var skillName = parseResult.GetValue(skillNameOption);

        return await IngestActivityLog(clientType, eventType, sessionId, skillName, ct);
    }

    public async Task<CommandResponse> IngestActivityLog(
        string clientType,
        string eventType,
        string? sessionId = null,
        string? skillName = null,
        CancellationToken ct = default)
    {
        var response = new TelemetryIngestionResponse
        {
            ClientType = clientType,
            EventType = eventType,
            SessionId = sessionId,
            SkillName = skillName
        };

        try
        {
            await RecordActivityAsync(response, ct);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest telemetry event {EventType}", eventType);
            response.ResponseError = $"Failed to ingest telemetry event: {ex.Message}";
            return response;
        }
    }

    private async Task RecordActivityAsync(TelemetryIngestionResponse response, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var activity = await telemetryService.StartActivity(response.EventType!, ct);
        if (activity == null)
        {
            return;
        }

        SetActivityTag(activity, TelemetryConstants.TagName.ClientName, response.ClientType);
        SetActivityTag(activity, TelemetryConstants.TagName.SessionId, response.SessionId);
        SetActivityTag(activity, TelemetryConstants.TagName.SkillName, response.SkillName);
        activity.SetStatus(ActivityStatusCode.Ok);
        activity.Dispose();
    }

    private static void SetActivityTag(Activity? activity, string key, string? value)
    {
        if (activity == null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        activity.SetTag(key, value);
    }
}
