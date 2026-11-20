using System.Text.Json.Serialization;

namespace PipelineGenerator
{
    public record ReleaseRunResult(
        [property: JsonPropertyName("descriptorPath")] string DescriptorPath,
        [property: JsonPropertyName("definitionId")] int DefinitionId,
        [property: JsonPropertyName("runId")] int RunId,
        [property: JsonPropertyName("url")] string Url);
}
