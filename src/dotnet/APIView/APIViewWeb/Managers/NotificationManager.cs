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

        public async Task NotifySubscribersOnCommentAsync(ClaimsPrincipal user, CommentItemModel comment)
        {
            var review = await _reviewRepository.GetReviewAsync(comment.ReviewId);
            var elementUrl = comment.ElementId == null
                ? null
                : ManagerHelpers.ResolveReviewUrl(
                    reviewId: review.Id,
                    apiRevisionId: comment.APIRevisionId,
                    language: review.Language,
                    configuration: _configuration,
                    languageServices: _languageServices,
                    elementId: comment.ElementId);

            var content = await _emailTemplateService.RenderAsync(
                EmailTemplateKey.SubscriberComment,
                SubscriberCommentEmailModel.Create(_apiviewEndpoint, comment, elementUrl));
            await SendSubscriberEmailsAsync(review, user, content, comment.TaggedUsers, "New Comment");
        }

        public async Task NotifyUserOnCommentTagAsync(CommentItemModel comment)
        {
            var review = await _reviewRepository.GetReviewAsync(comment.ReviewId);

            foreach (string username in comment.TaggedUsers)
            {
                if(string.IsNullOrEmpty(username)) continue;
                var user = await _userProfileRepository.TryGetUserProfileAsync(username);
                var reviewUrl = ManagerHelpers.ResolveReviewUrl(
                    reviewId: review.Id,
                    apiRevisionId: comment.APIRevisionId,
                    language: review.Language,
                    configuration: _configuration,
                    languageServices: _languageServices,
                    elementId: comment.ElementId);
                var content = await _emailTemplateService.RenderAsync(
                    EmailTemplateKey.CommentTag,
                    CommentTagEmailModel.Create(_apiviewEndpoint, comment, review, reviewUrl));
                await SendEmailAsync(user?.Email, BuildEmailSubject("Comment Mention", review.PackageName), content);
            } 
        }

        public async Task NotifyAssignedReviewersAsync(ClaimsPrincipal user, string apiRevisionId, HashSet<string> reviewers)
        {
            if (reviewers == null || reviewers.Count == 0)
            {
                return;
            }

            var userProfile = await _userProfileRepository.TryGetUserProfileAsync(user.GetGitHubLogin());
            var apiRevision = await _apiRevisionRepository.GetAPIRevisionAsync(apiRevisionId);

            foreach (var reviewer in reviewers)
            {
                var reviewerProfile = await _userProfileRepository.TryGetUserProfileAsync(reviewer);
                var content = await _emailTemplateService.RenderAsync(
                    EmailTemplateKey.ReviewerAssigned,
                    ReviewerAssignedEmailModel.Create(_apiviewEndpoint, userProfile.UserName, apiRevision.ReviewId, apiRevision.PackageName));
                await SendEmailAsync(reviewerProfile?.Email, BuildEmailSubject("Review Requested", apiRevision.PackageName), content);
            }
        }

        public async Task NotifySubscribersOnNewRevisionAsync(ReviewListItemModel review, APIRevisionListItemModel revision, ClaimsPrincipal user)
        {
            var htmlContent = await _emailTemplateService.RenderAsync(
                EmailTemplateKey.NewRevision,
                NewRevisionEmailModel.Create(_apiviewEndpoint, review, revision));
            await SendSubscriberEmailsAsync(review, user, htmlContent, null, "New Revision Uploaded");
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

        private static string BuildEmailSubject(string eventName, string packageName)
        {
            return $"[APIView] {eventName} for {packageName}";
        }

        private async Task SendSubscriberEmailsAsync(ReviewListItemModel review, ClaimsPrincipal user, string htmlContent, ISet<string> notifiedUsers, string eventName)
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
            await SendEmailAsync(emailToList, BuildEmailSubject(eventName, review.PackageName), htmlContent);
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

        public async Task NotifyNamespaceReviewRequestRecipientsAsync(ClaimsPrincipal user, ReviewListItemModel review, IEnumerable<ReviewListItemModel> languageReviews = null, string notes = "")
        {
            try
            {
                // Get all email recipients (approvers + requesting user)
                var emailAddresses = await GetNamespaceReviewEmailRecipientsAsync(review, user);
                
                if (emailAddresses.Count == 0)
                {
                    return;
                }

                var subject = BuildEmailSubject("Namespace Review Requested", review.PackageName);

                // Build TypeSpec URL
                var typeSpecUrl = $"{_apiviewEndpoint}/Assemblies/Review/{review.Id}";
                
                // Generate email content using template with actual language review data
                var emailContent = await _emailTemplateService.RenderAsync(
                    EmailTemplateKey.NamespaceReviewRequest,
                    NamespaceReviewRequestEmailModel.Create(
                        review.PackageName,
                        typeSpecUrl,
                        languageReviews ?? Enumerable.Empty<ReviewListItemModel>(),
                        notes,
                        _apiviewEndpoint));
                
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

            if (string.IsNullOrWhiteSpace(emailToList))
            {
                _logger.LogTrace("Email recipient is empty. Email will not be sent for subject: {Subject}", subject);
                _telemetryClient.TrackTrace($"Email recipient is empty. Email will not be sent for subject: {subject}");
                return;
            }

            var requestBody = CreateEmailModel(emailToList, subject, content);
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

        private EmailModel CreateEmailModel(string emailToList, string subject, string content)
        {
            var isTestMode = !string.IsNullOrEmpty(_testEmailToAddress);
            var emailToAddress = isTestMode ? _testEmailToAddress : emailToList;
            var emailContent = AppendTestModeFooter(content, emailToList, isTestMode);
            return new EmailModel(emailToAddress, subject, emailContent);
        }

        private static string AppendTestModeFooter(string content, string intendedRecipients, bool isTestMode)
        {
            if (!isTestMode)
            {
                return content;
            }

            var encodedRecipients = WebUtility.HtmlEncode(intendedRecipients);
            return content + $"<br><br><hr><div style='font-size: 12px; color: #666;'><b>Test mode:</b> Intended recipients: {encodedRecipients}</div>";
        }

        public async Task NotifyStakeholdersOfManualApprovalAsync(ReviewListItemModel review, IEnumerable<ReviewListItemModel> associatedReviews)
        {
            try
            {
                // Get all email recipients (approvers + original requester)
                var emailAddresses = await GetNamespaceReviewEmailRecipientsAsync(review);
                
                if (!emailAddresses.Any())
                {
                    return;
                }
                
                var subject = BuildEmailSubject("Namespace Review Approved", review.PackageName);
                
                // Build TypeSpec URL
                var typeSpecUrl = $"{_apiviewEndpoint}/Assemblies/Review/{review.Id}";
                
                // Use the unified approval email template for manual approval
                var emailContent = await _emailTemplateService.RenderAsync(
                    EmailTemplateKey.NamespaceReviewApproved,
                    NamespaceReviewApprovedEmailModel.Create(
                        review.PackageName,
                        typeSpecUrl,
                        associatedReviews ?? Enumerable.Empty<ReviewListItemModel>(),
                        _apiviewEndpoint));
                
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

