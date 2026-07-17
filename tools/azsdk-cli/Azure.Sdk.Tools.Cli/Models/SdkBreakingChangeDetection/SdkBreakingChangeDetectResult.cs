// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection
{
    /// <summary>
    /// Represents the result of detecting breaking changes in an SDK.
    /// </summary>
    public class SdkBreakingChangeDetectResult
    {
        /// <summary>
        /// A list of breaking changes detected in the SDK.
        /// </summary>
        [JsonPropertyName("breakingChanges")]
        public List<SdkBreakingChange> BreakingChanges { get; set; } = new List<SdkBreakingChange>();

        /// <summary>
        /// Indicates whether any breaking changes were detected in the SDK.
        /// </summary>
        [JsonPropertyName("hasBreakingChange")]
        public bool HasBreakingChange { get; set; }

        /// <summary>
        /// The sdk changes in Markdown format.
        /// </summary>
        [JsonPropertyName("changes")]
        public string? SdkChangeMD { get; set; } = null;
    }
}
