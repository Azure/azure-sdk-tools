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

        // Feature flag for pending namespace approvals tab UI
        public bool EnablePendingReviewTab
        {
            get
            {
                var enablePendingReviewTab = _configuration["EnablePendingReviewTab"];
                if (bool.TryParse(enablePendingReviewTab, out bool isEnabled))
                {
                    return isEnabled;
                }
                return false;
            }
        }

        /// <summary>
        /// Updates the latest revisions dictionary with the newest revision for a given review ID
        /// </summary>
        private void UpdateLatestRevisionForNamespaceReview(Dictionary<string, APIRevisionListItemModel> latestRevisions, APIRevisionListItemModel revision)
        {
            if (!latestRevisions.ContainsKey(revision.ReviewId))
            {
                latestRevisions[revision.ReviewId] = revision;
            }
            else
            {
                // Compare and keep the latest revision (most recent CreatedOn)
                var existingRevision = latestRevisions[revision.ReviewId];
                if (revision.CreatedOn > existingRevision.CreatedOn)
                {
                    latestRevisions[revision.ReviewId] = revision;
                }
            }
        }
        
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
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    userProfile = await _userProfileCache.GetUserProfileAsync(userId);
                }
            }
            catch (OperationCanceledException)
            {
                _telemetryClient?.TrackException(new TimeoutException("User profile loading timed out"));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("DefaultTempDataSerializer"))
            {
                _telemetryClient?.TrackException(ex, new Dictionary<string, string> { { "Context", "TempDataSerializer error during user profile loading" } });
            }
            catch (Exception ex)
            {
                _telemetryClient?.TrackException(ex, new Dictionary<string, string> { { "Context", "Failed to load user profile" }, { "UserId", userId } });
            }
            
            IEnumerable<APIRevisionListItemModel> assignedRevisions;
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    assignedRevisions = await _apiRevisionsManager.GetAPIRevisionsAssignedToUser(userId);
                }
            }
            catch (Exception ex)
            {
                _telemetryClient?.TrackException(ex, new Dictionary<string, string> { { "Context", "Failed to load assigned revisions" }, { "UserId", userId } });
                assignedRevisions = new List<APIRevisionListItemModel>();
            }
            
            APIRevisions = assignedRevisions;

            var activeAPIRevs = new List<APIRevisionListItemModel>();
            var approvedAPIRevs = new List<APIRevisionListItemModel>();
            var namespaceApprovalAPIRevs = new List<APIRevisionListItemModel>();

            var allNamespaceApprovalReviews = new List<ReviewListItemModel>();
            // Always load namespace approval reviews for all users
            try 
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    allNamespaceApprovalReviews = await _reviewManager.GetPendingNamespaceApprovalsBatchAsync(30);
                }
            }
            catch (Exception ex)
            {
                _telemetryClient?.TrackException(ex, new Dictionary<string, string> { { "Context", "Failed to load namespace approval reviews" } });
            }
            
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
                        catch (Exception ex)
                        {
                            _telemetryClient?.TrackException(ex, new Dictionary<string, string> { { "Context", "Failed to load individual review" }, { "ReviewId", reviewId } });
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
            catch (Exception ex)
            {
                _telemetryClient?.TrackException(ex, new Dictionary<string, string> { { "Context", "Failed to load reviews in batch" }, { "ReviewCount", reviewIds.Count.ToString() } });
            }

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
                    UpdateLatestRevisionForNamespaceReview(latestRevisionsForNamespaceReviews, apiRevision);
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
                            UpdateLatestRevisionForNamespaceReview(latestRevisionsForNamespaceReviews, latestRevision);
                        }
                    }
                    catch (Exception ex)
                    {
                        _telemetryClient?.TrackException(ex, new Dictionary<string, string> { { "Context", "Failed to get revision for namespace review" }, { "ReviewId", namespaceReview.Id } });
                    }
                }
            }
            
            // Convert the dictionary values to the final list
            namespaceApprovalAPIRevs = latestRevisionsForNamespaceReviews.Values.ToList();
            
            ActiveAPIRevisions = activeAPIRevs;
            ApprovedAPIRevisions = approvedAPIRevs;
            NamespaceApprovalRequestedAPIRevisions = namespaceApprovalAPIRevs;
            
            ReviewsWithoutNamespaceApproval = new List<APIRevisionListItemModel>();
            
            var orderedApprovedAPIRevs = ApprovedAPIRevisions.OrderByDescending(r => 
                r.ChangeHistory.FirstOrDefault(c => c.ChangeAction == APIRevisionChangeAction.Approved)?.ChangedOn ?? DateTime.MinValue);
            ApprovedAPIRevisions = orderedApprovedAPIRevs;

            return Page();
        }
    }
}
