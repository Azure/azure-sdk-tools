using System.Collections.Generic;
using System;
using System.Text.Json.Serialization;
using APIViewWeb.Helpers;
using APIViewWeb.Models;
using System.Linq;

namespace APIViewWeb.LeanModels
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum APIRevisionType
    {
        Manual = 0,
        Automatic,
        PullRequest,
        All
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ReviewState
    {
        Open = 0,
        Closed
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ApprovalStatus
    {
        Pending = 0,
        Approved
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NamespaceReviewStatus
    {
        NotStarted = 0,
        Pending = 1,
        Approved = 2
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
        [JsonPropertyName("id")]
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
        [JsonPropertyName("id")]
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

    public class BaseListitemModel 
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = IdHelper.GenerateId();
        public string PackageName { get; set; }
        public string Language { get; set; }
    }

    public class ReviewListItemModel : BaseListitemModel
    {
        public HashSet<string> Subscribers { get; set; } = new HashSet<string>();
        public List<ReviewChangeHistoryModel> ChangeHistory { get; set; } = new List<ReviewChangeHistoryModel>();
        public List<ReviewAssignmentModel> AssignedReviewers { get; set; } = new List<ReviewAssignmentModel>();
        public bool IsClosed { get; set; }
        public bool IsApproved { get; set; } // TODO: Deprecate in the future - redundant with NamespaceReviewStatus
        public PackageType? PackageType { get; set; } // Nullable - null means not yet classified
        public NamespaceReviewStatus NamespaceReviewStatus { get; set; } = NamespaceReviewStatus.NotStarted;
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime LastUpdatedOn { get; set; }
        public bool IsDeleted { get; set; }
        public string ReviewGroupId { get; set; }
        public string NamespaceApprovalRequestedBy { get; set; }
        public DateTime? NamespaceApprovalRequestedOn { get; set; }
    }

    public class APIRevisionListItemModel : BaseListitemModel
    {
        public string ReviewId { get; set; }
        public List<APICodeFileModel> Files { get; set; } = new List<APICodeFileModel>();
        public string Label { get; set; }
        [JsonPropertyName("resolvedLabel")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ResolvedLabel
        {
            get => PageModelHelpers.ResolveRevisionLabel(this, addAPIRevisionType: false, addCreatedBy: false, addCreatedOn: false);
        }
        [JsonPropertyName("packageVersion")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string PackageVersion
        {
            get => this.Files.First().PackageVersion;
        }
        public List<APIRevisionChangeHistoryModel> ChangeHistory { get; set; } = new List<APIRevisionChangeHistoryModel>();
        public APIRevisionType APIRevisionType { get; set; }
        public int? PullRequestNo { get; set; }
        public Dictionary<string, HashSet<int>> HeadingsOfSectionsWithDiff { get; set; } = new Dictionary<string, HashSet<int>>();
        public List<ReviewAssignmentModel> AssignedReviewers { get; set; } = new List<ReviewAssignmentModel>();
        public bool IsApproved { get; set; }
        public bool HasAutoGeneratedComments { get; set; }
        public bool CopilotReviewInProgress { get; set; }
        public string CopilotReviewJobId { get; set; }
        public HashSet<string> Approvers { get; set; } = new HashSet<string>();
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime LastUpdatedOn { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsReleased { get; set; }
        public DateTime ReleasedOn { get; set; }
        private string _sourceBranch;
        public string SourceBranch 
        { 
            get 
            {
                if (string.IsNullOrEmpty(_sourceBranch) && !string.IsNullOrEmpty(Label))
                {
                    const string prefix = "Source Branch:";
                    if (Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string extracted = Label.Substring(prefix.Length).Trim();
                        return string.IsNullOrWhiteSpace(extracted) ? null : extracted;
                    }
                }
                return _sourceBranch;
            }
            set => _sourceBranch = value;
        }
    }


    public class SamplesRevisionModel : BaseListitemModel
    {
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
