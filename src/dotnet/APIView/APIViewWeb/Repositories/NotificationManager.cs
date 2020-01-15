using APIViewWeb.Models;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIViewWeb.Repositories
{
    public class NotificationManager
    {
        private readonly string _endpoint;

        public NotificationManager(IConfiguration configuration)
        {
            _endpoint = configuration.GetValue<string>("Endpoint");
        }

        public async Task NotifySubscribersOnCommentAsync(ReviewModel review, CommentModel comment) =>
            await SendEmailsAsync(review, GetPlainTextContent(comment), GetHtmlContent(comment, review));

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
            var apiKey = Environment.GetEnvironmentVariable("API_VIEW", EnvironmentVariableTarget.Machine);
            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("apiview-noreply@microsoft.com", "Api View");
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
