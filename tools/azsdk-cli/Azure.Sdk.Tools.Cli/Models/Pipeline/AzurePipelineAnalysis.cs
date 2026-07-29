using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

public class AzurePipelineAnalysis
{
    [JsonPropertyName("pipeline_build")]
    public required AzurePipelineBuild PipelineBuild { get; set; }

    /// <summary>
    /// Failed tests recovered from the build's test artifacts, keyed by the platform the artifact was
    /// published for (for example "Ubuntu2404_NET80_PackageRef_Debug"). Null when no failed tests were
    /// recovered.
    /// </summary>
    [JsonPropertyName("failed_pipeline_tests")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, List<string>>? FailedPipelineTests { get; set; }

    /// <summary>
    /// Task-level failures recovered from the build's logs. Null when no failing tasks were found.
    /// </summary>
    [JsonPropertyName("failed_pipeline_tasks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LogAnalysisResponse? FailedPipelineTasks { get; set; }

    /// <summary>
    /// Non-fatal errors encountered while analyzing this specific build (for example a log or test-artifact
    /// read that failed). Kept per-build so one build's failure does not hide the analysis of the others.
    /// </summary>
    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Errors { get; set; }
}

public record AzurePipelineBuild(
    [property: JsonPropertyName("build_id")] int BuildId,
    [property: JsonPropertyName("project")] string? Project,
    [property: JsonPropertyName("pipeline_url")] string? PipelineUrl,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("result")] string? Result)
{
    public const string StatusUnavailable = "Not available";

    /// <summary>
    /// True when the build has a known run status that is not terminal ("completed"). While in this
    /// state failure logs and test artifacts may not be published yet, so an empty analysis does not
    /// necessarily mean the build is healthy. A missing/unavailable status is treated as not in progress.
    /// </summary>
    [JsonIgnore]
    public bool IsInProgress =>
        !string.IsNullOrEmpty(Status)
        && !string.Equals(Status, "completed", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(Status, StatusUnavailable, StringComparison.OrdinalIgnoreCase);
}
