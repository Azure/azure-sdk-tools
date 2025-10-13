using OpenTelemetry;
using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Telemetry;

public sealed class TelemetryProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity activity)
    {
    }

    public override void OnEnd(Activity activity)
    {
        // TODO: Add progress/logging for MCP clients so we can see the spans in debug mode
        // without it being treated as a parse failure when using AddConsoleExporter()

        // Do any post-processing work here
        /*
        var toolName = activity.GetTagItem("mcp.tool.name") as string;
        if (!string.IsNullOrEmpty(toolName))
        {
            activity.SetTag("CustomToolProperty", toolName);
        }
        */
    }
}
