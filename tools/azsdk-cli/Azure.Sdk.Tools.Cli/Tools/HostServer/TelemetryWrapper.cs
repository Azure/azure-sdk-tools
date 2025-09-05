// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Configuration;
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Tools;

public class InstrumentedTool(ILogger logger, McpServerTool innerTool, string toolName) : DelegatingMcpServerTool(innerTool)
{
    private static readonly ActivitySource source = new(Constants.TOOLS_ACTIVITY_SOURCE);

    private readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = false,
    };

    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken ct = default)
    {
        using var activity = source.StartActivity("tool.invoke", ActivityKind.Internal);
        if (activity == null)
        {
            logger.LogError("Null activity created for tool {ToolName}", toolName);
        }

        try
        {
            activity?.SetTag("name", toolName);
            var args = JsonSerializer.Serialize(request.Params?.Arguments, serializerOptions);
            activity?.SetTag("args", args);

            var result = await base.InvokeAsync(request, ct);

            var content = JsonSerializer.Serialize(result.Content);
            activity?.SetTag("result", content);
            activity?.SetStatus(ActivityStatusCode.Ok);

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
