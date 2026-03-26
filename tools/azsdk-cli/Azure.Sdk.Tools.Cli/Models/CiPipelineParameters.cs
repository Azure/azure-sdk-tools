// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// CI pipeline parameters extracted from ci*.yml files.
/// Supports multiple languages with conditional serialization - only non-default values are written.
/// </summary>
public class CiPipelineParameters
{
    // ============================================================
    // .NET-specific parameters
    // ============================================================

    /// <summary>
    /// Whether to build code snippets from samples. Defaults to true (opt-out). (.NET only)
    /// </summary>
    [JsonPropertyName("BuildSnippets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? BuildSnippets { get; set; }

    /// <summary>
    /// Whether to run AOT compatibility checks. (.NET only)
    /// </summary>
    [JsonPropertyName("CheckAOTCompat")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CheckAotCompat { get; set; }

    /// <summary>
    /// AOT test input configurations for the package. (.NET only)
    /// </summary>
    [JsonPropertyName("AOTTestInputs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Dictionary<string, object?>>? AotTestInputs { get; set; }

    // ============================================================
    // Go-specific parameters
    // ============================================================

    /// <summary>
    /// Whether to check licenses. (Go only)
    /// </summary>
    [JsonPropertyName("LicenseCheck")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LicenseCheck { get; set; }

    /// <summary>
    /// Whether package is non-shipping. (Go only)
    /// </summary>
    [JsonPropertyName("NonShipping")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? NonShipping { get; set; }

    /// <summary>
    /// Whether to use pipeline proxy. (Go only)
    /// </summary>
    [JsonPropertyName("UsePipelineProxy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UsePipelineProxy { get; set; }

    /// <summary>
    /// Whether this is an SDK library. (Go only)
    /// </summary>
    [JsonPropertyName("IsSdkLibrary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsSdkLibrary { get; set; }

    // ============================================================
    // Common parameters (all languages)
    // ============================================================

    /// <summary>
    /// CI matrix configurations (MatrixConfigs + AdditionalMatrixConfigs).
    /// </summary>
    [JsonPropertyName("CIMatrixConfigs")]
    public List<Dictionary<string, object?>> MatrixConfigs { get; set; } = [];

    /// <summary>
    /// Default CI parameters for .NET when no ci*.yml is found.
    /// </summary>
    public static CiPipelineParameters DefaultDotNet => new()
    {
        BuildSnippets = true,
        CheckAotCompat = false,
        AotTestInputs = []
    };

    /// <summary>
    /// Default CI parameters for Go when no ci*.yml is found.
    /// </summary>
    public static CiPipelineParameters DefaultGo => new()
    {
        LicenseCheck = true,
        NonShipping = false,
        UsePipelineProxy = true,
        IsSdkLibrary = true
    };
}
