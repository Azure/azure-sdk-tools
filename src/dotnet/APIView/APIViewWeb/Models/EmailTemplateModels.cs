using System.Collections.Generic;
using Microsoft.AspNetCore.Html;

namespace APIViewWeb.Models
{
    public class NamespaceReviewRequestEmailModel
    {
        public string PackageName { get; set; } = string.Empty;
        public string TypeSpecUrl { get; set; } = string.Empty;
        public IReadOnlyList<EmailLanguageReviewModel> LanguageReviews { get; set; } = [];
        public string Notes { get; set; } = string.Empty;
    }

    public class NamespaceReviewApprovedEmailModel
    {
        public string PackageName { get; set; } = string.Empty;
        public string TypeSpecUrl { get; set; } = string.Empty;
        public IReadOnlyList<EmailLanguageReviewModel> LanguageReviews { get; set; } = [];
    }

    public class EmailLanguageReviewModel
    {
        public string LanguageName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string ReviewUrl { get; set; } = string.Empty;
    }

    public class ReviewerAssignedEmailModel
    {
        public string RequesterProfileUrl { get; set; } = string.Empty;
        public string RequesterUserName { get; set; } = string.Empty;
        public string ReviewUrl { get; set; } = string.Empty;
        public string ReviewName { get; set; } = string.Empty;
        public string RequestedReviewsUrl { get; set; } = string.Empty;
    }

    public class CommentTagEmailModel
    {
        public string PosterProfileUrl { get; set; } = string.Empty;
        public string PosterUserName { get; set; } = string.Empty;
        public string ReviewUrl { get; set; } = string.Empty;
        public string ReviewName { get; set; } = string.Empty;
        public IHtmlContent CommentBodyHtml { get; set; } = HtmlString.Empty;
    }

    public class SubscriberCommentEmailModel
    {
        public string CommentedBy { get; set; } = string.Empty;
        public string ElementUrl { get; set; } = string.Empty;
        public string ElementId { get; set; } = string.Empty;
        public bool HasElementLink { get; set; }
        public IHtmlContent CommentBodyHtml { get; set; } = HtmlString.Empty;
    }

    public class NewRevisionEmailModel
    {
        public string RevisionUrl { get; set; } = string.Empty;
        public string RevisionLabel { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
    }
}