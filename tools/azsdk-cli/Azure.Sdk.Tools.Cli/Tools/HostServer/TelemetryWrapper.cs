// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tools;

public class InstrumentedTool(ILogger logger, McpServerTool innerTool, string toolName) : DelegatingMcpServerTool(innerTool)
{
    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken ct = default)
    {
        try
        {
            TelemetryService.InstrumentationBefore(logger, toolName, request.Params?.Arguments, ct);
            var result = await base.InvokeAsync(request, ct);
            TelemetryService.InstrumentationAfter(logger, toolName, result, ct);
            return result;
        }
        catch (Exception ex)
        {
            TelemetryService.InstrumentationError(logger, toolName, ex, ct);
            throw;
        }
    }
}
