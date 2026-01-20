using System;

namespace APIViewWeb.LeanModels;

public class ReviewMetadata
{
    public string ReviewId { get; set; }
    public string PackageName { get; set; }
    public string Language { get; set; }
    public bool IsApproved { get; set; }
    public string CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastUpdatedOn { get; set; }
    public RevisionMetadata Revision { get; set; }
}


public class RevisionMetadata
{
    public string RevisionId { get; set; }
    public string PackageVersion { get; set; }
    public bool IsApproved { get; set; }
    public int? PullRequestNo { get; set; }
    public string? PullRequestRepository { get; set; }
    public string RevisionLink { get; set; }
    public string CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
}
