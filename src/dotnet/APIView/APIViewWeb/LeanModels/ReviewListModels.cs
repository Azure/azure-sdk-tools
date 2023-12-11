using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using APIViewWeb.Helpers;
using APIViewWeb.Models;

namespace APIViewWeb.LeanModels
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum APIRevisionType
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
    public enum ApprovalStatus
    {
        Pending = 0,
        Approved
    }

    public class ReviewAssignmentModel
    {
        public string AssignedBy { get; set; }
        public string AssingedTo { get; set; }
        public DateTime AssingedOn { get; set; }
    }

    public class ReviewListModel
    {
        public int TotalNumberOfReviews { get; set; }
        public List<ReviewListItemModel> Reviews { get; set; }
    }

    public class ReviewRevisionListModel
    {
        public int TotalNumberOfReviewRevisions { get; set; }
        public List<APIRevisionListItemModel> APIRevisions { get; set; }
    }

    public class LegacyReviewModel
    {
        [JsonProperty("id")]
        public string ReviewId { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public DateTime CreationDate { get; set; }
        public List<LegacyRevisionModel> Revisions { get; set; } = new List<LegacyRevisionModel>();
        public bool RunAnalysis { get; set; }
        public bool IsClosed { get; set; }
        public HashSet<string> Subscribers { get; set; } = new HashSet<string>();
        public bool IsAutomatic { get; set; }
        public APIRevisionType FilterType { get; set; }
        public string ServiceName { get; set; }
        public string PackageDisplayName { get; set; }
        public HashSet<string> RequestedReviewers { get; set; }
        public string RequestedBy { get; set; }
        public DateTime ApprovalRequestedOn;
        public DateTime ApprovalDate;
        public bool IsApprovedForFirstRelease { get; set; }
        public string ApprovedForFirstReleaseBy { get; set; }
        public DateTime ApprovedForFirstReleaseOn { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class LegacyRevisionModel
    {
        [JsonProperty("id")]
        public string RevisionId { get; set; }
        public List<APICodeFileModel> Files { get; set; } = new List<APICodeFileModel>();
        public Dictionary<string, HashSet<int>> HeadingsOfSectionsWithDiff { get; set; } = new Dictionary<string, HashSet<int>>();
        public DateTime CreationDate { get; set; } = DateTime.Now;
        public string Name { get; set; }
        public string Author { get; set; }
        public string Label { get; set; }
        public int RevisionNumber { get; set; }
        public HashSet<string> Approvers { get; set; } = new HashSet<string>();
        public bool IsApproved { get; set; }
    }

    public class ReviewListItemModel
    {
        [JsonProperty("id")]
        public string Id { get; set; } = IdHelper.GenerateId();
        public string PackageName { get; set; }
        public string Language { get; set; }
        public HashSet<string> Subscribers { get; set; } = new HashSet<string>();
        public List<ReviewChangeHistoryModel> ChangeHistory { get; set; } = new List<ReviewChangeHistoryModel>();
        public List<ReviewAssignmentModel> AssignedReviewers { get; set; } = new List<ReviewAssignmentModel>();
        public bool IsClosed { get; set; }
        public bool IsApproved { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime LastUpdatedOn { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class APIRevisionListItemModel
    {
        [JsonProperty("id")]
        public string Id { get; set; } = IdHelper.GenerateId();
        public string ReviewId { get; set; }
        public string PackageName { get; set; }
        public string Language { get; set; }
        public List<APICodeFileModel> Files { get; set; } = new List<APICodeFileModel>();
        public string Label { get; set; }
        public List<APIRevisionChangeHistoryModel> ChangeHistory { get; set; } = new List<APIRevisionChangeHistoryModel>();
        public APIRevisionType APIRevisionType { get; set; }
        public int? PullRequestNo { get; set; }
        public Dictionary<string, HashSet<int>> HeadingsOfSectionsWithDiff { get; set; } = new Dictionary<string, HashSet<int>>();
        public bool IsApproved { get; set; }
        public HashSet<string> Approvers { get; set; } = new HashSet<string>();
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime LastUpdatedOn { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class SamplesRevisionModel
    {
        [JsonProperty("id")]
        public string Id { get; set; } = IdHelper.GenerateId();
        public string ReviewId { get; set; }
        public string FileId { get; set; } = IdHelper.GenerateId();
        public string OriginalFileId { get; set; } = IdHelper.GenerateId();
        public string OriginalFileName { get; set; } // likely to be null if uploaded via text
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Title { get; set; }
        public bool IsDeleted { get; set; }
    }
}
