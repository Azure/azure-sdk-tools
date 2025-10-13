// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Response payload for TspClientUpdateTool MCP / CLI operations.
/// </summary>
public class TspClientUpdateResponse : CommandResponse
{
    [JsonPropertyName("session")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ClientUpdateSessionState? Session { get; set; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("errorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(Message))
        {
            sb.AppendLine(Message);
        }
        if (Session != null)
        {
            sb.AppendLine($"Session: {Session.SessionId} Stage: {Session.LastStage} Manual?: {Session.RequiresManualIntervention}");
        }
        if (!string.IsNullOrWhiteSpace(ErrorCode))
        {
            sb.AppendLine($"ErrorCode: {ErrorCode}");
        }
        return ToString(sb);
    }
}

public enum UpdateStage
{
    /// <summary>
    /// Session object created; no work performed yet.
    /// </summary>
    Initialized,
    /// <summary>
    /// TypeSpec (or language) client re-generated into <see cref="ClientUpdateSessionState.NewGeneratedPath"/>.
    /// </summary>
    Regenerated,
    /// <summary>
    /// A diff between old and new generations has been produced and API changes collected.
    /// </summary>
    Diffed,
    /// <summary>
    /// Customization files analyzed and mapped to impacted API changes.
    /// </summary>
    Mapped,
    /// <summary>
    /// Patch proposals created for impacted customizations (but not yet applied).
    /// </summary>
    PatchesProposed,
    /// <summary>
    /// Proposed patches applied to the customization sources.
    /// </summary>
    Applied,
    /// <summary>
    /// Post-apply validation (build / tests / lint) executed and results captured.
    /// </summary>
    Validated,

    /// <summary>
    /// Update failed due to an unknown error.
    /// </summary>
    Failed
}

public class ClientUpdateSessionState
{
    /// <summary>
    /// Stable id to correlate multi-step invocations.
    /// </summary>
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = Guid.NewGuid().ToString("n");
    /// <summary>
    /// Path to the source spec (.tsp) used for regeneration.
    /// </summary>
    [JsonPropertyName("specPath")] public string SpecPath { get; set; } = string.Empty;
    /// <summary>
    /// Path containing (or destined to contain) the latest regenerated output; also used for validation.
    /// </summary>
    [JsonPropertyName("newGeneratedPath")] public string NewGeneratedPath { get; set; } = string.Empty;
    /// <summary>
    /// Root folder of user customizations (may be null when none exist).
    /// </summary>
    [JsonPropertyName("customizationRoot")] public string? CustomizationRoot { get; set; }
    /// <summary>
    /// Latest completed pipeline stage for progress / resumability.
    /// </summary>
    [JsonPropertyName("lastStage")] public UpdateStage LastStage { get; set; } = UpdateStage.Initialized;
    /// <summary>
    /// True when automation halted and requires user action.
    /// </summary>
    [JsonPropertyName("requiresManualIntervention")] public bool RequiresManualIntervention { get; set; } = false;
}
