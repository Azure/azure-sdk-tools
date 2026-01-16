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
        
        // Conversation tracking telemetry
        if (activity.GetCustomProperty(TelemetryConstants.TagName.ConversationTopic) is string conversationTopic)
        {
            activity.SetTag(TelemetryConstants.TagName.ConversationTopic, conversationTopic);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.ConversationContext) is string conversationContext)
        {
            activity.SetTag(TelemetryConstants.TagName.ConversationContext, conversationContext);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.ConversationTags) is string conversationTags)
        {
            activity.SetTag(TelemetryConstants.TagName.ConversationTags, conversationTags);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.ConversationSummary) is string conversationSummary)
        {
            activity.SetTag(TelemetryConstants.TagName.ConversationSummary, conversationSummary);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.ConversationCategory) is string conversationCategory)
        {
            activity.SetTag(TelemetryConstants.TagName.ConversationCategory, conversationCategory);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.SessionId) is string sessionId)
        {
            activity.SetTag(TelemetryConstants.TagName.SessionId, sessionId);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.SessionDurationMinutes) is string sessionDurationMinutes)
        {
            activity.SetTag(TelemetryConstants.TagName.SessionDurationMinutes, sessionDurationMinutes);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.MessageCount) is string messageCount)
        {
            activity.SetTag(TelemetryConstants.TagName.MessageCount, messageCount);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.AiAnalysisCompleted) is string aiAnalysisCompleted)
        {
            activity.SetTag(TelemetryConstants.TagName.AiAnalysisCompleted, aiAnalysisCompleted);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.AiConfidenceScore) is string aiConfidenceScore)
        {
            activity.SetTag(TelemetryConstants.TagName.AiConfidenceScore, aiConfidenceScore);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.SessionStartTime) is string sessionStartTime)
        {
            activity.SetTag(TelemetryConstants.TagName.SessionStartTime, sessionStartTime);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.InitialContext) is string initialContext)
        {
            activity.SetTag(TelemetryConstants.TagName.InitialContext, initialContext);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.TrackingTimestamp) is string trackingTimestamp)
        {
            activity.SetTag(TelemetryConstants.TagName.TrackingTimestamp, trackingTimestamp);
        }
        if (activity.GetCustomProperty(TelemetryConstants.TagName.NotificationSent) is string notificationSent)
        {
            activity.SetTag(TelemetryConstants.TagName.NotificationSent, notificationSent);
        }
    }
}
