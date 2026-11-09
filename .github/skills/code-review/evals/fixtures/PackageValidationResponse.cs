// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

public sealed class PackageValidationResponse : CommandResponse
{
    public string? Message { get; set; }

    protected override string Format() => Message ?? string.Empty;
}
