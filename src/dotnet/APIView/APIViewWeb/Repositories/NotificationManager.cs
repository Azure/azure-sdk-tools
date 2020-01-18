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

namespace APIViewWeb.Repositories
{
    public class NotificationManager
    {
        private readonly string _endpoint;
        private readonly string _sendGridKey;
        private readonly CosmosReviewRepository _reviewRepository;

        private const string ENDPOINT_SETTING = "Endpoint";
        private const string SENDGRID_KEY_SETTING = "SendGrid:Key";
        private const string FROM_ADDRESS = "apiview-noreply@microsoft.com";

        public NotificationManager(IConfiguration configuration, CosmosReviewRepository reviewRepository)
        {
            _sendGridKey = configuration[SENDGRID_KEY_SETTING];
            _endpoint = configuration.GetValue<string>(ENDPOINT_SETTING);
            _reviewRepository = reviewRepository;
        }

        public async Task NotifySubscribersOnComment(ClaimsPrincipal user, CommentModel comment)
        {
            ReviewModel review = await _reviewRepository.GetReviewAsync(comment.ReviewId);
            await SendEmailsAsync(review, user, GetPlainTextContent(comment), GetHtmlContent(comment, review));
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

        private string GetPlainTextContent(CommentModel comment)
        {
            var sb = new StringBuilder();
            sb.Append(GetContentHeading(comment, false));
            sb.Append("\r\n");
            sb.Append(CommentMarkdownExtensions.MarkdownAsPlainText(comment.Comment));
            return sb.ToString();
        }

        private static string GetContentHeading(CommentModel comment, bool includeHtml) =>
            $"{(includeHtml ? $"<b>{comment.Username}</b>" : $"{comment.Username}")} commented on this review at {comment.TimeStamp}";

        public async Task NotifySubscribersOnNewRevisionAsync(ReviewRevisionModel revision, ClaimsPrincipal user)
        {
            var review = revision.Review;
            var uri = new Uri($"{_endpoint}/Assemblies/Review/{review.ReviewId}");
            var plainTextContent = $"A new revision, {revision.Name}," +
                $" was uploaded by {revision.Author} at {revision.CreationDate}";
            var htmlContent = $"A new revision, <a href='{uri.ToString()}'>{revision.Name}</a>," +
                $" was uploaded by <b>{revision.Author}</b> at {revision.CreationDate}";
            await SendEmailsAsync(review, user, plainTextContent, htmlContent);
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
                Enumerable.Repeat(review.Name, review.Subscribers.Count).ToList(),
                plainTextContent,
                htmlContent,
                Enumerable.Repeat(new Dictionary<string, string>(), review.Subscribers.Count).ToList());
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
