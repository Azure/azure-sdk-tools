using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Telemetry;

// This class contains fields that can be set per request scope
// by callers to track token usage
public static class TelemetryContext
{
    public static TokenUsageHelper TokenUsageHelper { get; set; }

    public static void Reset(TokenUsageHelper tokenUsageHelper)
    {
        TokenUsageHelper = tokenUsageHelper;
    }
}
