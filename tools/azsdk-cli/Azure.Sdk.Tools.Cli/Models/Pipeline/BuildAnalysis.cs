using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

public class BuildAnalysis
{
    [JsonPropertyName("build")]
    public required ResolvedBuild Build { get; set; }

    [JsonPropertyName("failed_build_tests")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<FailedTestRunResponse> FailedBuildTests { get; set; } = [];

    [JsonPropertyName("failed_build_tasks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public LogAnalysisResponse FailedBuildTasks { get; set; } = new LogAnalysisResponse();

    /// <summary>
    /// Non-fatal errors encountered while analyzing this specific build (for example a log or test-artifact
    /// read that failed). Kept per-build so one build's failure does not hide the analysis of the others.
    /// </summary>
    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Errors { get; set; }
}

public record ResolvedBuild(
    [property: JsonPropertyName("build_id")] int BuildId,
    [property: JsonPropertyName("project")] string? Project,
    [property: JsonPropertyName("pipeline_url")] string? PipelineUrl,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("result")] string? Result)
{
    /// <summary>Sentinel used when the run status could not be read from DevOps.</summary>
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
