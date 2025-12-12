using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

// for V1 prototype only
public class SetupRequirements
{
    [JsonPropertyName("categories")]
    public Dictionary<string, List<Requirement>> categories { get; set; }

    public class Requirement
    {
        [JsonPropertyName("requirement")]
        public string requirement { get; set; }

        [JsonPropertyName("check")]
        public string[] check { get; set; }
        
        [JsonPropertyName("instructions")]
        public List<string> instructions { get; set; }

        [JsonPropertyName("reason")]
        public string? reason { get; set; }
    }
}
