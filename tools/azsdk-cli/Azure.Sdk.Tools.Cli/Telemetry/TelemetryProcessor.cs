using System.Diagnostics;
using OpenTelemetry;

namespace Azure.Sdk.Tools.Cli.Telemetry;

public sealed class TelemetryProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity activity)
    {
    }

    public override void OnEnd(Activity activity)
    {
        if (TelemetryContext.TokenUsageHelper?.TotalTokens > 0)
        {
            activity.SetTag("PromptTokens", TelemetryContext.TokenUsageHelper.PromptTokens);
            activity.SetTag("CompletionTokens", TelemetryContext.TokenUsageHelper.CompletionTokens);
            activity.SetTag("TotalTokens", TelemetryContext.TokenUsageHelper.TotalTokens);
            activity.SetTag("Models", TelemetryContext.TokenUsageHelper.ModelsUsed);
        }
    }
}
