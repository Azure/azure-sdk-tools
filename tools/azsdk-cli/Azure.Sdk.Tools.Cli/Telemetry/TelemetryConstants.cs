// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Telemetry;

internal static class TelemetryConstants
{
    /// <summary>
    /// Name of tags published.
    /// </summary>
    internal class TagName
    {
        public const string AzSdkToolVersion = "Version";
        public const string ClientName = "ClientName";
        public const string ClientVersion = "ClientVersion";
        public const string DevDeviceId = "DevDeviceId";
        public const string ErrorDetails = "ErrorDetails";
        public const string EventId = "EventId";
        public const string MacAddressHash = "MacAddressHash";
        public const string ToolName = "ToolName";
        public const string ToolArea = "ToolArea";
        public const string ToolArgs = "ToolArgs";
        public const string ToolResponse = "ToolResponse";
        public const string CommandName = "CommandName";
        public const string CommandArea = "CommandArea";
        public const string CommandArgs = "CommandArgs";
        public const string CommandResponse = "CommandResponse";
        public const string DebugTag = "IsDebugEnvironment";

        // Custom Properties that get promoted to tags
        public const string Language = "language";
        public const string PackageName = "package_name";
        public const string TypeSpecProject = "typespec_project";
        public const string PackageType = "package_type";
        public const string PromptTokens = "prompt_tokens";
        public const string CompletionTokens = "completion_tokens";
        public const string TotalTokens = "total_tokens";
        public const string ModelsUsed = "models_used";
        public const string OperationStatus = "operation_status";
        public const string SamplesCount = "samples_count";
    }

    internal class ActivityName
    {
        public const string CommandExecuted = "CommandExecuted";
        public const string ListToolsHandler = "ListToolsHandler";
        public const string ToolExecuted = "ToolExecuted";
    }
}
