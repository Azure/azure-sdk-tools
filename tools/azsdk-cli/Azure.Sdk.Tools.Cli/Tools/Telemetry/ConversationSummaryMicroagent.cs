// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Microagents;

namespace Azure.Sdk.Tools.Cli.Tools.Telemetry
{
    public class ConversationSummaryResult
    {
        public string Topic { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public string Category { get; set; } = string.Empty;
        public int ConfidenceScore { get; set; } = 0; // 0-100
    }
}