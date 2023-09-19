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
        Undeleted
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReviewRevisionChangeAction
    {
        Created = 0,
        Approved,
        ApprovalReverted,
        Deleted,
        Undeleted
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReviewRevisionUserInteraction
    {
        Viewed = 0,
        Commented,
    }

    public abstract class ChangeHistoryModel
    {
        public string User { get; set; }
        public DateTime? ChangeDateTime { get; set; }
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

    public class ReviewRevisionChangeHistoryModel : ChangeHistoryModel
    {
        public ReviewRevisionChangeAction ChangeAction { get; set; }
    }
}
