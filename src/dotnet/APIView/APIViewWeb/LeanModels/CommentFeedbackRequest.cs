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

    public static class AICommentFeedbackReasonExtensions
    {
        private static readonly Dictionary<AICommentFeedbackReason, string> FeedbackMessages = new()
        {
            { AICommentFeedbackReason.FactuallyIncorrect, "This comment is factually incorrect." },
            { AICommentFeedbackReason.RenderingBug, "This is a rendering bug in the associated language parser. Please open an issue to correct." },
            { AICommentFeedbackReason.AcceptedRenderingChoice, "This is how things are deliberately rendered in APIView. It is not a valid comment." },
            { AICommentFeedbackReason.AcceptedSDKPattern, "This is a pattern we accept and encourage in our SDKs. DO NOT suggest otherwise." },
            { AICommentFeedbackReason.OutdatedGuideline, "This is a valid comment for the guideline listed, but this guideline itself is out-of-date. Please open an issue." }
        };

        public static string ToFeedbackMessage(this AICommentFeedbackReason reason)
        {
            return FeedbackMessages.TryGetValue(reason, out var message) ? message : reason.ToString();
        }
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
