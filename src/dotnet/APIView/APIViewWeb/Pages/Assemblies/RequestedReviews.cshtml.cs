using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.ApplicationInsights;

namespace APIViewWeb.Pages.Assemblies
{    
    public class RequestedReviews: PageModel
    {
        private readonly IAPIRevisionsManager _apiRevisionsManager;
        private readonly IReviewManager _reviewManager;
        private readonly IPullRequestManager _pullRequestManager;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly TelemetryClient _telemetryClient;
        public readonly UserProfileCache _userProfileCache;
        public IEnumerable<APIRevisionListItemModel> APIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> ActiveAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> ApprovedAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> NamespaceApprovalRequestedAPIRevisions { get; set; } = new List<APIRevisionListItemModel>();
        public IEnumerable<APIRevisionListItemModel> ReviewsWithoutNamespaceApproval { get; set; } = new List<APIRevisionListItemModel>();
        
        // Track namespace approval request info for associated revisions
        public Dictionary<string, (DateTime RequestedOn, string RequestedBy)> NamespaceApprovalInfo { get; set; } = new Dictionary<string, (DateTime, string)>();
        
        public RequestedReviews(IAPIRevisionsManager apiRevisionsManager, IReviewManager reviewManager, IPullRequestManager pullRequestManager, UserProfileCache userProfileCache, IConfiguration configuration, IMemoryCache cache, TelemetryClient telemetryClient)
        {
            _apiRevisionsManager = apiRevisionsManager;
            _reviewManager = reviewManager;
            _pullRequestManager = pullRequestManager;
            _userProfileCache = userProfileCache;
            _configuration = configuration;
            _cache = cache;
            _telemetryClient = telemetryClient;
        }        
        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.GetGitHubLogin();
            
