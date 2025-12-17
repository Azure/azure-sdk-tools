using System.Diagnostics;
using OpenTelemetry;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Telemetry;

public sealed class TelemetryProcessor : BaseProcessor<Activity>
{
    private const string NotApplicable = "N/A";
    private string[] CustomTelemetryProperties = {
        // Tool/Command telemetry
        TelemetryConstants.TagName.Language,
        TelemetryConstants.TagName.PackageName,
        TelemetryConstants.TagName.TypeSpecProject,
        TelemetryConstants.TagName.PackageType,
        TelemetryConstants.TagName.OperationStatus
    };

    // TokenUsageHelper telemetry
    private string[] TokenUsageTelemetryProperties = {
        TelemetryConstants.TagName.PromptTokens,
        TelemetryConstants.TagName.CompletionTokens,
        TelemetryConstants.TagName.TotalTokens,
        TelemetryConstants.TagName.ModelsUsed
    };
    public override void OnStart(Activity activity)
    {
    }

    public override void OnEnd(Activity activity)
    {
        foreach (var item in CustomTelemetryProperties)
        {
            if (activity.GetCustomProperty(item) is string value)
            {
                activity.SetTag(item, value);
            }
            else
            {
                activity.SetTag(item, NotApplicable);
            }
        }

        foreach (var item in TokenUsageTelemetryProperties)
        {
            if (activity.GetCustomProperty(item) is string value)
            {
                activity.SetTag(item, value);
            }
        }
    }
}
