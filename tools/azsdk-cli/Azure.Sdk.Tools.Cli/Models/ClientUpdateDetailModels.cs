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
    /// <summary>
    /// Optional structured metadata for language-specific enrichments (e.g. returnType, parametersBefore/After).
    /// Keys should be lowerCamelCase. Consumers must treat absence the same as empty.
    /// </summary>
    [JsonPropertyName("meta")] public Dictionary<string, string> Metadata { get; set; } = new();
}

public class CustomizationImpact
{
    /// <summary>Customization source file impacted by at least one API change.</summary>
    [JsonPropertyName("file")] public string File { get; set; } = string.Empty;
    
    /// <summary>Type of impact (e.g., ParameterNameConflict, MethodRemoval, ReturnTypeChange).</summary>
    [JsonPropertyName("impactType")] public string ImpactType { get; set; } = string.Empty;
    
    /// <summary>Severity level: Critical, High, Moderate, Low, Info.</summary>
    [JsonPropertyName("severity")] public string Severity { get; set; } = string.Empty;
    
    /// <summary>Human-readable description of the impact and recommended action.</summary>
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    
    /// <summary>The API symbol (method, class, field) being impacted.</summary>
    [JsonPropertyName("affectedSymbol")] public string AffectedSymbol { get; set; } = string.Empty;
    
    /// <summary>Approximate line range in customization file where impact occurs (e.g., "65-75").</summary>
    [JsonPropertyName("lineRange")] public string LineRange { get; set; } = string.Empty;
    
    /// <summary>The original API change that caused this impact.</summary>
    [JsonPropertyName("apiChange")] public ApiChange ApiChange { get; set; } = new();
}

public class PatchProposal
{
    /// <summary>Path to the customization file.</summary>
    [JsonPropertyName("file")] public string File { get; set; } = string.Empty;

    /// <summary>Git-style diff showing the proposed changes.</summary>
    [JsonPropertyName("diff")] public string Diff { get; set; } = string.Empty;
    
    /// <summary>The specific customization impact this patch addresses.</summary>
    [JsonPropertyName("impactId")] public string ImpactId { get; set; } = string.Empty;
    
    /// <summary>Original code that needs to be changed.</summary>
    [JsonPropertyName("originalCode")] public string OriginalCode { get; set; } = string.Empty;
    
    /// <summary>Proposed fixed code.</summary>
    [JsonPropertyName("fixedCode")] public string FixedCode { get; set; } = string.Empty;
    
    /// <summary>Line range affected by this patch (e.g., "45-47").</summary>
    [JsonPropertyName("lineRange")] public string LineRange { get; set; } = string.Empty;
    
    /// <summary>Explanation of why this fix is recommended.</summary>
    [JsonPropertyName("rationale")] public string Rationale { get; set; } = string.Empty;
    
    /// <summary>Confidence level of the fix (High, Medium, Low).</summary>
    [JsonPropertyName("confidence")] public string Confidence { get; set; } = string.Empty;
}

/// <summary>
/// Result of applying patches to customization files.
/// </summary>
public class PatchApplicationResult
{
    /// <summary>Overall success status of the patch application.</summary>
    public bool Success { get; set; }
    
    /// <summary>List of patches that were successfully applied.</summary>
    public List<string> AppliedPatches { get; set; } = new();
    
    /// <summary>List of patches that failed to apply.</summary>
    public List<string> FailedPatches { get; set; } = new();
    
    /// <summary>Detailed error messages from patch application failures.</summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>Mapping of original files to their backup file paths.</summary>
    public Dictionary<string, string> BackupFiles { get; set; } = new();
    
    /// <summary>Total number of patches attempted.</summary>
    public int TotalPatches { get; set; }
    
    /// <summary>Number of successfully applied patches.</summary>
    public int SuccessfulPatches => AppliedPatches.Count;
    
    /// <summary>Number of failed patch applications.</summary>
    public int FailedPatchesCount => FailedPatches.Count;
}

/// <summary>
/// Structured context for LLM analysis of API changes and dependency chains
/// </summary>
public class StructuredApiChangeContext
{
    public List<ApiChange> Changes { get; set; } = new();
    public Dictionary<string, List<ApiChange>> ChangesByKind { get; set; } = new();
    public List<ApiChange> MethodChanges { get; set; } = new();
    public List<ApiChange> ParameterChanges { get; set; } = new();
    public List<ApiChange> TypeChanges { get; set; } = new();
}
