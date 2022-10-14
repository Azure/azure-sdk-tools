// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Models;
using Markdig;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Common;

namespace APIViewWeb.Repositories
{
    public class NotificationManager
    {
        private readonly string _endpoint;
        private readonly string _sendGridKey;
        private readonly CosmosReviewRepository _reviewRepository;
        private readonly CosmosUserProfileRepository _userProfileRepository;

        private const string ENDPOINT_SETTING = "Endpoint";
        private const string SENDGRID_KEY_SETTING = "SendGrid:Key";
        private const string FROM_ADDRESS = "apiview-noreply@microsoft.com";
        private const string REPLY_TO_HEADER = "In-Reply-To";
        private const string REFERENCES_HEADER = "References";

        public NotificationManager(IConfiguration configuration, CosmosReviewRepository reviewRepository,
            CosmosUserProfileRepository userProfileRepository)
        {
            _sendGridKey = configuration[SENDGRID_KEY_SETTING];
            _endpoint = configuration.GetValue<string>(ENDPOINT_SETTING);
            _reviewRepository = reviewRepository;
            _userProfileRepository = userProfileRepository;
        }

        public async Task NotifySubscribersOnComment(ClaimsPrincipal user, CommentModel comment)
        {
            ReviewModel review = await _reviewRepository.GetReviewAsync(comment.ReviewId);
            await SendEmailsAsync(review, user, GetPlainTextContent(comment), GetHtmlContent(comment, review));
        }
        
        public async Task NotifyUserOnCommentTag(string username, CommentModel comment)
        {
            ReviewModel review = await _reviewRepository.GetReviewAsync(comment.ReviewId);
            UserProfileModel user = await _userProfileRepository.tryGetUserProfileAsync(username);
            await SendUserEmailsAsync(review, user, GetCommentTagPlainTextContent(comment), GetCommentTagHtmlContent(comment, review));
        }
        
        public async Task NotifyApproversOfReview(ClaimsPrincipal user, string reviewId, HashSet<String> reviewers)
        {
            UserProfileModel userProfile = await _userProfileRepository.tryGetUserProfileAsync(user.GetGitHubLogin());
            ReviewModel review = await _reviewRepository.GetReviewAsync(reviewId);
            foreach (string reviewer in reviewers)
            {
                UserProfileModel reviewerProfile = await _userProfileRepository.tryGetUserProfileAsync(reviewer);
                await SendUserEmailsAsync(review, reviewerProfile, 
                    GetApproverReviewContentHeading(userProfile,false), 
                    GetApproverReviewHtmlContent(userProfile, review));
            }
        }
        
        private string GetApproverReviewHtmlContent(UserProfileModel user, ReviewModel review)
        {
            var reviewName = review.Name;
            var reviewLink = new Uri($"{_endpoint}/Assemblies/Review/{review.ReviewId}");
            var poster = user.UserName;
            var userLink = new Uri($"{_endpoint}/Assemblies/Profile/{poster}");
            var requestsLink = new Uri($"{_endpoint}/Assemblies/RequestedReviews/");
            var sb = new StringBuilder();
            sb.Append($"<a href='{userLink.ToString()}'>{poster}</a>");
            sb.Append($" requested you to review <a href='{reviewLink.ToString()}'><b>{reviewName}</b></a>");
            sb.Append("<br>");
            sb.Append($"You can review all your pending APIViews <a href='{requestsLink.ToString()}'><b>here</b></a>");
            return sb.ToString();
        }

        private string GetCommentTagHtmlContent(CommentModel comment, ReviewModel review)
        {
            var reviewName = review.Name;
            var reviewLink = new Uri($"{_endpoint}/Assemblies/Review/{review.ReviewId}#{Uri.EscapeUriString(comment.ElementId)}");
            var commentText = comment.Comment;
            var poster = comment.Username;
            var userLink = new Uri($"{_endpoint}/Assemblies/Profile/{poster}");
            var sb = new StringBuilder();
            sb.Append($"<a href='{userLink.ToString()}'>{poster}</a>");
            sb.Append($" mentioned you in <a href='{reviewLink.ToString()}'><b>{reviewName}</b></a>");
            sb.Append("<br>");
            sb.Append("Their comment was the following:");
            sb.Append("<br><br><i>");
            sb.Append(CommentMarkdownExtensions.MarkdownAsHtml(commentText));
            sb.Append("</i>");
            return sb.ToString();
        }
        
        private string GetHtmlContent(CommentModel comment, ReviewModel review)
        {
            var uri = new Uri($"{_endpoint}/Assemblies/Review/{review.ReviewId}#{Uri.EscapeUriString(comment.ElementId)}");
            var sb = new StringBuilder();
            sb.Append(GetContentHeading(comment, true));
            sb.Append("<br><br>");
            sb.Append($"In <a href='{uri.ToString()}'>{comment.ElementId}</a>:");
            sb.Append("<br><br>");
            sb.Append(CommentMarkdownExtensions.MarkdownAsHtml(comment.Comment));
            return sb.ToString();
        }
        
        private static string GetCommentTagPlainTextContent(CommentModel comment)
        {
            var sb = new StringBuilder();
            sb.Append(GetCommentTagContentHeading(comment, false));
            return sb.ToString();
        }

