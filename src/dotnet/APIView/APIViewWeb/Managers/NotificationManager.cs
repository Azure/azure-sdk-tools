// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIView.Identity;
using APIViewWeb.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights;
using System.Net.Http;
using System.Text.Json;
using APIViewWeb.LeanModels;
using APIViewWeb.Helpers;

namespace APIViewWeb.Managers
{
    public class NotificationManager : INotificationManager
    {
        private readonly string _apiviewEndpoint;
        private readonly ICosmosReviewRepository _reviewRepository;
        private readonly ICosmosUserProfileRepository _userProfileRepository;
        private readonly IConfiguration _configuration;
        private readonly string _testEmailToAddress;
        private readonly string _emailSenderServiceUrl;

        private const string ENDPOINT_SETTING = "Endpoint";

        static TelemetryClient _telemetryClient = new(TelemetryConfiguration.CreateDefault());

        public NotificationManager(IConfiguration configuration,
            ICosmosReviewRepository reviewRepository,
            ICosmosUserProfileRepository userProfileRepository)
        {
            _apiviewEndpoint = configuration.GetValue<string>(ENDPOINT_SETTING);
            _reviewRepository = reviewRepository;
            _userProfileRepository = userProfileRepository;
            _configuration = configuration;
            _testEmailToAddress = configuration["apiview-email-test-address"] ?? "";
            _emailSenderServiceUrl = configuration["azure-sdk-emailer-url"] ?? "";
        }

        public async Task NotifySubscribersOnComment(ClaimsPrincipal user, CommentItemModel comment)
        {
            var review = await _reviewRepository.GetReviewAsync(comment.ReviewId);
            await SendEmailsAsync(review, user, GetHtmlContent(comment, review), comment.TaggedUsers);
        }

        public async Task NotifyUserOnCommentTag(CommentItemModel comment)
        {
            foreach (string username in comment.TaggedUsers)
            {
                if(string.IsNullOrEmpty(username)) continue;
                var review = await _reviewRepository.GetReviewAsync(comment.ReviewId);
                var user = await _userProfileRepository.TryGetUserProfileAsync(username);
                await SendUserEmailsAsync(review, user, GetCommentTagHtmlContent(comment, review));
            } 
        }

        public async Task NotifyApproversOfReview(ClaimsPrincipal user, string reviewId, HashSet<string> reviewers)
        {
            var userProfile = await _userProfileRepository.TryGetUserProfileAsync(user.GetGitHubLogin());
            var review = await _reviewRepository.GetReviewAsync(reviewId);
            foreach (var reviewer in reviewers)
            {
                var reviewerProfile = await _userProfileRepository.TryGetUserProfileAsync(reviewer);
                await SendUserEmailsAsync(review, reviewerProfile,
                    GetApproverReviewHtmlContent(userProfile, review));
            }
        }

        public async Task NotifySubscribersOnNewRevisionAsync(ReviewListItemModel review, APIRevisionListItemModel revision, ClaimsPrincipal user)
        {
            var uri = new Uri($"{_apiviewEndpoint}/Assemblies/Review/{review.Id}");
            var htmlContent = $"A new revision, <a href='{uri.ToString()}'>{PageModelHelpers.ResolveRevisionLabel(revision)}</a>," +
                $" was uploaded by <b>{revision.CreatedBy}</b>.";
            await SendEmailsAsync(review, user, htmlContent, null);
        }
        /// <summary>
        /// Toggle Subscription to a Review
        /// </summary>
        /// <param name="user"></param>
        /// <param name="reviewId"></param>
        /// <returns></returns>
        public async Task ToggleSubscribedAsync(ClaimsPrincipal user, string reviewId)
        {
            var review = await _reviewRepository.GetReviewAsync(reviewId);
            if (PageModelHelpers.IsUserSubscribed(user, review.Subscribers))
            {
                await UnsubscribeAsync(review, user);
            }
            else
            {
                await SubscribeAsync(review, user);
            }
        }

        /// <summary>
        /// Subscribe to Review
        /// </summary>
        /// <param name="review"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task SubscribeAsync(ReviewListItemModel review, ClaimsPrincipal user)
        {
            var email = GetUserEmail(user);

            if (email != null && !review.Subscribers.Contains(email))
            {
                review.Subscribers.Add(email);
                await _reviewRepository.UpsertReviewAsync(review);
            }
        }

        /// <summary>
        /// Unsubscribe from Review
        /// </summary>
        /// <param name="review"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task UnsubscribeAsync(ReviewListItemModel review, ClaimsPrincipal user)
        {
            var email = GetUserEmail(user);
            if (email != null && review.Subscribers.Contains(email))
            {
                review.Subscribers.Remove(email);
                await _reviewRepository.UpsertReviewAsync(review);
            }
        }

        public static string GetUserEmail(ClaimsPrincipal user) =>
            user.FindFirstValue(ClaimConstants.Email);

