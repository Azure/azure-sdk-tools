// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Detailed change model types supporting the client update pipeline. Not persisted in the minimal session; produced ad hoc by services.
/// </summary>
public class ApiChange
{
    /// <summary>Change category (e.g. MethodAdded, MethodRemoved).</summary>
    [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;
    /// <summary>
    /// Primary symbol id representing the API element affected by the change.
    /// This is typically a fully qualified name (e.g., Namespace.Class.Method) or a unique identifier
    /// that allows consumers to locate the specific symbol in the codebase or API surface.
    /// </summary>
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = string.Empty;
    /// <summary>Human-readable description.</summary>
    [JsonPropertyName("detail")] public string Detail { get; set; } = string.Empty;
}

/// <summary>
/// Represents a code patch to be applied to a file.
/// </summary>
public class CodePatch
{
    public string FilePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OldContent { get; set; } = string.Empty;
    public string NewContent { get; set; } = string.Empty;
}
