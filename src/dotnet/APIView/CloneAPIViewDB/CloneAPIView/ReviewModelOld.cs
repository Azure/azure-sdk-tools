using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace CloneAPIViewDB
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
    public enum CommentType
    {
        APIRevision = 0,
        SampleRevision
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReviewChangeAction
    {
        Created,
        Closed,
        ReOpened,
        Approved,
        ApprovalReverted,
        Deleted,
        Undeleted
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum APIRevisionChangeAction
    {
        Created = 0,
        Approved,
        ApprovalReverted,
        Deleted,
        Undeleted
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CommentChangeAction
    {
        Created = 0,
        Edited,
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
        public string ChangedBy { get; set; }
        public DateTime? ChangedOn { get; set; }
        public string Notes { get; set; }

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

    public class ReviewAssignmentModel
    {
        public string AssignedBy { get; set; }
        public string AssignedTo { get; set; }
        public DateTime AssignedOn { get; set; }
    }

    public class ReviewModelOld
    {
        [JsonProperty("id")]
        public string ReviewId { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public DateTime CreationDate { get; set; }
        public List<RevisionModelOld> Revisions { get; set; } = new List<RevisionModelOld>();
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
        public int _ts { get; set; }
    }

    public class RevisionModelOld 
    {
        [JsonProperty("id")]
        public string RevisionId { get; set; }
        public List<CodeFileModel> Files { get; set; } = new List<CodeFileModel>();
        public Dictionary<string, HashSet<int>> HeadingsOfSectionsWithDiff { get; set; } = new Dictionary<string, HashSet<int>>();
        public DateTime CreationDate { get; set; } = DateTime.Now;
        public string Name { get; set; }
        public string Author { get; set; }
        public string Label { get; set; }
        public int RevisionNumber { get; set; }
        public HashSet<string> Approvers { get; set; } = new HashSet<string>();
        public bool IsApproved { get; set; }
    }

    public class APICodeFileModel
    {
        public string FileId { get; set; }
        public string Name { get; set; }
        public string Language { get; set; }
        public string VersionString { get; set; }
        public string LanguageVariant { get; set; }
        public bool HasOriginal { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;
        public bool RunAnalysis { get; set; }
        public string PackageName { get; set; }
        public string FileName { get; set; }
        public string PackageVersion { get; set; }
    }

    public class CodeFileModel
    {
        public string ReviewFileId { get; set; }
        public string Name { get; set; }
        public string Language { get;  set; }
        public string VersionString { get; set; }
        public string LanguageVariant { get; set; }
        public bool HasOriginal { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;
        public bool RunAnalysis { get; set; }
        public string PackageName { get; set; }
        public string FileName { get; set; }
        public string PackageVersion { get; set; }
    }


    public class CommentModelOld
    {
        [JsonProperty("id")]
        public string CommentId { get; set; }
        public string ReviewId { get; set; }
        public string RevisionId { get; set; }
        public string ElementId { get; set; }
        public string SectionClass { get; set; }
        public string GroupNo { get; set; }
        public string Comment { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Username { get; set; }
        public bool IsResolve { get; set; }
        public DateTime? EditedTimeStamp { get; set; }
        public List<string> Upvotes { get; set; } = new List<string>();
        public HashSet<string> TaggedUsers { get; set; } = new HashSet<string>();
        public bool IsUsageSampleComment { get; set; } = false;
        public bool ResolutionLocked { get; set; } = false;
    }

    public class PullRequestModelOld
    {
        [JsonProperty("id")]
        public string PullRequestId { get; set; }
        public int PullRequestNumber { get; set; }
        public List<string> Commits { get; set; } = new List<string>();
        public string RepoName { get; set; }
        public string FilePath { get; set; }
        public bool IsOpen { get; set; } = true;
        public string ReviewId { get; set; }
        public string Author { get; set; }
        public string PackageName { get; set; }
        public string Language { get; set; }
        public string Assignee { get; set; }
    }

    public class UsageSampleModel
    {
        [JsonProperty("id")]
        public string SampleId { get; set; }
        public string ReviewId { get; set; }
        public List<UsageSampleRevisionModel> Revisions { get; set; }
    }

    public class UsageSampleRevisionModel
    {
        public string FileId { get; set; }
        public string OriginalFileId { get; set; }
        public string OriginalFileName { get; set; } // likely to be null if uploaded via text
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.Now;
        public string RevisionTitle { get; set; }
        public int RevisionNumber { get; set; }
        public bool RevisionIsDeleted { get; set; } = false;
    }

    public class SampleRevisionModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string ReviewId { get; set; }
        public string PackageName { get; set; }
        public string Language { get; set; }
        public string FileId { get; set; }
        public string OriginalFileId { get; set; }
        public string OriginalFileName { get; set; } // likely to be null if uploaded via text
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Title { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class ReviewModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
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

    public class APIRevisionModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string ReviewId { get; set; }
        public string PackageName { get; set; }
        public string Language { get; set; }
        public List<APICodeFileModel> Files { get; set; } = new List<APICodeFileModel>();
        public string Label { get; set; }
        public List<APIRevisionChangeHistoryModel> ChangeHistory { get; set; } = new List<APIRevisionChangeHistoryModel>();
        public APIRevisionType APIRevisionType { get; set; }
        public Dictionary<string, HashSet<int>> HeadingsOfSectionsWithDiff { get; set; } = new Dictionary<string, HashSet<int>>();
        public bool IsApproved { get; set; }
        public HashSet<string> Approvers { get; set; } = new HashSet<string>();
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime LastUpdatedOn { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class CommentModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string ReviewId { get; set; }
        public string APIRevisionId { get; set; }
        public string ElementId { get; set; }
        public string SectionClass { get; set; }
        public string CommentText { get; set; }
        public List<CommentChangeHistoryModel> ChangeHistory { get; set; } = new List<CommentChangeHistoryModel>();
        public bool IsResolved { get; set; }
        public List<string> Upvotes { get; set; } = new List<string>();
        public HashSet<string> TaggedUsers { get; set; } = new HashSet<string>();
        public CommentType CommentType { get; set; }
        public bool ResolutionLocked { get; set; } = false;
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? LastEditedOn { get; set; }
        public bool IsDeleted { get; set; }
    }
    public class PullRequestModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string ReviewId { get; set; }
        public string APIRevisionId { get; set; }
        public int PullRequestNumber { get; set; }
        public List<string> Commits { get; set; } = new List<string>();
        public string RepoName { get; set; }
        public string FilePath { get; set; }
        public bool IsOpen { get; set; }
        public string CreatedBy { get; set; }
        public string PackageName { get; set; }
        public string Language { get; set; }
        public string Assignee { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class MappingModel
    {
        [JsonProperty("id")]
        public string ReviewNewId { get; set; }
        public HashSet<string> ReviewOldIds { get; set; }
    }
}
