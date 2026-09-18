// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public sealed record CustomizedCodeRepairChange(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("status")] string Status);