            UserProfileModel userProfile = null;
            var approvedLanguages = new string[0];
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    userProfile = await _userProfileCache.GetUserProfileAsync(userId);
                }
                approvedLanguages = userProfile?.Preferences?.ApprovedLanguages?.ToArray() ?? new string[0];
            }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException ex) when (ex.Message.Contains("DefaultTempDataSerializer")) { }
            catch (Exception) { }
            
            IEnumerable<APIRevisionListItemModel> assignedRevisions;
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    assignedRevisions = await _apiRevisionsManager.GetAPIRevisionsAssignedToUser(userId);
                }
            }
            catch (OperationCanceledException)
            {
                assignedRevisions = new List<APIRevisionListItemModel>();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("DefaultTempDataSerializer"))
            {
                assignedRevisions = new List<APIRevisionListItemModel>();
            }
            catch (Exception)
            {
                assignedRevisions = new List<APIRevisionListItemModel>();
            }
            
            APIRevisions = assignedRevisions;

            // Debug: Log APIRevisions content
            _telemetryClient.TrackTrace($"[NAMESPACE DEBUG] APIRevisions contains {APIRevisions.Count()} revisions");
            var namespaceReviewIds = new[] { "8aea98eee9724452a9ea9cf08c3fdcf6", "90544d0a5f9c42fa971e468d53ac7578" };
            foreach (var nsReviewId in namespaceReviewIds)
            {
                var hasRevision = APIRevisions.Any(r => r.ReviewId == nsReviewId);
                _telemetryClient.TrackTrace($"[NAMESPACE DEBUG] APIRevisions contains revision for review {nsReviewId}: {hasRevision}");
            }

            var activeAPIRevs = new List<APIRevisionListItemModel>();
            var approvedAPIRevs = new List<APIRevisionListItemModel>();
            var namespaceApprovalAPIRevs = new List<APIRevisionListItemModel>();

            var allNamespaceApprovalReviews = new List<ReviewListItemModel>();
            // Always load namespace approval reviews for all users
            try 
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    allNamespaceApprovalReviews = await _reviewManager.GetPendingNamespaceApprovalsBatchAsync(User, 30);
                    
                    // Debug: Log what the page model received
                    _telemetryClient.TrackTrace($"[NAMESPACE DEBUG] Page model received {allNamespaceApprovalReviews.Count} namespace approval reviews");
                    foreach (var review in allNamespaceApprovalReviews)
                    {
                        _telemetryClient.TrackTrace($"[NAMESPACE DEBUG] Page received review: {review.Id} ({review.Language}) - Status: {review.NamespaceReviewStatus}");
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex.Message.Contains("AAD groups are being resolved")) { }
            catch (Exception) { }
            
            // Store namespace approval info for these reviews
            foreach (var nsReview in allNamespaceApprovalReviews)
            {
                if (nsReview.NamespaceApprovalRequestedOn.HasValue && !string.IsNullOrEmpty(nsReview.NamespaceApprovalRequestedBy))
                {
                    NamespaceApprovalInfo[nsReview.Id] = (
                        RequestedOn: nsReview.NamespaceApprovalRequestedOn.Value,
                        RequestedBy: nsReview.NamespaceApprovalRequestedBy
                    );
                }
            }

            var reviewIds = APIRevisions.Select(r => r.ReviewId).Distinct().ToList();
            // Add review IDs from namespace approval reviews
            reviewIds.AddRange(allNamespaceApprovalReviews.Select(r => r.Id));
            reviewIds = reviewIds.Distinct().ToList();
            var reviews = new Dictionary<string, ReviewListItemModel>();
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    var reviewTasks = reviewIds.Select(async reviewId =>
                    {
                        try
                        {
                            var review = await _reviewManager.GetReviewAsync(User, reviewId);
                            return (reviewId, review);
                        }
                        catch (Exception)
                        {
                            return (reviewId, (ReviewListItemModel)null);
                        }
                    });

                    var reviewResults = await Task.WhenAll(reviewTasks);
                    foreach (var (rid, review) in reviewResults)
                    {
                        if (review != null)
                        {
                            reviews[rid] = review;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }

            // Group revisions by ReviewId and track the latest one for namespace approval
            var latestRevisionsForNamespaceReviews = new Dictionary<string, APIRevisionListItemModel>();

            foreach (var apiRevision in APIRevisions.OrderByDescending(r => r.AssignedReviewers.Any() ? r.AssignedReviewers.Where(x => x.AssingedTo.Equals(userId)).FirstOrDefault()?.AssingedOn ?? r.CreatedOn : r.CreatedOn))
            {
                bool isAssignedToUser = apiRevision.AssignedReviewers.Any(x => x.AssingedTo.Equals(userId));
                
                if (isAssignedToUser)
                {
                    if (!apiRevision.IsApproved)
                    {
                        activeAPIRevs.Add(apiRevision);
                    }

                    if (apiRevision.IsApproved && apiRevision.ChangeHistory.First(c => c.ChangeAction == APIRevisionChangeAction.Approved).ChangedOn >= DateTime.Now.AddDays(-7))
                    {
                        approvedAPIRevs.Add(apiRevision);
                    }
                }

                if (reviews.TryGetValue(apiRevision.ReviewId, out var parentReview))
                {
                    bool isRevisionNamespaceRelated = parentReview.NamespaceReviewStatus == NamespaceReviewStatus.Pending;
                    
                    if (isRevisionNamespaceRelated && !parentReview.IsApproved)
                    {
                        // Allow all users to see namespace approvals
                        // Only keep the latest revision per review for namespace approval
                        if (!latestRevisionsForNamespaceReviews.ContainsKey(apiRevision.ReviewId))
                        {
                            latestRevisionsForNamespaceReviews[apiRevision.ReviewId] = apiRevision;
                        }
                        else
                        {
                            // Compare and keep the latest revision (most recent CreatedOn)
                            var existingRevision = latestRevisionsForNamespaceReviews[apiRevision.ReviewId];
                            if (apiRevision.CreatedOn > existingRevision.CreatedOn)
                            {
                                latestRevisionsForNamespaceReviews[apiRevision.ReviewId] = apiRevision;
                            }
                        }
                    }
                }
                
                // Also check if this revision belongs to any of the namespace approval reviews
                var namespaceReview = allNamespaceApprovalReviews.FirstOrDefault(r => r.Id == apiRevision.ReviewId);
                if (namespaceReview != null)
                {
                    // Allow all users to see namespace approvals
                    // Only keep the latest revision per review for namespace approval
                    if (!latestRevisionsForNamespaceReviews.ContainsKey(apiRevision.ReviewId))
                    {
                        latestRevisionsForNamespaceReviews[apiRevision.ReviewId] = apiRevision;
                    }
                    else
                    {
                        // Compare and keep the latest revision (most recent CreatedOn)
                        var existingRevision = latestRevisionsForNamespaceReviews[apiRevision.ReviewId];
                        if (apiRevision.CreatedOn > existingRevision.CreatedOn)
                        {
                            latestRevisionsForNamespaceReviews[apiRevision.ReviewId] = apiRevision;
                        }
                    }
                }
            }
            
            // For namespace approval reviews that don't have any revisions in APIRevisions, 
            // we need to fetch their revisions directly to display them
            foreach (var namespaceReview in allNamespaceApprovalReviews)
            {
                if (!latestRevisionsForNamespaceReviews.ContainsKey(namespaceReview.Id))
                {
                    try
                    {
                        // Get all revisions for this namespace approval review and find the latest one
                        var revisions = await _apiRevisionsManager.GetAPIRevisionsAsync(namespaceReview.Id);
                        var latestRevision = revisions?.OrderByDescending(r => r.CreatedOn).FirstOrDefault();
                        if (latestRevision != null)
                        {
                            latestRevisionsForNamespaceReviews[namespaceReview.Id] = latestRevision;
                            _telemetryClient.TrackTrace($"[NAMESPACE DEBUG] Added missing revision for namespace review {namespaceReview.Id}: {latestRevision.Id}");
                        }
                        else
                        {
                            _telemetryClient.TrackTrace($"[NAMESPACE DEBUG] No revisions found for namespace review {namespaceReview.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetryClient.TrackTrace($"[NAMESPACE DEBUG] Failed to get revision for namespace review {namespaceReview.Id}: {ex.Message}");
                    }
                }
            }
            
            // Convert the dictionary values to the final list
            namespaceApprovalAPIRevs = latestRevisionsForNamespaceReviews.Values.ToList();
            
            // Debug: Log final namespace approval revisions
            _telemetryClient.TrackTrace($"[NAMESPACE DEBUG] Final NamespaceApprovalRequestedAPIRevisions count: {namespaceApprovalAPIRevs.Count}");
            foreach (var revision in namespaceApprovalAPIRevs)
            {
                _telemetryClient.TrackTrace($"[NAMESPACE DEBUG] Final revision: {revision.Id} for review {revision.ReviewId}");
            }
            
            ActiveAPIRevisions = activeAPIRevs;
            ApprovedAPIRevisions = approvedAPIRevs;
            NamespaceApprovalRequestedAPIRevisions = namespaceApprovalAPIRevs;
            
            ReviewsWithoutNamespaceApproval = new List<APIRevisionListItemModel>();
            
            ApprovedAPIRevisions.OrderByDescending(r => r.ChangeHistory.First(c => c.ChangeAction == APIRevisionChangeAction.Approved).ChangedOn);

            return Page();
        }
    }
}
