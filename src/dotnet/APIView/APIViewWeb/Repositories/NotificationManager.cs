// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Models;
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
        private const string FROM_NAME = "Api View";

        public NotificationManager(IConfiguration configuration, CosmosReviewRepository reviewRepository)
        {
            _sendGridKey = configuration[SENDGRID_KEY_SETTING];
            _endpoint = configuration.GetValue<string>(ENDPOINT_SETTING);
            _reviewRepository = reviewRepository;
        }

        public async Task SubscribeAndNotify(ClaimsPrincipal user, CommentModel comment)
        {
            ReviewModel review = await _reviewRepository.GetReviewAsync(comment.ReviewId);
            review.Subscribe(user);
            await _reviewRepository.UpsertReviewAsync(review);
            await SendEmailsAsync(review, GetPlainTextContent(comment), GetHtmlContent(comment, review));
        }

        private string GetHtmlContent(CommentModel comment, ReviewModel review)
        {
            var uri = new Uri($"{_endpoint}/Assemblies/Review/{review.ReviewId}#{comment.ElementId}");
            var sb = new StringBuilder();
            sb.Append(GetContentHeading(comment, true));
            sb.Append("<br><br>");
            sb.Append($"In <a href='{uri.ToString()}'>{comment.ElementId}</a>:");
            sb.Append("<br><br>");
            sb.Append(comment.Comment);
            return sb.ToString();
        }

        private string GetPlainTextContent(CommentModel comment)
        {
            var sb = new StringBuilder();
            sb.Append(GetContentHeading(comment, false));
            sb.Append("\r\n");
            sb.Append(comment.Comment);
            return sb.ToString();
        }

        private static string GetContentHeading(CommentModel comment, bool includeHtml) =>
            $"{(includeHtml ? $"<b>{comment.Username}</b>" : $"{comment.Username}")} commented on this review at {comment.TimeStamp}";

        public async Task NotifySubscribersOnNewRevisionAsync(ReviewRevisionModel revision)
        {
            var review = revision.Review;
            var uri = new Uri($"{_endpoint}/Assemblies/Review/{review.ReviewId}");
            var plainTextContent = $"A new revision, {revision.Name}," +
                $" was uploaded by {revision.Author} at {revision.CreationDate}";
            var htmlContent = $"A new revision, <a href='{uri.ToString()}'>{revision.Name}</a>," +
                $" was uploaded by <b>{revision.Author}</b> at {revision.CreationDate}";
            await SendEmailsAsync(review, plainTextContent, htmlContent);
        }
        private async Task SendEmailsAsync(ReviewModel review, string plainTextContent, string htmlContent)
        {
            if (review.Subscribers.Count == 0)
            {
                return;
            }
            var client = new SendGridClient(_sendGridKey);
            var from = new EmailAddress(FROM_ADDRESS, FROM_NAME);
            SendGridMessage msg = MailHelper.CreateMultipleEmailsToMultipleRecipients(
                from,
                review.Subscribers.ToList().
                    Select(e => new EmailAddress(e)).ToList(),
                Enumerable.Repeat(review.Name, review.Subscribers.Count).ToList(),
                plainTextContent,
                htmlContent,
                Enumerable.Repeat(new Dictionary<string, string>(), review.Subscribers.Count).ToList());
            await client.SendEmailAsync(msg);
        }
    }
}
