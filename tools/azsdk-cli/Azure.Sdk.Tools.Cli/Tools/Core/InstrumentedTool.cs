// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Telemetry;
using static Azure.Sdk.Tools.Cli.Telemetry.TelemetryConstants;

namespace Azure.Sdk.Tools.Cli.Tools;

public class InstrumentedTool(
    ITelemetryService telemetryService,
    ILogger logger,
    McpServerTool innerTool
) : DelegatingMcpServerTool(innerTool)
{
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = false,
    };

    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken ct = default)
    {
        using var activity = await telemetryService.StartActivity(ActivityName.ToolExecuted, request?.Server?.ClientInfo);
        Activity.Current = activity;
        if (request?.Params == null || string.IsNullOrEmpty(request.Params.Name))
        {
            activity?.SetStatus(ActivityStatusCode.Error)?.AddTag(TagName.ErrorDetails, "Cannot call tool with null parameters");
            logger.LogWarning("Tool request or tool name is null or empty");
            return await base.InvokeAsync(request, ct);
        }

        try
        {
            // Add tool name and arg
            activity?.AddTag(TagName.ToolName, request.Params.Name);
            var args = JsonSerializer.Serialize(request.Params.Arguments, serializerOptions);
            activity?.SetTag(TagName.ToolArgs, args);

            // Invoke the tool
            var result = await base.InvokeAsync(request, ct);

            // Tag response in the activity
            var content = JsonSerializer.Serialize(result.Content);
            activity?.SetTag(TagName.ToolResponse, content);
            activity?.SetStatus(ActivityStatusCode.Ok);

            try
            {
                foreach (var c in result.Content)
                {
                    // Process only TextContentBlock for custom properties
                    // Other content types are binary, audiio etc.
                    if (c is TextContentBlock contentBlock)
                    {
                        var responseDict = JsonSerializer.Deserialize<Dictionary<string, object>>(contentBlock.Text);
                        if (responseDict != null)
                        {
                            foreach (var kvp in responseDict)
                            {
                                if (kvp.Value != null)
                                {
                                    activity?.SetCustomProperty(kvp.Key, JsonSerializer.Serialize(kvp.Value));
                                }
                            }                  
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Failed to deserialize contentBlock.Text for telemetry properties");
            }
            return result;
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
