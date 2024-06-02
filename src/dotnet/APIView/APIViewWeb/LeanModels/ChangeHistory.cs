using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace APIViewWeb.LeanModels
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AICommentChangeAction
    {
        Created = 0,
        Deleted,
        Modified
    }

    [JsonConverter(typeof(StringEnumConverter))]
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

    [JsonConverter(typeof(StringEnumConverter))]
    public enum APIRevisionChangeAction
    {
        Created = 0,
        Approved,
        ApprovalReverted,
        Deleted,
        UnDeleted
    }

    [JsonConverter(typeof(StringEnumConverter))]
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
