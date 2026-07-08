using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

public class CheckPackageIssue
{
    public static class Codes
    {
        public const string InvalidDirectoryPath = "invalid_directory_path";
        public const string NoMatchingPath = "no_matching_path";
        public const string InsufficientOwners = "insufficient_owners";
        public const string MissingPrLabel = "missing_pr_label";
        public const string InsufficientServiceOwners = "insufficient_service_owners";
        public const string InvalidCacheSource = "invalid_cache_source";
        public const string InvalidRepo = "invalid_repo";
        public const string UnexpectedError = "unexpected_error";
    }

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("next_step")]
    public string NextStep { get; set; } = string.Empty;

    [JsonPropertyName("found_count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FoundCount { get; set; }

    [JsonPropertyName("required_count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RequiredCount { get; set; }

    [JsonPropertyName("current_values")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? CurrentValues { get; set; }
}
