using System.Text.Json.Serialization;

namespace SwaggerApiParser;

public class Parameter
{
    public string name { get; set; }
    public bool required { get; set; }
    public string description { get; set; }
    
    public string type { get; set; }

    public BaseSchema schema { get; set; }

    [JsonPropertyName("$ref")] public string Ref { get; set; }

    [JsonPropertyName("in")] public string? In { get; set; }

    public bool IsRefObject()
    {
        return this.Ref != null;
    }
}
