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
using Microsoft.ApplicationInsights;
using System.Net.Http;
using System.Text.Json;
using APIViewWeb.LeanModels;
using APIViewWeb.Helpers;
using APIViewWeb.Services;
using Microsoft.Extensions.Logging;
using APIViewWeb.Managers.Interfaces;

namespace APIViewWeb.Managers
{
        public class NotificationManager : INotificationManager
    {
        private readonly IConfiguration _configuration;
        private readonly string _apiviewEndpoint;
        private readonly ICosmosReviewRepository _reviewRepository;
        private readonly ICosmosAPIRevisionsRepository _apiRevisionRepository;
        private readonly ICosmosUserProfileRepository _userProfileRepository;
        private readonly UserProfileCache _userProfileCache;
        private readonly string _testEmailToAddress;
        private readonly string _emailSenderServiceUrl;
        private readonly TelemetryClient _telemetryClient;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NotificationManager> _logger;
        private readonly IEnumerable<LanguageService> _languageServices;
        private readonly IPermissionsManager _permissionsManager;

        private const string ENDPOINT_SETTING = "APIVIew-Host-Url";

        public NotificationManager(IConfiguration configuration,
            ICosmosReviewRepository reviewRepository, ICosmosAPIRevisionsRepository apiRevisionRepository,
            ICosmosUserProfileRepository userProfileRepository,
            UserProfileCache userProfileCache,
            TelemetryClient telemetryClient,
            IEmailTemplateService emailTemplateService,
            IHttpClientFactory httpClientFactory,
            ILogger<NotificationManager> logger,
            IEnumerable<LanguageService> languageServices,
            IPermissionsManager permissionsManager)
        {
            _configuration = configuration;
            _apiviewEndpoint = configuration.GetValue<string>(ENDPOINT_SETTING);
            _reviewRepository = reviewRepository;
            _apiRevisionRepository = apiRevisionRepository;
            _userProfileRepository = userProfileRepository;
            _userProfileCache = userProfileCache;
            _testEmailToAddress = configuration["apiview-email-test-address"] ?? "";
            _emailSenderServiceUrl = configuration["azure-sdk-emailer-url"] ?? "";
            _telemetryClient = telemetryClient;
            _emailTemplateService = emailTemplateService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _languageServices = languageServices;
            _permissionsManager = permissionsManager;
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

        public async Task NotifyApproversOfReview(ClaimsPrincipal user, string apiRevisionId, HashSet<string> reviewers)
        {
            var userProfile = await _userProfileRepository.TryGetUserProfileAsync(user.GetGitHubLogin());
            var apiRevision = await _apiRevisionRepository.GetAPIRevisionAsync(apiRevisionId);
            foreach (var reviewer in reviewers)
            {
                var reviewerProfile = await _userProfileRepository.TryGetUserProfileAsync(reviewer);
                await SendUserEmailsAsync(apiRevision, reviewerProfile,
                    GetApproverReviewHtmlContent(userProfile, apiRevision));
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
        /// <param name="state"></param> true = subscribe, false = unsubscribe
        /// <returns></returns>
        public async Task ToggleSubscribedAsync(ClaimsPrincipal user, string reviewId, bool? state = null)
        {
            var review = await _reviewRepository.GetReviewAsync(reviewId);
            if (PageModelHelpers.IsUserSubscribed(user, review.Subscribers))
            {
                if (state == true)
                {
                    return; // already subscribed
                }

                await UnsubscribeAsync(review, user);
            }
            else
            {
                if (state == false)
                {
                    return; // already unsubscribed
                }

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

        private string GetApproverReviewHtmlContent<T>(UserProfileModel user, T model) where T : BaseListitemModel
        {
            var reviewName = model.PackageName;
            var reviewLink = new Uri($"{_apiviewEndpoint}/Assemblies/Review/{model.Id}");
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
            var reviewLink = ManagerHelpers.ResolveReviewUrl(
                reviewId: review.Id,
                apiRevisionId: comment.APIRevisionId,
                language: review.Language,
                configuration: _configuration,
                languageServices: _languageServices,
                elementId: comment.ElementId);
            var commentText = comment.CommentText;
            var poster = comment.CreatedBy;
            var userLink = new Uri($"{_apiviewEndpoint}/Assemblies/Profile/{poster}");
            var sb = new StringBuilder();
            sb.Append($"<a href='{userLink.ToString()}'>{poster}</a>");
            sb.Append($" mentioned you in <a href='{reviewLink}'><b>{reviewName}</b></a>");
            sb.Append("<br>");
            sb.Append("Their comment was the following:");
            sb.Append("<br><br><i>");
            sb.Append(CommentMarkdownExtensions.MarkdownAsHtml(commentText));
            sb.Append("</i>");
            return sb.ToString();
        }

        private string GetHtmlContent(CommentItemModel comment, ReviewListItemModel review)
        {
            var sb = new StringBuilder();
            sb.Append(GetContentHeading(comment, true));
            sb.Append("<br><br>");

            if (comment.ElementId != null)
            {
                var uri = ManagerHelpers.ResolveReviewUrl(
                    reviewId: review.Id,
                    apiRevisionId: comment.APIRevisionId,
                    language: review.Language,
                    configuration: _configuration,
                    languageServices: _languageServices,
                    elementId: comment.ElementId);
                sb.Append($"In <a href='{uri}'>{WebUtility.HtmlEncode(comment.ElementId)}</a>:");
                sb.Append("<br><br>");
            }

            sb.Append(CommentMarkdownExtensions.MarkdownAsHtml(comment.CommentText));
            return sb.ToString();
        }

        private static string GetContentHeading(CommentItemModel comment, bool includeHtml) =>
            $"{(includeHtml ? $"<b>{comment.CreatedBy}</b>" : $"{comment.CreatedBy}")} commented on this review.";

        private async Task SendUserEmailsAsync<T>(T model, UserProfileModel user, string htmlContent) where T : BaseListitemModel 
        {
            // SendEmailAsync already handles email validation
            await SendEmailAsync(user.Email, $"Notification from APIView - {model.PackageName}", htmlContent);
        }
        private async Task SendEmailsAsync(ReviewListItemModel review, ClaimsPrincipal user, string htmlContent, ISet<string> notifiedUsers)
        {
            var initiatingUserEmail = GetUserEmail(user);
            
            // Get emails for notified users concurrently
            HashSet<string> notifiedEmails = new HashSet<string>();
            if (notifiedUsers?.Any() == true)
            {
                // Create all email fetch tasks concurrently
                var emailTasks = notifiedUsers
                    .Select(async username => new { Username = username, Email = await GetEmailAddress(username) })
                    .ToArray();
                
                // Await all tasks at once
                var emailResults = await Task.WhenAll(emailTasks);
                
                // Process results and add valid emails
                foreach (var result in emailResults)
                {
                    if (string.IsNullOrEmpty(result.Email))
                    {
                        _telemetryClient.TrackTrace($"Email address is not available for user {result.Username}, review {review.Id}. Not sending email.");
                        continue;
                    }
                    notifiedEmails.Add(result.Email);
                }
            }
            
            var subscribers = review.Subscribers.ToList()
                    .Where(e => e != initiatingUserEmail && !notifiedEmails.Contains(e)) // don't include the initiating user and tagged users in the comment
                    .ToList();
            if (subscribers.Count == 0)
            {
                return;
            }

            // Send single email to all subscribers
            var emailToList = string.Join("; ", subscribers);
            await SendEmailAsync(emailToList, $"Update on APIView - {review.PackageName} from {GetUserName(user)}", htmlContent);
        }

        private static string GetUserName(ClaimsPrincipal user)
        {
            var name = user.FindFirstValue(ClaimConstants.Name);
            return string.IsNullOrEmpty(name) ? user.FindFirstValue(ClaimConstants.Login) : name;
        }

        private async Task<string> GetEmailAddress(string username)
        {
            var user = await _userProfileRepository.TryGetUserProfileAsync(username);
            return user?.Email ?? "";
        }

        /// <summary>
        /// Get all email recipients for namespace review notifications (approvers + requester)
        /// </summary>
        private async Task<List<string>> GetNamespaceReviewEmailRecipientsAsync(ReviewListItemModel review, ClaimsPrincipal requestingUser = null)
        {
            var emailAddresses = new List<string>();
            
            // Get all approvers for the review's language using the permissions system
            HashSet<string> approversForLanguage = await _permissionsManager.GetApproversForLanguageAsync(review.Language);

            // Add language approvers' emails
            // Create all tasks first (starts them concurrently)
            var emailTasks = (approversForLanguage ?? Enumerable.Empty<string>())
                .Select(GetEmailAddress)
                .ToArray();
                
            // Await all tasks at once
            var approverEmails = await Task.WhenAll(emailTasks);
            
            // Filter out null/empty emails
            emailAddresses.AddRange(approverEmails.Where(email => !string.IsNullOrEmpty(email)));
            
            // Add requesting user's email (either from ClaimsPrincipal or from review record)
            string requesterUsername = requestingUser?.GetGitHubLogin() ?? review.NamespaceApprovalRequestedBy;
            if (!string.IsNullOrEmpty(requesterUsername))
            {
                string requesterEmail = await GetEmailAddress(requesterUsername);
                if (!string.IsNullOrEmpty(requesterEmail) && !emailAddresses.Contains(requesterEmail))
                {
                    emailAddresses.Add(requesterEmail);
                }
            }
            
            return emailAddresses;
        }

        public async Task NotifyApproversOnNamespaceReviewRequest(ClaimsPrincipal user, ReviewListItemModel review, IEnumerable<ReviewListItemModel> languageReviews = null, string notes = "")
        {
            try
            {
                // Get all email recipients (approvers + requesting user)
                var emailAddresses = await GetNamespaceReviewEmailRecipientsAsync(review, user);
                
                if (emailAddresses.Count == 0)
                {
                    return;
                }

                var subject = $"Namespace Review Requested: {review.PackageName}";

                // Build TypeSpec URL
                var typeSpecUrl = $"{_apiviewEndpoint}/Assemblies/Review/{review.Id}";
                
                // Generate email content using template with actual language review data
                var emailContent = await _emailTemplateService.GetNamespaceReviewRequestEmailAsync(
                    review.PackageName,
                    typeSpecUrl,
                    languageReviews ?? Enumerable.Empty<ReviewListItemModel>(),
                    notes);
                
                var emailToList = string.Join("; ", emailAddresses);
                
                await SendEmailAsync(emailToList, subject, emailContent);
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
        }

        private async Task SendEmailAsync(string emailToList, string subject, string content)
        {
            if (string.IsNullOrEmpty(_emailSenderServiceUrl))
            {
                _logger.LogTrace("Email sender service URL is not configured. Email will not be sent to {EmailToList} with subject: {Subject}", emailToList, subject);
                return;
            }
            
            var emailToAddress = !string.IsNullOrEmpty(_testEmailToAddress) ? _testEmailToAddress : emailToList;
            var requestBody = new EmailModel(emailToAddress, subject, content);
            var httpClient = new HttpClient();
            
            try
            {
                var requestBodyJson = JsonSerializer.Serialize(requestBody);
                _logger.LogTrace("Sending email address request to logic apps. request: {RequestBody}", requestBodyJson);
                var response = await httpClient.PostAsync(_emailSenderServiceUrl, new StringContent(requestBodyJson, Encoding.UTF8, "application/json"));
                if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Accepted)
                {
                   _logger.LogTrace("Failed to send email to user {EmailToList} with subject: {Subject}, status code: {StatusCode}, Details: {ResponseDetails}", emailToList, subject, response.StatusCode, response.ToString());
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
        }

        public async Task NotifyStakeholdersOfManualApproval(ReviewListItemModel review, IEnumerable<ReviewListItemModel> associatedReviews)
        {
            try
            {
                // Get all email recipients (approvers + original requester)
                var emailAddresses = await GetNamespaceReviewEmailRecipientsAsync(review);
                
                if (!emailAddresses.Any())
                {
                    return;
                }
                
                var subject = $"Namespace Review Approved: {review.PackageName}";
                
                // Build TypeSpec URL
                var typeSpecUrl = $"{_apiviewEndpoint}/Assemblies/Review/{review.Id}";
                
                // Use the unified approval email template for manual approval
                var emailContent = await _emailTemplateService.GetNamespaceReviewApprovedEmailAsync(
                    review.PackageName,
                    typeSpecUrl,
                    associatedReviews ?? Enumerable.Empty<ReviewListItemModel>());
                
                var emailToList = string.Join("; ", emailAddresses);
                
                await SendEmailAsync(emailToList, subject, emailContent);
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
        }
    }
}

