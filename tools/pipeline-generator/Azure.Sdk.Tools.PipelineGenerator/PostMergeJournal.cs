using System.Text.Json.Serialization;

namespace PipelineGenerator
{
    public record PostMergeJournal(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("runId")] int? RunId = null);
}
