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

    public class BatchConversationRequest
    {
        [JsonPropertyName("commentIds")]
        public List<string> CommentIds { get; set; }
        
        [JsonPropertyName("vote")]
        public FeedbackVote Vote { get; set; } = FeedbackVote.None;
        
        [JsonPropertyName("commentReply")]
        public string CommentReply { get; set; }
        
        [JsonPropertyName("disposition")]
        public ConversationDisposition? Disposition { get; set; }
        
        [JsonPropertyName("severity")]
        public CommentSeverity? Severity { get; set; }
        
        [JsonPropertyName("feedback")]
        public CommentFeedbackRequest Feedback { get; set; }
    }
}
