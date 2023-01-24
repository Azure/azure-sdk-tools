using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Constants
{
    // In theory, we should be using Octokit's EventInfoState however it seems to lack
    // the opened state...no, really.
    public class ActionConstants
    {
        public static readonly string Assigned = "assigned";
        public static readonly string Created = "created";
        public static readonly string Closed = "closed";
        public static readonly string Deleted = "deleted";
        public static readonly string Demilestoned = "demilestoned";
        public static readonly string Dismissed = "dismissed";
        public static readonly string Edited = "edited";
        public static readonly string Labeled = "labeled";
        public static readonly string Locked = "locked";
        public static readonly string Milestoned = "milestoned";
        public static readonly string Opened = "opened";
        public static readonly string Pinned = "pinned";
        public static readonly string Reopened = "reopened";
        public static readonly string ReviewRequested = "review_requested";
        public static readonly string Submitted = "submitted";
        public static readonly string Synchronize = "synchronize";
        public static readonly string Transferred = "transferred";
        public static readonly string Unassigned = "unassigned";
        public static readonly string Unlabeled = "unlabeled";
        public static readonly string Unlocked = "unlocked";
        public static readonly string Unpinned = "unpinned";
    }
}
