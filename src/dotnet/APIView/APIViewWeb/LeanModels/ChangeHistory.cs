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
        Subscribed,
        UnSubScribed,
        ApprovedForFirstRelease,
        RevertedFirstReleaseApproval,
        Closed,
        ReOpened,
        Deleted,
        Undeleted
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum RevisionChangeAction
    {
        Approved = 0,
        RevertedApproval,
        Deleted,
        Undeleted
    }

    public class AICommentChangeHistoryModel
    {
        public AICommentChangeAction ChangeAction { get; set; }
        public string User { get; set; }
        public DateTime ChangeDateTime { get; set; }
    }

    public class ReviewChangeHistoryModel
    {
        public ReviewChangeAction ChangeAction { get; set; }
        public string User { get; set; }
        public DateTime ChangeDateTime { get; set; }
        public string Notes { get; set; }

    }

    public class RevisionChangeHistoryModel
    {
        public RevisionChangeAction ChangeAction { get; set; }
        public string User { get; set; }
        public DateTime ChangeDateTime { get; set; }
        public string Notes { get; set; }
    }
}
