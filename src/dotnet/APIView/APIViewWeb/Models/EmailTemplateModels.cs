using System.Collections.Generic;
using System;
using System.Linq;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using Microsoft.AspNetCore.Html;

namespace APIViewWeb.Models
{
    public class NamespaceReviewRequestEmailModel
    {
        public string PackageName { get; set; } = string.Empty;
        public string TypeSpecUrl { get; set; } = string.Empty;
        public IReadOnlyList<EmailLanguageReviewModel> LanguageReviews { get; set; } = [];
        public string Notes { get; set; } = string.Empty;

        public static NamespaceReviewRequestEmailModel Create(
            string packageName,
            string typeSpecUrl,
            IEnumerable<ReviewListItemModel> languageReviews,
            string notes,
            string apiviewEndpoint)
        {
            return new NamespaceReviewRequestEmailModel
            {
                PackageName = packageName,
                TypeSpecUrl = typeSpecUrl,
                LanguageReviews = EmailLanguageReviewModel.From(languageReviews, apiviewEndpoint),
                Notes = notes ?? string.Empty,
            };
        }
    }

    public class NamespaceReviewApprovedEmailModel
    {
        public string PackageName { get; set; } = string.Empty;
        public string TypeSpecUrl { get; set; } = string.Empty;
        public IReadOnlyList<EmailLanguageReviewModel> LanguageReviews { get; set; } = [];

        public static NamespaceReviewApprovedEmailModel Create(
            string packageName,
            string typeSpecUrl,
            IEnumerable<ReviewListItemModel> languageReviews,
            string apiviewEndpoint)
        {
            return new NamespaceReviewApprovedEmailModel
            {
                PackageName = packageName,
                TypeSpecUrl = typeSpecUrl,
                LanguageReviews = EmailLanguageReviewModel.From(languageReviews, apiviewEndpoint),
            };
        }
    }

    public class EmailLanguageReviewModel
    {
        public string LanguageName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string ReviewUrl { get; set; } = string.Empty;

        public static IReadOnlyList<EmailLanguageReviewModel> From(
            IEnumerable<ReviewListItemModel> languageReviews,
            string apiviewEndpoint)
        {
            return languageReviews?.Select(review => new EmailLanguageReviewModel
            {
                LanguageName = review.Language,
                PackageName = review.PackageName,
                ReviewUrl = $"{apiviewEndpoint}/Assemblies/Review/{review.Id}",
            }).ToList() ?? [];
        }
    }

    public class ReviewerAssignedEmailModel
    {
        public string RequesterProfileUrl { get; set; } = string.Empty;
        public string RequesterUserName { get; set; } = string.Empty;
        public string ReviewUrl { get; set; } = string.Empty;
        public string ReviewName { get; set; } = string.Empty;
        public string RequestedReviewsUrl { get; set; } = string.Empty;

        public static ReviewerAssignedEmailModel Create(
            string apiviewEndpoint,
            string requesterUserName,
            string reviewId,
            string reviewName)
        {
            return new ReviewerAssignedEmailModel
            {
                RequesterProfileUrl = $"{apiviewEndpoint}/Assemblies/Profile/{requesterUserName}",
                RequesterUserName = requesterUserName,
                ReviewUrl = $"{apiviewEndpoint}/Assemblies/Review/{reviewId}",
                ReviewName = reviewName,
                RequestedReviewsUrl = $"{apiviewEndpoint}/Assemblies/RequestedReviews/",
            };
        }
    }

    public class CommentTagEmailModel
    {
        public string PosterProfileUrl { get; set; } = string.Empty;
        public string PosterUserName { get; set; } = string.Empty;
        public string ReviewUrl { get; set; } = string.Empty;
        public string ReviewName { get; set; } = string.Empty;
        public IHtmlContent CommentBodyHtml { get; set; } = HtmlString.Empty;

        public static CommentTagEmailModel Create(
            string apiviewEndpoint,
            CommentItemModel comment,
            ReviewListItemModel review,
            string reviewUrl)
        {
            return new CommentTagEmailModel
            {
                PosterProfileUrl = $"{apiviewEndpoint}/Assemblies/Profile/{comment.CreatedBy}",
                PosterUserName = comment.CreatedBy,
                ReviewUrl = reviewUrl,
                ReviewName = review.PackageName,
                CommentBodyHtml = new HtmlString(CommentMarkdownExtensions.MarkdownAsHtml(comment.CommentText)),
            };
        }
    }

    public class SubscriberCommentEmailModel
    {
        public string CommentedBy { get; set; } = string.Empty;
        public string CommenterProfileUrl { get; set; } = string.Empty;
        public string ElementUrl { get; set; } = string.Empty;
        public string ElementId { get; set; } = string.Empty;
        public bool HasElementLink { get; set; }
        public IHtmlContent CommentBodyHtml { get; set; } = HtmlString.Empty;

        public static SubscriberCommentEmailModel Create(string apiviewEndpoint, CommentItemModel comment, string elementUrl)
        {
            return new SubscriberCommentEmailModel
            {
                CommentedBy = comment.CreatedBy,
                CommenterProfileUrl = $"{apiviewEndpoint}/Assemblies/Profile/{comment.CreatedBy}",
                ElementUrl = elementUrl ?? string.Empty,
                ElementId = comment.ElementId ?? string.Empty,
                HasElementLink = !string.IsNullOrEmpty(comment.ElementId) && !string.IsNullOrEmpty(elementUrl),
                CommentBodyHtml = new HtmlString(CommentMarkdownExtensions.MarkdownAsHtml(comment.CommentText)),
            };
        }
    }

    public class NewRevisionEmailModel
    {
        public string ReviewName { get; set; } = string.Empty;
        public string RevisionUrl { get; set; } = string.Empty;
        public string RevisionName { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }

        public static NewRevisionEmailModel Create(
            string apiviewEndpoint,
            ReviewListItemModel review,
            APIRevisionListItemModel revision)
        {
            var resolvedRevisionLabel = PageModelHelpers.ResolveRevisionLabel(revision);
            var resolvedRevisionName = PageModelHelpers.ResolveRevisionLabel(
                revision,
                addAPIRevisionType: false,
                addCreatedBy: false,
                addCreatedOn: false);

            return new NewRevisionEmailModel
            {
                ReviewName = review.PackageName,
                RevisionUrl = $"{apiviewEndpoint}/Assemblies/Review/{review.Id}",
                RevisionName = string.IsNullOrWhiteSpace(resolvedRevisionName) ? resolvedRevisionLabel : resolvedRevisionName,
                CreatedBy = revision.CreatedBy,
                CreatedOn = revision.CreatedOn,
            };
        }
    }
}
