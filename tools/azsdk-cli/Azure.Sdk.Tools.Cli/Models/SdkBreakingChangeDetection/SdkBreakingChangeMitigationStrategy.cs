// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;

[JsonConverter(typeof(JsonStringEnumWithEnumMemberConverter<SdkBreakingChangeMitigationStrategy>))]
public enum SdkBreakingChangeMitigationStrategy
{
    [EnumMember(Value = "manual")]
    Manual,

    [EnumMember(Value = "generator")]
    Generator,

    /// <summary>TypeSpec client customization or handwritten SDK custom code, never generated code.</summary>
    [EnumMember(Value = "client customization")]
    ClientCustomization,
}
