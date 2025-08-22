// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// CLI stage selector for TSP update workflow.
/// Keep this limited to user-facing stages.
/// </summary>
public enum TspStageSelection
{
    All,
    Regenerate,
    Diff,
    Apply
}
