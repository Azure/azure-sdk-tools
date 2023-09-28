using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace APIViewWeb.LeanModels
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RevisionType
    {
        Manual = 0,
        Automatic,
        PullRequest,
        All
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReviewState
    {
        Open = 0,
        Closed
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReviewRevisionStatus
    {
        Pending = 0,
        Approved
    }

    public class ReviewRequestModel
    {
        public string Requester { get; set; }
        public DateTime RequestDateTime { get; set; }
    }

    public class ReviewListModel
    {
        public int TotalNumberOfReviews { get; set; }
        public List<ReviewListItemModel> Reviews { get; set; }
    }

    public class ReviewRevisionListModel
    {
        public int TotalNumberOfReviewRevisions { get; set; }
        public List<ReviewRevisionListItemModel> ReviewRevisions { get; set; }
    }

    public class ReviewListItemModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string PackageName { get; set; }
        public string PackageDisplayName { get; set; }
        public string ServiceName { get; set; }
        public string Language { get; set; }
        public HashSet<string> ReviewRevisions { get; set; } = new HashSet<string>();
        public HashSet<string> Subscribers { get; set; } = new HashSet<string>();
        public List<ReviewChangeHistoryModel> ChangeHistory { get; set; } = new List<ReviewChangeHistoryModel>();
        public ReviewState State { get; set; }
        public ReviewRevisionStatus Status { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class ReviewRevisionListItemModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string ReviewId { get; set; }
        public string PackageName { get; set; }
        public string Language { get; set; }
        public List<CodeFileModel> Files { get; set; } = new List<CodeFileModel>();
        public string Label { get; set; }
        public List<ReviewRevisionChangeHistoryModel> ChangeHistory { get; set; } = new List<ReviewRevisionChangeHistoryModel>();
        public Dictionary<string, HashSet<ReviewRevisionUserInteraction>> UserInteractions { get; set; } = new Dictionary<string, HashSet<ReviewRevisionUserInteraction>>();
        public RevisionType ReviewRevisionType { get; set; }
        public Dictionary<string, HashSet<int>> HeadingsOfSectionsWithDiff { get; set; } = new Dictionary<string, HashSet<int>>();
        public Dictionary<string, ReviewRequestModel> AssignedReviewers { get; set; } = new Dictionary<string, ReviewRequestModel>();
        public ReviewRevisionStatus Status { get; set; }
        public bool IsDeleted { get; set; }
    }
}
