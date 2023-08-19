using System;

namespace APIViewWeb.Models
{
    public enum ReviewChangeAction
    {
        Approved = 0,
        RevertedApproval,
        ApprovedForFirstRelease,
        RevertedFirstReleaseApproval,
        Subscribed,
        UnSubScribed,
        Closed,
        Opened,
        Deleted,
        Undeleted
    }

    public class ReviewChangeHistory
    {
        public ReviewChangeAction ChangeAction { get; set; }
        public string User { get; set; }
        public DateTime ChangeDateTime { get; set; }
    }
}
