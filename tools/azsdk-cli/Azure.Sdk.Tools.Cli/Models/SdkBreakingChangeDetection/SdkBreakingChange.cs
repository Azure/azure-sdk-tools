// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection
{
    /// <summary>
    /// Represents a breaking change in the SDK.
    /// </summary>
    public class SdkBreakingChange
    {
        /// <summary>
        /// The description of the breaking change.
        /// </summary>
        [JsonPropertyName("breakingChange")]
        [JsonRequired]
        public string BreakingChange { get; set; }

        /// <summary>
        /// The category of the breaking change. it can be one of the following: "emitter change", "conversion-by design", "conversion-need resolve", "spec change", "unknown".
        /// </summary>
        [JsonPropertyName("category")]
        [JsonRequired]
        public string Category { get; set; }

        /// <summary>
        /// The resolution for the breaking change, if available.
        /// </summary>
        [JsonPropertyName("resolution")]
        public string? Resolution { get; set; }

        /// <summary>
        /// The original breaking changes that this change is related to, if any.
        /// </summary>
        [JsonPropertyName("originBreaks")]
        public List<string>? OriginBreaks { get; set; }
    }
}
