// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Helpers;
using static Azure.Sdk.Tools.Cli.Telemetry.TelemetryConstants;

namespace Azure.Sdk.Tools.Cli.Tools.Core;

public class InstrumentedTool : DelegatingMcpServerTool
{
    private readonly ITelemetryService telemetryService;
    private readonly ILogger logger;
    private readonly IMcpServerContextAccessor mcpServerContextAccessor;
    private readonly McpServerTool innerTool;
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = false,
    };

    public InstrumentedTool(
        ITelemetryService telemetryService,
        ILogger logger,
        IMcpServerContextAccessor mcpServerContextAccessor,
        McpServerTool innerTool
    ) : base(innerTool)
    {
        this.telemetryService = telemetryService;
        this.mcpServerContextAccessor = mcpServerContextAccessor;
        this.logger = logger;
        this.innerTool = innerTool;
    }

    public override IReadOnlyList<object> Metadata => innerTool.Metadata;

    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken ct = default)
    {
        mcpServerContextAccessor.Initialize(request?.Server);
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
                                if (kvp.Value is JsonElement value)
                                {
                                    switch (value.ValueKind)
                                    {
                                        // Add custom properties based on the value kind. Otherwise string type is url escaped in telemetry
                                        // like \"python\" instead of python
                                        case JsonValueKind.String:
                                            activity?.SetCustomProperty(kvp.Key, value.GetString() ?? string.Empty);
                                            break;
                                        default:
                                            activity?.SetCustomProperty(kvp.Key, JsonSerializer.Serialize(kvp.Value));
                                            break;
                                    }
                                }
                                else
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
