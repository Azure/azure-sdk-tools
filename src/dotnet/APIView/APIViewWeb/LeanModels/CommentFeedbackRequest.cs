using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIViewWeb.LeanModels
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AICommentFeedbackReason
    {
        FactuallyIncorrect,
        RenderingBug,
        AcceptedRenderingChoice,
        AcceptedSDKPattern,
        OutdatedGuideline
    }

    public class CommentFeedbackRequest
    {
        [JsonPropertyName("reasons")]
        public List<AICommentFeedbackReason> Reasons { get; set; } = new();
        
        [JsonPropertyName("comment")]
        public string Comment { get; set; } = string.Empty;
        
        [JsonPropertyName("isDelete")]
        public bool IsDelete { get; set; } = false;
    }
}
