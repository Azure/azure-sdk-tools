using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIViewWeb.LeanModels;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VersionKind
{
    Stable,
    Preview,
    RollingPrerelease,
    PullRequest
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PullRequestStatus
{
    Open,
    Merged,
    Closed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum APIVersionChangeAction
{
    Created,
    Approved,
    ApprovalReverted,
    Deleted,
    UnDeleted,
    Promoted,
    RetentionSet,
    RetentionUnset
}

public class APIVersionChangeHistoryModel : ChangeHistoryModel
{
    public APIVersionChangeAction ChangeAction { get; set; }
}

public class APIVersionModel : BaseListitemModel
{
    public string ReviewId { get; set; }
    //Normalized version string (e.g. "12.2.0", "12.3.0-beta.1", "12.3.0-alpha", "PR#1234").
    public string VersionIdentifier { get; set; }
    //  Latest raw package version from the most recent revision.
    public string LatestPackageVersion { get; set; }
    public VersionKind Kind { get; set; }

    // PR specific 
    public int? PullRequestNumber { get; set; }
    public string SourceBranch { get; set; }
    public PullRequestStatus? PrStatus { get; set; }

    // Approval 
    public bool IsApproved { get; set; }
    public HashSet<string> Approvers { get; set; } = [];
    public DateTime? ApprovalDate { get; set; }
    /// null = directly approved by a human via ReviewSubmission
    public string ApprovalInheritedFromVersionId { get; set; }

    // Release tracking
    public bool IsReleased { get; set; }
    public DateTime? ReleasedOn { get; set; }

    //  Review requests
    public List<string> ReviewRequestIds { get; set; } = [];
    public bool IsReviewedByCopilot { get; set; }

    public string CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastUpdated { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? RetainUntil { get; set; }
    public List<APIVersionChangeHistoryModel> ChangeHistory { get; set; } = [];
}
