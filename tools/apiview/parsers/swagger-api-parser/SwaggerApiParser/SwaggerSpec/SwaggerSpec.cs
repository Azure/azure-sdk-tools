using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwaggerApiParser;

public class SwaggerSpec
{
    public string swagger { get; set; }

    public string host { get; set; }

    public List<string> schemes { get; set; }
    public List<string> consumes{ get; set; }
    public List<string> produces{ get; set; }
    public Dictionary<string, Dictionary<string, Operation>> paths { get; set; }
    public Info info { get; set; }

    public Dictionary<string, Parameter> parameters { get; set; }

    [JsonPropertyName("x-ms-paths")] public Dictionary<string, Dictionary<string, Operation>> xMsPaths { get; set; }

    public Dictionary<string, Definition > definitions { get; set; }
}
