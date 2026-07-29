// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection
{
    /// <summary>
    /// Represents changes in the SDK
    /// </summary>
    public class SdkChange
    {
        /// <summary>
        /// The SDK changes in Markdown format.
        /// </summary>
        [JsonPropertyName("changes")]
        [JsonRequired]
        public string SdkChangeMD { get; set; }

        /// <summary>
        /// Indicates whether the SDK change contains breaking changes.
        /// </summary>
        [JsonPropertyName("hasBreakingChange")]
        [JsonRequired]
        public bool HasBreakingChange { get; set; }
    }
}
