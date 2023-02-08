using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SwaggerApiParser;

public class SwaggerSpec
{
    public string swagger { get; set; }

    public string swaggerFilePath;

    public string swaggerLink;

    public Info info { get; set; }
    public string host { get; set; }

    public string basePath { get; set; }
    
    public Security security { get; set; }
    public List<string> schemes { get; set; }
    public List<string> consumes { get; set; }
    public List<string> produces { get; set; }


    public SecurityDefinitions securityDefinitions { get; set; }
    public Dictionary<string, ApiPath> paths { get; set; }

    public Dictionary<string, Parameter> parameters { get; set; }

    public Dictionary<string, Response> responses { get; set; }

    [JsonPropertyName("x-ms-paths")] public Dictionary<string, Dictionary<string, Operation>> xMsPaths { get; set; }
    
    [JsonPropertyName("x-ms-parameterized-host")]
    public XMsParameterizedHost xMsParameterizedHost { get; set; }

    public Dictionary<string, Definition> definitions { get; set; }

    public object ResolveRefObj(string Ref)
    {
        if (Ref.Contains("parameters"))
        {
            var key = Ref.Split("/").Last();
            if (this.parameters == null)
            {
                return null;
            }
            this.parameters.TryGetValue(key, out var ret);
            return ret;
        }

        if (Ref.Contains("definitions"))
        {
            var key = Ref.Split("/").Last();
            this.definitions.TryGetValue(key, out var ret);
            return ret;
        }

        return null;
    }
}
