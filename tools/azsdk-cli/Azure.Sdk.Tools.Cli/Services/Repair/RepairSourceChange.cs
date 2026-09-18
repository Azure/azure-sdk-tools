// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services.Repair;

public record RepairSourceChange(string Path, string Status, string OldMode, string NewMode);
