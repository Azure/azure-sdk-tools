// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Base class for all environment requirements that can be verified by the setup tool.
/// </summary>
public abstract class Requirement
{
    /// <summary>
    /// Display name (e.g., "Node.js").
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Determines whether this requirement should be checked in the given context.
    /// </summary>
    /// <param name="ctx">The current environment context.</param>
    /// <returns>True if the requirement should be checked, false otherwise.</returns>
    public virtual bool ShouldCheck(RequirementContext ctx) => true;

    /// <summary>
    /// Optional minimum version (e.g., "22.16.0"). Null if no specific version required.
    /// </summary>
    public virtual string? MinVersion => null;

    /// <summary>
    /// Optional maximum version (e.g., "22.99.99"). Null if no specific version required.
    /// </summary>
    public virtual string? MaxVersion => null;

    /// <summary>
    /// Command to run to verify the requirement is installed.
    /// </summary>
    public abstract string[] CheckCommand { get; }

    /// <summary>
    /// Returns context-aware installation instructions.
    /// </summary>
    /// <param name="ctx">The current environment context.</param>
    /// <returns>List of installation instructions.</returns>
    public abstract IReadOnlyList<string> GetInstructions(RequirementContext ctx);

    /// <summary>
    /// Optional reason why the requirement is needed.
    /// </summary>
    public virtual string? Reason;
}
