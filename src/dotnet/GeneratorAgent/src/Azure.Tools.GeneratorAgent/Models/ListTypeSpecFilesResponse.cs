using System.Text.Json.Serialization;

namespace Azure.Tools.GeneratorAgent.Models
{
    /// <summary>
    /// Response for the list_typespec_files tool
    /// </summary>
    public class ListTypeSpecFilesResponse
    {
        [JsonPropertyName("files")]
        public required List<TypeSpecFileInfo> Files { get; set; }
    }
}