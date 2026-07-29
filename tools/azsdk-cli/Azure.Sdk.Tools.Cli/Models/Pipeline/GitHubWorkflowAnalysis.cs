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
/// Points at the GitHub commit an analysis runs against: the repository that owns it, the commit itself, and
/// the pull request it belongs to when there is one. Resolved either from a DevOps build's source version or
/// directly from a pull request; resolution returns null rather than a ref with no commit, so a ref always
/// names one. <see cref="PullRequestNumber"/> is null for commits not associated with a pull request.
/// </summary>
public record GitHubCommitRef(string Owner, string Repo, string HeadSha, int? PullRequestNumber)
{
    public string HeadSha { get; init; } = string.IsNullOrEmpty(HeadSha)
        ? throw new ArgumentException("A GitHub commit ref must name a commit.", nameof(HeadSha))
        : HeadSha;
}
