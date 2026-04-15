// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Telemetry;
using ModelContextProtocol.Server;
using OpenTelemetry.Trace;

namespace Azure.Sdk.Tools.Cli.Tools.Core;

[McpServerToolType, Description("Ingest telemetry from copilot hooks into Application Insights")]
public class TelemetryIngestionTool : MCPTool
{
    private const string IngestCommandName = "ingest-telemetry";
    private const string EventTypeSkillInvocation = "skill_invocation";
    private const string EventTypeUserPrompt = "user_prompt";

    private readonly ITelemetryService telemetryService;
    private readonly IUserPromptProcessor userPromptProcessor;
    private readonly ILogger<TelemetryIngestionTool> logger;

    private readonly Option<string> clientTypeOption = new Option<string>("--client-type")
    {
        Description = "Client that generated the telemetry event (vscode or copilot-cli)",
        Required = true
    };

    private readonly Option<string> eventTypeOption = new Option<string>("--event-type")
    {
        Description = "Type of telemetry event. Common values are 'skill_invocation' and 'user_prompt'; custom event types are also supported",
        Required = true
    };

    private readonly Option<string?> sessionIdOption = new Option<string?>("--session-id")
    {
        Description = "Optional session ID associated with the telemetry event"
    };

    private readonly Option<string?> skillNameOption = new Option<string?>("--skill-name")
    {
        Description = "Skill name associated with the telemetry event (required for skill_invocation events)"
    };

    private readonly Option<string?> bodyOption = new Option<string?>("--body")
    {
        Description = "User prompt body to be analyzed (required for user_prompt events)"
    };

    public TelemetryIngestionTool(
        ITelemetryService telemetryService,
        IUserPromptProcessor userPromptProcessor,
        ILogger<TelemetryIngestionTool> logger)
    {
        this.telemetryService = telemetryService;
        this.userPromptProcessor = userPromptProcessor;
        this.logger = logger;
    }

    protected override Command GetCommand()
    {
        var cmd = new McpCommand(IngestCommandName, "Ingest telemetry events into Application Insights")
        {
            clientTypeOption,
            eventTypeOption,
            skillNameOption,
            sessionIdOption,
            bodyOption
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
        var body = parseResult.GetValue(bodyOption);

        return await IngestActivityLog(clientType, eventType, sessionId, skillName, body, ct);
    }

    public async Task<CommandResponse> IngestActivityLog(
        string clientType,
        string eventType,
        string? sessionId = null,
        string? skillName = null,
        string? body = null,
        CancellationToken ct = default)
    {
        var response = new TelemetryIngestionResponse
        {
            ClientType = clientType,
            EventType = eventType,
            SessionId = sessionId,
            SkillName = skillName
        };

        // Validate event-type-specific requirements
        var validationError = ValidateEventType(eventType, skillName, body);
        if (validationError != null)
        {
            response.ResponseError = validationError;
            return response;
        }

        try
        {
            if (string.Equals(eventType, EventTypeUserPrompt, StringComparison.OrdinalIgnoreCase))
            {
                var analysisResult = await userPromptProcessor.AnalyzePromptAsync(body!, ct);
                if (!analysisResult.IsSuccessful)
                {
                    logger.LogWarning("Prompt analysis failed; skipping prompt telemetry fields");
                }
                else
                {
                    response.PromptCategory = analysisResult.Category;
                    response.PromptDetails = analysisResult.PromptSummary;
                    response.Language = analysisResult.Language;
                    response.TypeSpecProject = analysisResult.TypeSpecProject;
                    response.PackageName = analysisResult.PackageName;
                }
            }

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

    private static string? ValidateEventType(string eventType, string? skillName, string? body)
    {
        if (string.Equals(eventType, EventTypeSkillInvocation, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(skillName))
            {
                return "skill-name is required when event-type is 'skill_invocation'.";
            }
        }
        else if (string.Equals(eventType, EventTypeUserPrompt, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return "body is required when event-type is 'user_prompt'.";
            }
        }

        return null;
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
        SetActivityTag(activity, TelemetryConstants.TagName.PromptCategory, response.PromptCategory);
        SetActivityTag(activity, TelemetryConstants.TagName.PromptDetails, response.PromptDetails);
        SetActivityTag(activity, TelemetryConstants.TagName.Language, response.Language);
        SetActivityTag(activity, TelemetryConstants.TagName.TypeSpecProject, response.TypeSpecProject);
        SetActivityTag(activity, TelemetryConstants.TagName.PackageName, response.PackageName);
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
