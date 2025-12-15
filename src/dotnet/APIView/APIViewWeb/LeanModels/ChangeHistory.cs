using System;
using System.Text.Json.Serialization;

namespace APIViewWeb.LeanModels
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AICommentChangeAction
    {
        Created = 0,
        Deleted,
        Modified
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ReviewChangeAction
    {
        Created = 0,
        Closed,
        ReOpened,
        Approved,
        ApprovalReverted,
        Deleted,
        UnDeleted
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum APIRevisionChangeAction
    {
        Created = 0,
        Approved,
        ApprovalReverted,
        Deleted,
        UnDeleted
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CommentChangeAction
    {
        Created = 0,
        Edited,
        Resolved,
        UnResolved,
        Deleted,
        UnDeleted
    }

    public abstract class ChangeHistoryModel
    {
        public string ChangedBy { get; set; }
        public DateTime? ChangedOn { get; set; }
        public string Notes { get; set; }
    }

    public class AICommentChangeHistoryModel : ChangeHistoryModel
    {
        public AICommentChangeAction ChangeAction { get; set; }
    }

    public class ReviewChangeHistoryModel : ChangeHistoryModel
    {
        public ReviewChangeAction ChangeAction { get; set; }
    }

    public class APIRevisionChangeHistoryModel : ChangeHistoryModel
    {
        public APIRevisionChangeAction ChangeAction { get; set; }
    }

    public class CommentChangeHistoryModel : ChangeHistoryModel
    {
        public CommentChangeAction ChangeAction { get; set; }
    }
}
