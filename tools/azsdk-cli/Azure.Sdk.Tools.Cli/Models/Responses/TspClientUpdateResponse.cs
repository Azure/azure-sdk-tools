// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Response payload for TspClientUpdateTool MCP / CLI operations.
/// </summary>
public class TspClientUpdateResponse : Response
{
    [JsonPropertyName("session")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UpdateSessionState? Session { get; set; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("errorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("nextStep")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextStep { get; set; }

    [JsonPropertyName("nextTool")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextTool { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(Message))
        {
            sb.AppendLine(Message);
        }
        if (Session != null)
        {
            sb.AppendLine($"Session: {Session.SessionId} Status: {Session.Status} API changes: {Session.ApiChanges.Count} Impacted: {Session.ImpactedCustomizations.Count} Patches: {Session.ProposedPatches.Count} Next: {NextStep} NextTool: {NextTool}");
        }
        if (!string.IsNullOrWhiteSpace(ErrorCode))
        {
            sb.AppendLine($"ErrorCode: {ErrorCode}");
        }
        return ToString(sb.ToString());
    }
}

public enum UpdateStage
{
    Initialized,
    Regenerated,
    Diffed,
    Mapped,
    Merged,
    PatchesProposed,
    AppliedDryRun,
    Applied,
    Stale
}

public class UpdateSessionState
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; } = 1;
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = Guid.NewGuid().ToString("n");
    [JsonPropertyName("specPath")] public string SpecPath { get; set; } = string.Empty;
    [JsonPropertyName("language")] public string Language { get; set; } = string.Empty; // language key (java, csharp, etc.)
    [JsonPropertyName("oldGeneratedPath")] public string OldGeneratedPath { get; set; } = string.Empty;
    [JsonPropertyName("newGeneratedPath")] public string NewGeneratedPath { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = "Initialized";
    [JsonPropertyName("lastStage")] public UpdateStage LastStage { get; set; } = UpdateStage.Initialized;
    [JsonPropertyName("createdUtc")] public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    [JsonPropertyName("updatedUtc")] public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    [JsonPropertyName("createdBy")] public string CreatedBy { get; set; } = Environment.UserName;
    [JsonPropertyName("toolVersion")] public string ToolVersion { get; set; } = string.Empty;
    [JsonPropertyName("apiChangeCount")] public int ApiChangeCount { get; set; } = 0;
    [JsonPropertyName("impactedCount")] public int ImpactedCount { get; set; } = 0;
    [JsonPropertyName("patchesAppliedSuccess")] public int PatchesAppliedSuccess { get; set; } = 0;
    [JsonPropertyName("patchesAppliedFailed")] public int PatchesAppliedFailed { get; set; } = 0;
    [JsonPropertyName("apiChanges")] public List<ApiChange> ApiChanges { get; set; } = new();
    [JsonPropertyName("impactedCustomizations")] public List<CustomizationImpact> ImpactedCustomizations { get; set; } = new();
    [JsonPropertyName("directMergeFiles")] public List<string> DirectMergeFiles { get; set; } = new();
    [JsonPropertyName("proposedPatches")] public List<PatchProposal> ProposedPatches { get; set; } = new();
}

public class ApiChange
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty; // Enum name style e.g. MethodAdded, MethodRemoved, MethodSignatureChanged
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = string.Empty; // Primary stable symbol id (usually old id if removed, new id otherwise)
    [JsonPropertyName("oldId")] public string? OldId { get; set; } // When symbol id changed (rename / move)
    [JsonPropertyName("newId")] public string? NewId { get; set; }
    [JsonPropertyName("detail")] public string Detail { get; set; } = string.Empty; // Human summary
    [JsonPropertyName("oldSignature")] public string? OldSignature { get; set; }
    [JsonPropertyName("newSignature")] public string? NewSignature { get; set; }
    [JsonPropertyName("returnTypeChanged")] public bool? ReturnTypeChanged { get; set; }
    [JsonPropertyName("parameterDiffs")] public List<ParameterDiff>? ParameterDiffs { get; set; }
    [JsonPropertyName("breaking")] public bool? Breaking { get; set; }
}

public class ParameterDiff
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty; // Current (new) name when applicable
    [JsonPropertyName("changeType")] public string ChangeType { get; set; } = string.Empty; // Added, Removed, Renamed, TypeChanged
    [JsonPropertyName("oldName")] public string? OldName { get; set; }
    [JsonPropertyName("newName")] public string? NewName { get; set; }
    [JsonPropertyName("oldType")] public string? OldType { get; set; }
    [JsonPropertyName("newType")] public string? NewType { get; set; }
}

public class CustomizationImpact
{
    [JsonPropertyName("file")] public string File { get; set; } = string.Empty;
    [JsonPropertyName("reasons")] public List<string> Reasons { get; set; } = new();
}

public class PatchProposal
{
    [JsonPropertyName("file")] public string File { get; set; } = string.Empty;
    [JsonPropertyName("diff")] public string Diff { get; set; } = string.Empty; // unified diff placeholder
    [JsonPropertyName("rationale")] public string Rationale { get; set; } = string.Empty;
}
