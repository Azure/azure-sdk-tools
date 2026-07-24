// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;
using Azure.Sdk.Tools.Cli.Models.Serialization;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection
{
    /// <summary>
    /// Represents the category of a breaking change in the SDK.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumWithEnumMemberConverter<SdkBreakingChangeCategory>))]
    public enum SdkBreakingChangeCategory
    {
        [EnumMember(Value = "emitter change")]
        EmitterChange,

        [EnumMember(Value = "conversion-by design")]
        ConversionByDesign,

        [EnumMember(Value = "conversion-need resolve")]
        ConversionNeedResolve,

        [EnumMember(Value = "spec change")]
        SpecChange,

        [EnumMember(Value = "unknown")]
        Unknown
    }
}
