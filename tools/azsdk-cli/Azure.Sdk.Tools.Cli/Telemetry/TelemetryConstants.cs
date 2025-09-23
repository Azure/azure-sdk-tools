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
    }

    internal class ActivityName
    {
        public const string CommandExecuted = "CommandExecuted";
        public const string ListToolsHandler = "ListToolsHandler";
        public const string ToolExecuted = "ToolExecuted";
    }
}
