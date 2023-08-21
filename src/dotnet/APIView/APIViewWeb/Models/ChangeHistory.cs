using System;

namespace APIViewWeb.Models
{
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

    public enum RevisionChangeAction
    {
        Approved = 0,
        RevertedApproval,
        Deleted,
        Undeleted
    }

    public class ReviewChangeHistoryModel
    {
        public ReviewChangeAction ChangeAction { get; set; }
        public string User { get; set; }
        public DateTime ChangeDateTime { get; set; }
    }

    public class RevisionChangeHistoryModel
    {
        public RevisionChangeAction ChangeAction { get; set; }
        public string User { get; set; }
        public DateTime ChangeDateTime { get; set; }
    }
}
