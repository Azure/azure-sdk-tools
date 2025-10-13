// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;
using System.Text;
namespace Azure.Sdk.Tools.Cli.Models;

public class VerifySetupResponse : CommandResponse
{
    [JsonPropertyName("allRequirementsSatisfied")]
    public bool? AllRequirementsSatisfied { get; set; }

    [JsonPropertyName("results")]
    public List<RequirementCheckResult>? Results { get; set; } // all checks with details

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"AllRequirementsSatisfied: {AllRequirementsSatisfied}");
        sb.AppendLine("Results:");

        if (Results != null)
        {
            foreach (var result in Results)
            {
                sb.AppendLine($"  - Requirement: {result.Requirement}");
                sb.AppendLine($"    Version: {result.Version}");
                sb.AppendLine($"    Instructions: {result.Instructions}");
            }
        }
        else
        {
            sb.AppendLine("  None");
        }
        return sb.ToString();
    }
}

public class RequirementCheckResult
{
    public string Requirement { get; set; }
    public string Version { get; set; }
    public string Instructions { get; set; }
}
