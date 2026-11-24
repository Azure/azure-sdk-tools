// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;

/// <summary>The outcome of breaking-change detection, independent of command execution success.</summary>
[JsonConverter(typeof(JsonStringEnumWithEnumMemberConverter<SdkBreakingChangeStatus>))]
public enum SdkBreakingChangeStatus
{
    /// <summary>Detection completed without finding breaking changes.</summary>
    [EnumMember(Value = "clean")]
    Clean,

    /// <summary>Raw breaking changes were detected; classification was not requested.</summary>
    [EnumMember(Value = "detected")]
    Detected,

    /// <summary>Detected breaking changes were classified and validated.</summary>
    [EnumMember(Value = "classified")]
    Classified,

    /// <summary>A report was returned, but compatibility could not be evaluated against a baseline.</summary>
    [EnumMember(Value = "inconclusive")]
    Inconclusive,

    /// <summary>A required detector configuration is absent; detection did not run.</summary>
    [EnumMember(Value = "blocked")]
    Blocked,

    /// <summary>Detection, report validation, or classification failed.</summary>
    [EnumMember(Value = "failed")]
    Failed,
}
