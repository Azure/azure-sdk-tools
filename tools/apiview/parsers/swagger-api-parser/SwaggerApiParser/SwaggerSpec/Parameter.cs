using System.Text.Json.Serialization;

namespace SwaggerApiParser;

public class Parameter : BaseSchema
{
    public string name { get; set; }
    public bool required { get; set; }
    public string description { get; set; }

    [JsonPropertyName("in")] public string? In { get; set; }
}
