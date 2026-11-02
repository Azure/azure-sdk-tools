// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

internal static class PackageValidationCallers
{
    internal static int HandleCli(PackageValidationResponse response) => response.ExitCode;

    internal static string HandleMcp(PackageValidationResponse response) => JsonSerializer.Serialize(response);
}
