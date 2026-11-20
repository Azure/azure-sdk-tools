using System.Text.Json.Serialization;

namespace PipelineGenerator
{
    public record PipelineGenerationResult(
        [property: JsonPropertyName("descriptorPath")] string DescriptorPath,
        [property: JsonPropertyName("definitionId")] int DefinitionId,
        [property: JsonPropertyName("convention")] string Convention,
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("operation")] string Operation);
}
