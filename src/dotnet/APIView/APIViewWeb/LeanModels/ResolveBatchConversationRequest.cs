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

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ConversationDisposition
    {
        [EnumMember(Value = "keepOpen")]
        KeepOpen, //default
        [EnumMember(Value = "resolve")]
        Resolve,
        [EnumMember(Value = "delete")]
        Delete
    }

    public class ResolveBatchConversationRequest
    {
        public List<string> CommentIds { get; set; }
        public FeedbackVote Vote { get; set; } = FeedbackVote.None;
        public string CommentReply { get; set; }
        public ConversationDisposition? Disposition { get; set; }
        public CommentSeverity? Severity { get; set; }
    }
}
