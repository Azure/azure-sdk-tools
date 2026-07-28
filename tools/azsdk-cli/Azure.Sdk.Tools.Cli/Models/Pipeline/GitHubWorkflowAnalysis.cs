using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

/// <summary>
/// A single failed GitHub Actions workflow run along with its logs and jobs. Logs and jobs are fetched
/// best-effort, so either may be missing while the rest of the run detail is still reported.
/// </summary>
public class GitHubWorkflowRunAnalysis
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>Error lines, with surrounding context, extracted from the run's logs.</summary>
    [JsonPropertyName("logs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<LogEntry> Logs { get; set; } = [];

    /// <summary>The run's jobs, each summarized as "name: conclusion".</summary>
    [JsonPropertyName("jobs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<string> Jobs { get; set; } = [];

    /// <summary>Non-fatal errors encountered while reading this run's logs or jobs.</summary>
    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Errors { get; set; }
}

/// <summary>
/// The GitHub repository, commit, and pull request a DevOps build ran against. Only resolvable for pipelines
/// whose source is a GitHub repository; <see cref="HeadSha"/> is null when the build reports no source version,
/// and <see cref="PullRequestNumber"/> is null for builds that were not triggered by a pull request.
/// </summary>
public record BuildGitHubSource(string Owner, string Repo, string? HeadSha, int? PullRequestNumber);