        private string GetPlainTextContent(CommentModel comment)
        {
            var sb = new StringBuilder();
            sb.Append(GetContentHeading(comment, false));
            sb.Append("\r\n");
            sb.Append(CommentMarkdownExtensions.MarkdownAsPlainText(comment.Comment));
            return sb.ToString();
        }
        
        private static string GetApproverReviewContentHeading(UserProfileModel user, bool includeHtml) =>
            $"{(includeHtml ? $"<b>{user.UserName}</b>" : $"{user.UserName}")} requested you to review API.";
        
        private static string GetCommentTagContentHeading(CommentModel comment, bool includeHtml) =>
            $"{(includeHtml ? $"<b>{comment.Username}</b>" : $"{comment.Username}")} tagged you in a comment.";

        private static string GetContentHeading(CommentModel comment, bool includeHtml) =>
            $"{(includeHtml ? $"<b>{comment.Username}</b>" : $"{comment.Username}")} commented on this review.";

        public async Task NotifySubscribersOnNewRevisionAsync(ReviewRevisionModel revision, ClaimsPrincipal user)
        {
            var review = revision.Review;
            var uri = new Uri($"{_endpoint}/Assemblies/Review/{review.ReviewId}");
            var plainTextContent = $"A new revision, {revision.DisplayName}," +
                $" was uploaded by {revision.Author}.";
            var htmlContent = $"A new revision, <a href='{uri.ToString()}'>{revision.DisplayName}</a>," +
                $" was uploaded by <b>{revision.Author}</b>.";
            await SendEmailsAsync(review, user, plainTextContent, htmlContent);
        }
        
        private async Task SendUserEmailsAsync(ReviewModel review, UserProfileModel user, string plainTextContent, string htmlContent)
        {
            ClaimsPrincipal userBackup = new ClaimsPrincipal();
            EmailAddress e;
            if (!user.Email.IsNullOrEmpty())
            {
                e = new EmailAddress(user.Email, user.UserName);
            } 
            else 
            {
                var backupEmail = GetUserEmail(userBackup);
                if (!backupEmail.IsNullOrEmpty())
                {
                    e = new EmailAddress(backupEmail, user.UserName);
                }
                else
                {
                    return;
                }
            }
            var client = new SendGridClient(_sendGridKey);
            var from = new EmailAddress(FROM_ADDRESS);
            SendGridMessage msg = MailHelper.CreateSingleEmail(
                from,
                e,
                user.UserName,
                plainTextContent,
                htmlContent);
            var threadHeader = $"<{review.ReviewId}{FROM_ADDRESS}>";
            msg.AddHeader(REPLY_TO_HEADER, threadHeader); 
            msg.AddHeader(REFERENCES_HEADER, threadHeader);
            await client.SendEmailAsync(msg);
        }
        private async Task SendEmailsAsync(ReviewModel review, ClaimsPrincipal user, string plainTextContent, string htmlContent)
        {
            string initiatingUserEmail = GetUserEmail(user);
            var subscribers = review.Subscribers.ToList()
                    .Where(e => e != initiatingUserEmail) // don't include the initiating user in the email
                    .Select(e => new EmailAddress(e))
                    .ToList();
            if (subscribers.Count == 0)
            {
                return;
            }

            var client = new SendGridClient(_sendGridKey);
            var from = new EmailAddress(FROM_ADDRESS, GetUserName(user));
            SendGridMessage msg = MailHelper.CreateMultipleEmailsToMultipleRecipients(
                from,
                subscribers,
                Enumerable.Repeat(review.DisplayName, review.Subscribers.Count).ToList(),
                plainTextContent,
                htmlContent,
                Enumerable.Repeat(new Dictionary<string, string>(), review.Subscribers.Count).ToList());
            var threadHeader = $"<{review.ReviewId}{FROM_ADDRESS}>";
            msg.AddHeader(REPLY_TO_HEADER, threadHeader); 
            msg.AddHeader(REFERENCES_HEADER, threadHeader);
            await client.SendEmailAsync(msg);
        }

        public async Task ToggleSubscribedAsync(ClaimsPrincipal user, string reviewId)
        {
            ReviewModel review = await _reviewRepository.GetReviewAsync(reviewId);
            if (review.IsUserSubscribed(user))
            {
                await UnsubscribeAsync(review, user);
            }
            else
            {
                await SubscribeAsync(review, user);
            }
        }

        public async Task SubscribeAsync(ReviewModel review, ClaimsPrincipal user)
        {
            string email = GetUserEmail(user);

            if (email != null && !review.Subscribers.Contains(email))
            {
                review.Subscribers.Add(email);
                await _reviewRepository.UpsertReviewAsync(review);
            }
        }

        public async Task UnsubscribeAsync(ReviewModel review, ClaimsPrincipal user)
        {
            string email = GetUserEmail(user);
            if (email != null && review.Subscribers.Contains(email))
            {
                review.Subscribers.Remove(email);
                await _reviewRepository.UpsertReviewAsync(review);
            }
        }

        public static string GetUserEmail(ClaimsPrincipal user) =>
            user.FindFirstValue(ClaimConstants.Email);

        private static string GetUserName(ClaimsPrincipal user)
        {
            string name = user.FindFirstValue(ClaimConstants.Name);
            return string.IsNullOrEmpty(name) ? user.FindFirstValue(ClaimConstants.Login) : name;
        }
    }
}

