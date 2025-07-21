using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models
{
    /// <summary>
    /// Represents the result of a file update operation
    /// </summary>
    public class FileUpdateResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("lineNumber")]
        public int LineNumber { get; set; }

        [JsonPropertyName("textInserted")]
        public string TextInserted { get; set; } = string.Empty;

        [JsonPropertyName("totalLines")]
        public int TotalLines { get; set; }

        [JsonPropertyName("commitSha")]
        public string? CommitSha { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        public override string ToString()
        {
            if (!Success)
            {
                return $"File update failed: {Error ?? Message}";
            }

            return $"Successfully updated {FileName} at line {LineNumber}. Total lines: {TotalLines}. Commit SHA: {CommitSha}";
        }
    }
}
