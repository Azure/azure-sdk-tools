// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// The mode in which tests are executed.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TestMode
{
    /// <summary>
    /// Tests run against previously recorded HTTP interactions without hitting real Azure resources.
    /// </summary>
    Playback,

    /// <summary>
    /// Tests run against real Azure resources and record HTTP interactions for later playback.
    /// </summary>
    Record,

    /// <summary>
    /// Tests run against real Azure resources without recording.
    /// </summary>
    Live
}
