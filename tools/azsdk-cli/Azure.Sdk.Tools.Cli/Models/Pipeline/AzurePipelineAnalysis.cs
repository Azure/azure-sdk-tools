using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

public class AzurePipelineAnalysis
{
    [JsonPropertyName("pipeline_build")]
    public required AzurePipelineBuild PipelineBuild { get; set; }

    /// <summary>
    /// Failed tests recovered from the build's test artifacts, one entry per artifact file. Null when none
    /// were recovered.
    /// </summary>
    [JsonPropertyName("failed_pipeline_tests")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FailedTestArtifact>? FailedPipelineTests { get; set; }

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

/// <summary>
/// One test-result artifact: the file its failures were parsed from, the platform it was published for, and
/// the failing test titles. Pass the path to azsdk_get_failed_test_case_data / azsdk_get_failed_test_run_data
/// for full details.
/// </summary>
public class FailedTestArtifact
{
    [JsonPropertyName("artifact_file_path")]
    public required string ArtifactFilePath { get; set; }

    [JsonPropertyName("platform")]
    public required string Platform { get; set; }

    [JsonPropertyName("failed_test_titles")]
    public List<string> FailedTestTitles { get; set; } = [];
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
