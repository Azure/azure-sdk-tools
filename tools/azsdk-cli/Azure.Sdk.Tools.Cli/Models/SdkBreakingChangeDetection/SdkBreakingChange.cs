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
        public SdkBreakingChangeCategory Category { get; set; }

        /// <summary>
        /// Actionable instructions for resolving the breaking change, if available.
        /// Retained for customization consumers; unlike Mitigation, this describes what to do.
        /// </summary>
        [JsonPropertyName("resolution")]
        public string? Resolution { get; set; }

        /// <summary>
        /// The mitigation route (generator, client customization, or manual), not the fix instructions.
        /// Client customization includes TypeSpec client customization and handwritten SDK custom
        /// code, never generated code.
        /// Detection and classification never apply the mitigation themselves.
        /// </summary>
        [JsonPropertyName("mitigation")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SdkBreakingChangeMitigation? Mitigation { get; set; }

        /// <summary>
        /// The original breaking changes that this change is related to, if any.
        /// </summary>
        [JsonPropertyName("originBreaks")]
        public List<string>? OriginBreaks { get; set; }
    }
}