        private string GetApproverReviewHtmlContent(UserProfileModel user, ReviewListItemModel review)
        {
            var reviewName = review.PackageName;
            var reviewLink = new Uri($"{_apiviewEndpoint}/Assemblies/Review/{review.Id}");
            var poster = user.UserName;
            var userLink = new Uri($"{_apiviewEndpoint}/Assemblies/Profile/{poster}");
            var requestsLink = new Uri($"{_apiviewEndpoint}/Assemblies/RequestedReviews/");
            var sb = new StringBuilder();
            sb.Append($"<a href='{userLink.ToString()}'>{poster}</a>");
            sb.Append($" requested you to review <a href='{reviewLink.ToString()}'><b>{reviewName}</b></a>");
            sb.Append("<br>");
            sb.Append($"You can review all your pending APIViews <a href='{requestsLink.ToString()}'><b>here</b></a>");
            return sb.ToString();
        }

        private string GetCommentTagHtmlContent(CommentItemModel comment, ReviewListItemModel review)
        {
            var reviewName = review.PackageName;
            var reviewLink = new Uri($"{_apiviewEndpoint}/Assemblies/Review/{review.Id}#{Uri.EscapeDataString(comment.ElementId)}");
            var commentText = comment.CommentText;
            var poster = comment.CreatedBy;
            var userLink = new Uri($"{_apiviewEndpoint}/Assemblies/Profile/{poster}");
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

        private string GetHtmlContent(CommentItemModel comment, ReviewListItemModel review)
        {
            var uri = new Uri($"{_apiviewEndpoint}/Assemblies/Review/{review.Id}#{Uri.EscapeDataString(comment.ElementId)}");
            var sb = new StringBuilder();
            sb.Append(GetContentHeading(comment, true));
            sb.Append("<br><br>");
            sb.Append($"In <a href='{uri.ToString()}'>{comment.ElementId}</a>:");
            sb.Append("<br><br>");
            sb.Append(CommentMarkdownExtensions.MarkdownAsHtml(comment.CommentText));
            return sb.ToString();
        }

        private static string GetContentHeading(CommentItemModel comment, bool includeHtml) =>
            $"{(includeHtml ? $"<b>{comment.CreatedBy}</b>" : $"{comment.CreatedBy}")} commented on this review.";

        private async Task SendUserEmailsAsync(ReviewListItemModel review, UserProfileModel user, string htmlContent)
        {
            // Always send email to a test address when test address is configured.
            if (string.IsNullOrEmpty(user.Email))
            {
                _telemetryClient.TrackTrace($"Email address is not available for user {user.UserName}. Not sending email.");
                return;
            }

            await SendEmail(user.Email, $"Notification from APIView - {review.PackageName}", htmlContent);
        }
        private async Task SendEmailsAsync(ReviewListItemModel review, ClaimsPrincipal user, string htmlContent, ISet<string> notifiedUsers)
        {
            var initiatingUserEmail = GetUserEmail(user);
            // Find email address of already tagged users in comment
            HashSet<string> notifiedEmails = new HashSet<string>();
            if (notifiedUsers != null)
            {
                foreach (var username in notifiedUsers)
                {
                    var email = await GetEmailAddress(username);
                    if (string.IsNullOrEmpty(email))
                    {
                        _telemetryClient.TrackTrace($"Email address is not available for user {username}, review {review.Id}. Not sending email.");
                        continue;
                    }
                    notifiedEmails.Add(email);
                }
            }           
            var subscribers = review.Subscribers.ToList()
                    .Where(e => e != initiatingUserEmail && !notifiedEmails.Contains(e)) // don't include the initiating user and tagged users in the comment
                    .ToList();
            if (subscribers.Count == 0)
            {
                return;
            }

            foreach(var userEmail in subscribers)
            {
                await SendEmail(userEmail, $"Update on APIView - {review.PackageName} from {GetUserName(user)}", htmlContent);
            }
        }

        private async Task SendEmail(string emailToList, string subject, string content)
        {
            if (string.IsNullOrEmpty(_emailSenderServiceUrl))
            {
                _telemetryClient.TrackTrace($"Email sender service URL is not configured. Email will not be sent to {emailToList} with subject: {subject}");
                return;
            }
            var emailToAddress = !string.IsNullOrEmpty(_testEmailToAddress) ? _testEmailToAddress : emailToList;
            var requestBody = new EmailModel(emailToAddress, subject, content);
            var httpClient = new HttpClient();
            try
            {
                var requestBodyJson = JsonSerializer.Serialize(requestBody);
                _telemetryClient.TrackTrace($"Sending email address request to logic apps. request: {requestBodyJson}");
                var response = await httpClient.PostAsync(_emailSenderServiceUrl, new StringContent(requestBodyJson, Encoding.UTF8, "application/json"));
                if (response.StatusCode !=  HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Accepted)
                {
                    _telemetryClient.TrackTrace($"Failed to send email to user {emailToList} with subject: {subject}, status code: {response.StatusCode}, Details: {response.ToString}");
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
        }
        private static string GetUserName(ClaimsPrincipal user)
        {
            var name = user.FindFirstValue(ClaimConstants.Name);
            return string.IsNullOrEmpty(name) ? user.FindFirstValue(ClaimConstants.Login) : name;
        }

        private async Task<string> GetEmailAddress(string username)
        {
            if (string.IsNullOrEmpty(username))
                return "";
            var user = await _userProfileRepository.TryGetUserProfileAsync(username);
            return user.Email;
        }
    }
}

