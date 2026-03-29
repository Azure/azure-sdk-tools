using System.Diagnostics;
using System.Linq;
using OpenTelemetry;
using Azure.Sdk.Tools.Cli.Models;
namespace Azure.Sdk.Tools.Cli.Telemetry;

public sealed class TelemetryProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity activity)
    {
    }

    public override void OnEnd(Activity activity)
    {
        // Tool/Command telemetry
        if (activity.GetCustomProperty(TelemetryConstants.TagName.Language) is string language)
        {
            activity.SetTag(TelemetryConstants.TagName.Language, language);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.PackageName) is string packageName)
        {
            activity.SetTag(TelemetryConstants.TagName.PackageName, packageName);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.TypeSpecProject) is string typeSpecProject)
        {
            activity.SetTag(TelemetryConstants.TagName.TypeSpecProject, typeSpecProject);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.PackageType) is string packageType)
        {
            activity.SetTag(TelemetryConstants.TagName.PackageType, packageType);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.OperationStatus) is string operationStatus)
        {
            activity.SetTag(TelemetryConstants.TagName.OperationStatus, operationStatus);
        }

        // TokenUsageHelper telemetry
        if (activity.GetCustomProperty(TelemetryConstants.TagName.PromptTokens) is string promptTokens)
        {
            activity.SetTag(TelemetryConstants.TagName.PromptTokens, promptTokens);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.CompletionTokens) is string completionTokens)
        {
            activity.SetTag(TelemetryConstants.TagName.CompletionTokens, completionTokens);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.TotalTokens) is string totalTokens)
        {
            activity.SetTag(TelemetryConstants.TagName.TotalTokens, totalTokens);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.ModelsUsed) is string modelsUsed)
        {
            activity.SetTag(TelemetryConstants.TagName.ModelsUsed, modelsUsed);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.SamplesCount) is string samplesCount)
        {
            activity.SetTag(TelemetryConstants.TagName.SamplesCount, samplesCount);
        }

        SanitizeTags(activity);
    }

    private static void SanitizeTags(Activity activity)
    {
        foreach (var tag in activity.TagObjects.ToList())
        {
            if (tag.Value is string value)
            {
                var sanitized = TelemetryPathSanitizer.Sanitize(value);
                if (!string.Equals(sanitized, value, StringComparison.Ordinal))
                {
                    activity.SetTag(tag.Key, sanitized);
                }
            }
        }
    }
}
