using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Constants
{
    /// <summary>
    /// Action constants for github actions. These match the activity types for events defined in the
    /// github docs https://docs.github.com/en/actions/using-workflows/events-that-trigger-workflows.
    /// Some activities belong to multiple events, for example issues and pull_requests both have an
    /// opened event.
    /// </summary>
    public class ActionConstants
    {
        public const string Assigned = "assigned";
        public const string Created = "created";
        public const string Closed = "closed";
        public const string Deleted = "deleted";
        public const string Demilestoned = "demilestoned";
        public const string Dismissed = "dismissed";
        public const string Edited = "edited";
        public const string Labeled = "labeled";
        public const string Locked = "locked";
        public const string Milestoned = "milestoned";
        public const string Opened = "opened";
        public const string Pinned = "pinned";
        public const string Reopened = "reopened";
        public const string ReviewRequested = "review_requested";
        public const string Submitted = "submitted";
        public const string Synchronize = "synchronize";
        public const string Transferred = "transferred";
        public const string Unassigned = "unassigned";
        public const string Unlabeled = "unlabeled";
        public const string Unlocked = "unlocked";
        public const string Unpinned = "unpinned";
    }
}
