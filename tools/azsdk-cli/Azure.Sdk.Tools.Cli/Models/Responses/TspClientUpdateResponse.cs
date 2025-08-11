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

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(Message))
        {
            sb.AppendLine(Message);
        }
        if (Session != null)
        {
            sb.AppendLine($"Session: {Session.SessionId} Status: {Session.Status} API changes: {Session.ApiChanges.Count} Impacted: {Session.ImpactedCustomizations.Count} Patches: {Session.ProposedPatches.Count}");
        }
        return ToString(sb.ToString());
    }
}

public class UpdateSessionState
{
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = Guid.NewGuid().ToString("n");
    [JsonPropertyName("specPath")] public string SpecPath { get; set; } = string.Empty;
    [JsonPropertyName("oldGeneratedPath")] public string OldGeneratedPath { get; set; } = string.Empty;
    [JsonPropertyName("newGeneratedPath")] public string NewGeneratedPath { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = "Initialized";
    [JsonPropertyName("apiChanges")] public List<ApiChange> ApiChanges { get; set; } = new();
    [JsonPropertyName("impactedCustomizations")] public List<CustomizationImpact> ImpactedCustomizations { get; set; } = new();
    [JsonPropertyName("directMergeFiles")] public List<string> DirectMergeFiles { get; set; } = new();
    [JsonPropertyName("proposedPatches")] public List<PatchProposal> ProposedPatches { get; set; } = new();
}

public class ApiChange
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = string.Empty;
    [JsonPropertyName("detail")] public string Detail { get; set; } = string.Empty;
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
