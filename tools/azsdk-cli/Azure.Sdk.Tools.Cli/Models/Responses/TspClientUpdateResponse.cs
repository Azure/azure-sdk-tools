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

    [JsonPropertyName("nextStage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextStage { get; set; }

    [JsonPropertyName("needsFinalize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? NeedsFinalize { get; set; }

    [JsonPropertyName("terminal")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Terminal { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(Message))
        {
            sb.AppendLine(Message);
        }
        if (Session != null)
        {
            sb.AppendLine($"Session: {Session.SessionId} Status: {Session.Status} API changes: {Session.ApiChanges.Count} Impacted: {Session.ImpactedCustomizations.Count} Next: {NextStep}");
            if (!string.IsNullOrWhiteSpace(NextStage) || NeedsFinalize == true || Terminal == true)
            {
                sb.AppendLine($"NextStage: {NextStage ?? "<none>"} NeedsFinalize: {NeedsFinalize} Terminal: {Terminal}");
            }
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
    PatchesProposed,
    AppliedDryRun,
    Applied,
    Validated,
    Stale
}

public class UpdateSessionState
{
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = Guid.NewGuid().ToString("n");
    [JsonPropertyName("specPath")] public string SpecPath { get; set; } = string.Empty;
    [JsonPropertyName("language"), JsonIgnore] public string Language { get; set; } = string.Empty; // language key (java, csharp, etc.)
    [JsonPropertyName("oldGeneratedPath")] public string OldGeneratedPath { get; set; } = string.Empty;
    [JsonPropertyName("newGeneratedPath")] public string NewGeneratedPath { get; set; } = string.Empty;
    [JsonPropertyName("customizationRoot")] public string? CustomizationRoot { get; set; }
    [JsonPropertyName("status"), JsonIgnore] public string Status { get; set; } = "Initialized";
    [JsonPropertyName("lastStage")] public UpdateStage LastStage { get; set; } = UpdateStage.Initialized;
    [JsonPropertyName("apiChangeCount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ApiChangeCount { get; set; } = 0;
    [JsonPropertyName("apiChanges"), JsonIgnore] public List<ApiChange> ApiChanges { get; set; } = new();
    [JsonPropertyName("impactedCustomizations"), JsonIgnore] public List<CustomizationImpact> ImpactedCustomizations { get; set; } = new();
    [JsonPropertyName("proposedPatches"), JsonIgnore] public List<PatchProposal> ProposedPatches { get; set; } = new();
    [JsonPropertyName("validationErrors"), JsonIgnore] public List<string> ValidationErrors { get; set; } = new();
    [JsonPropertyName("validationSuccess")] public bool? ValidationSuccess { get; set; }
    [JsonPropertyName("validationAttemptCount"), JsonIgnore] public int ValidationAttemptCount { get; set; } = 0;
    [JsonPropertyName("requiresManualIntervention")] public bool RequiresManualIntervention { get; set; } = false;
    [JsonPropertyName("requiresFinalize")] public bool RequiresFinalize { get; set; } = false;
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
    [JsonPropertyName("diff")] public string Diff { get; set; } = string.Empty; // unified diff text
    [JsonPropertyName("rationale")] public string? Rationale { get; set; }
}
