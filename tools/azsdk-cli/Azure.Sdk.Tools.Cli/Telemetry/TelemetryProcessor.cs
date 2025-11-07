using System.Diagnostics;
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
        if (activity.GetCustomProperty(TelemetryConstants.TagName.SdkType) is SdkType sdkType)
        {
            activity.SetTag(TelemetryConstants.TagName.SdkType, sdkType);
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
    }
}
