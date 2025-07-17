using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class ServiceLabelResponse : Response
{
    [JsonPropertyName("service_label")]
    public string ServiceLabel { get; set; } = string.Empty;

    [JsonPropertyName("found")]
    public bool Found { get; set; }

    [JsonPropertyName("color_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ColorCode { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    public override string ToString()
    {
        var output = $"Service Label: {ServiceLabel}" + Environment.NewLine +
                     $"Found: {Found}" + Environment.NewLine;
        
        if (Found)
        {
            output += $"Color Code: {ColorCode}" + Environment.NewLine;
            if (!string.IsNullOrEmpty(Description))
            {
                output += $"Description: {Description}" + Environment.NewLine;
            }
        }
        
        return ToString(output);
    }
}
