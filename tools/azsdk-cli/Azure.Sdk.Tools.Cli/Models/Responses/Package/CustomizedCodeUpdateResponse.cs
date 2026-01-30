// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

/// <summary>
/// Response payload for CustomizedCodeUpdateTool MCP / CLI operations.
/// </summary>
public class CustomizedCodeUpdateResponse : PackageResponseBase
{
    /// <summary>
    /// Error codes for classifier to parse programmatically.
    /// These define the contract between the tool and downstream processors.
    /// </summary>
    public static class KnownErrorCodes
    {
        public const string RegenerateFailed = "RegenerateFailed";
        public const string RegenerateAfterPatchesFailed = "RegenerateAfterPatchesFailed";
        public const string BuildAfterPatchesFailed = "BuildAfterPatchesFailed";
        public const string BuildNoCustomizationsFailed = "BuildNoCustomizationsFailed";
        public const string PatchesFailed = "PatchesFailed";
        public const string NoLanguageService = "NoLanguageService";
        public const string InvalidInput = "InvalidInput";
        public const string UnexpectedError = "UnexpectedError";
    }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("errorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("classifications")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ItemClassificationDetails>? Classifications { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();
        
        // Show detailed per-item classifications FIRST if available
        if (Classifications != null && Classifications.Any())
        {
            sb.AppendLine("=== Detailed Feedback Analysis ===");
            foreach (var item in Classifications)
            {
                sb.AppendLine();
                sb.AppendLine($"## {item.ItemId}");
                
                if (!string.IsNullOrEmpty(item.Text))
                {
                    sb.AppendLine($"**Text:** {item.Text}");
                }
                
                sb.AppendLine($"**Classification:** {item.Classification}");
                sb.AppendLine($"**Reason:** {item.Reason}");
                
                if (!string.IsNullOrEmpty(item.NextAction))
                {
                    sb.AppendLine();
                    sb.AppendLine("**Next Action:**");
                    sb.AppendLine(item.NextAction);
                }
                
                sb.AppendLine("---");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(Message))
        {
            sb.AppendLine(Message);
        }
        if (!string.IsNullOrWhiteSpace(ErrorCode))
        {
            sb.AppendLine($"ErrorCode: {ErrorCode}");
        }

        return sb.ToString();
    }

    public override string ToString()
    {
        var value = Format();

        List<string> messages = [];
        if (!string.IsNullOrEmpty(ResponseError))
        {
            messages.Add("[ERROR] " + ResponseError);
        }
        foreach (var error in ResponseErrors ?? [])
        {
            messages.Add("[ERROR] " + error);
        }

        if (NextSteps?.Count > 0)
        {
            messages.Add("[NEXT STEPS]");
            foreach (var step in NextSteps)
            {
                messages.Add(step);
            }
        }

        // IMPORTANT: Prepend the Format() output (which includes detailed feedback)
        // instead of replacing it like the base class does
        if (messages.Count > 0)
        {
            if (!string.IsNullOrEmpty(value))
            {
                return value + Environment.NewLine + string.Join(Environment.NewLine, messages);
            }
            return string.Join(Environment.NewLine, messages);
        }

        return value;
    }

    public class ItemClassificationDetails
    {
        public string ItemId { get; set; } = string.Empty;
        public string Classification { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string? NextAction { get; set; }
        public string? Text { get; set; }
    }
}
