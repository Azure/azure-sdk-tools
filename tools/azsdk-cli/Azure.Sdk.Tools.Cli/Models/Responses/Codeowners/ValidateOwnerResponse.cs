// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

/// <summary>
/// Structured result for <c>validate-owner</c>: whether an alias or team can own code, and what has
/// to change when it cannot.
/// </summary>
public class ValidateOwnerResponse : CommandResponse
{
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    /// <summary>Individuals the owner resolves to. A single alias resolves to itself.</summary>
    [JsonPropertyName("members")]
    public IReadOnlyList<string> Members { get; set; } = [];

    [JsonPropertyName("violation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LintViolation? Violation { get; set; }

    /// <summary>
    /// An invalid owner exits non-zero so the command is usable as a gate, not only as a report.
    /// </summary>
    [JsonPropertyName("response_error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public override string? ResponseError
    {
        get => Valid ? base.ResponseError : $"{Owner} is not a valid code owner: {Violation?.Description}";
        set => base.ResponseError = value;
    }

    protected override string Format()
    {
        if (Valid)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Owner} is a valid code owner.");

            if (Members.Count > 1)
            {
                sb.AppendLine($"Expands to {Members.Count} member(s): {string.Join(", ", Members)}");
            }

            return sb.ToString().TrimEnd();
        }

        return $"{Owner} is not a valid code owner.{Environment.NewLine}" +
               $"  [{Violation?.RuleId}] {Violation?.Description}{Environment.NewLine}" +
               $"  {Violation?.Detail}";
    }
}
