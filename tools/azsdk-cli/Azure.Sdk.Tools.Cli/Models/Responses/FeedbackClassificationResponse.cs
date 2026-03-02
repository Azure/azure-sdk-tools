// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses;

/// <summary>
/// Response payload for feedback classification operations in the CustomizedCodeUpdateTool.
/// Used when classifying APIView comments or plain text feedback.
/// </summary>
public class FeedbackClassificationResponse : CommandResponse
{
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("classifications")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ItemClassificationDetails>? Classifications { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();
        
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
                sb.AppendLine("---");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(Message))
        {
            sb.AppendLine(Message);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Detailed classification result for a single feedback item.
    /// </summary>
    public class ItemClassificationDetails
    {
        /// <summary>
        /// Unique identifier of the feedback item.
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// Classification outcome for the item.
        /// </summary>
        public string Classification { get; set; } = string.Empty;

        /// <summary>
        /// Reason provided for the classification decision.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Original feedback text for the item, when available.
        /// </summary>
        public string? Text { get; set; }
    }
}
