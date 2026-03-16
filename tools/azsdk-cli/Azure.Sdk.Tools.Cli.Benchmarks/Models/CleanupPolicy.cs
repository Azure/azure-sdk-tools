// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Specifies when workspace cleanup should occur.
/// </summary>
public enum CleanupPolicy
{
    /// <summary>
    /// Always cleanup the workspace after execution.
    /// </summary>
    Always,

    /// <summary>
    /// Never cleanup the workspace after execution.
    /// </summary>
    Never,

    /// <summary>
    /// Only cleanup the workspace if the execution was successful.
    /// </summary>
    OnSuccess
}
