using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace APIViewWeb.LeanModels
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FeedbackVote
    {
        [EnumMember(Value = "none")]
        None = 0,
        [EnumMember(Value = "up")]
        Up = 1,
        [EnumMember(Value = "down")]
        Down = 2
    }

    public class ResolveBatchConversationRequest
    {
        public List<string> CommentIds { get; set; }
        public FeedbackVote Vote { get; set; } = FeedbackVote.None;
        public string CommentReply { get; set; }
    }
}
